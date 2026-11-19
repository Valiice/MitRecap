using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using MitRecap.Core;

namespace MitRecap;

internal sealed unsafe class MitCredit(History history, IReadOnlyDictionary<uint, JobRole> party)
{
    private const uint LivingDeadAction = 3638;
    private const uint InterventionAction = 7382;
    private const uint InterventionStatus = 1174;
    private const uint Rampart = 1191;
    private const uint Sentinel = 74;
    private const uint Guardian = 3829;

    // Statuses that heal their holder on each hit. The action is set when the press does not apply the status itself.
    private static readonly Dictionary<uint, uint> LifeSteal = new()
    {
        [735] = 0, // Raw Intuition
        [2678] = 0, // Bloodwhetting
        [1857] = 0, // Nascent Flash
        [84] = 0, // Bloodbath
        [811] = LivingDeadAction, // Walking Dead
    };

    private readonly List<(uint Source, uint StatusId, uint ActionId)> mits = [];
    private readonly List<double> reductions = [];
    private readonly List<(int Priority, uint Source, uint StatusId)> shields = [];

    // Intervention is 20% instead of 10% when pressed with Rampart, Sentinel or Guardian up.
    private readonly HashSet<(uint Source, uint Target)> strongInterventions = [];

    private RecapLog Log => history.Current;

    public void Reset() => this.strongInterventions.Clear();

    public void OnPress(Character* caster, uint casterId, uint actionId, IReadOnlyList<TargetHit> targets)
    {
        if (actionId != InterventionAction || caster == null)
        {
            return;
        }

        var own = caster->GetStatusManager();
        var strong = own != null && (own->HasStatus(Rampart) || own->HasStatus(Sentinel) || own->HasStatus(Guardian));
        foreach (var t in targets)
        {
            if (strong)
            {
                this.strongInterventions.Add((casterId, t.Id));
            }
            else
            {
                this.strongInterventions.Remove((casterId, t.Id));
            }
        }
    }

    public void OnLifeSteal(uint casterId, uint healed)
    {
        if (healed == 0 || Services.ObjectTable.SearchByEntityId(casterId) is not ICharacter caster)
        {
            return;
        }

        var statuses = ((Character*)caster.Address)->GetStatusManager();
        if (statuses == null)
        {
            return;
        }

        foreach (ref var status in statuses->Status)
        {
            if (LifeSteal.TryGetValue(status.StatusId, out var actionId))
            {
                var room = caster.MaxHp - Math.Min(caster.CurrentHp, caster.MaxHp);
                this.Log.CreditHeal(DateTime.Now, casterId, status.StatusId, actionId, healed, healed > room ? healed - room : 0);
                return;
            }
        }
    }

    public void OnEnemyAction(
        Character* attacker,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetIds)
    {
        var autoAttack = Actions.Row(header->ActionId)?.ActionCategory.RowId == ActionCategories.AutoAttack;
        for (var i = 0; i < header->NumTargets; i++)
        {
            var targetId = targetIds[i].ObjectId;
            if (!party.ContainsKey(targetId))
            {
                continue;
            }

            for (var j = 0; j < Effects.PerTarget; j++)
            {
                ref var effect = ref effects[i].Effects[j];

                var type = (EffectType)effect.Type;
                var hit = type is EffectType.Damage or EffectType.BlockedDamage or EffectType.ParriedDamage;
                var damage = hit ? Effects.Value(effect) : 0;
                if ((damage == 0 && type != EffectType.InstantDeath) || Services.ObjectTable.SearchByEntityId(targetId) is not ICharacter target)
                {
                    continue;
                }

                var source = attacker != null ? attacker->NameString : "";

                // A wipe mechanic such as Ancient Flare kills outright and carries no damage number, so it takes all the HP left.
                if (type == EffectType.InstantDeath)
                {
                    this.Log.AddDamage(DateTime.Now, targetId, source, header->ActionId, target.CurrentHp, target.CurrentHp, target.MaxHp);
                    continue;
                }

                this.Log.AddDamage(DateTime.Now, targetId, source, header->ActionId, damage, target.CurrentHp, target.MaxHp, autoAttack);
                this.CreditMitigations(attacker, target, damage, (DamageType)(effect.Param1 & 0xF));
                this.CreditShields(target, damage);
            }
        }
    }

    private void CreditMitigations(Character* attacker, ICharacter target, uint damage, DamageType damageType)
    {
        this.mits.Clear();
        this.reductions.Clear();
        this.Collect((Character*)target.Address, onAttacker: false, damageType, target.Level);
        if (attacker != null)
        {
            this.Collect(attacker, onAttacker: true, damageType, target.Level);
        }

        var saved = Mitigation.Split(damage, this.reductions);
        for (var k = 0; k < saved.Length; k++)
        {
            this.Log.Credit(DateTime.Now, this.mits[k].Source, this.mits[k].StatusId, this.mits[k].ActionId, saved[k]);
        }
    }

    private void Collect(Character* character, bool onAttacker, DamageType damageType, int level)
    {
        var statuses = character->GetStatusManager();
        if (statuses == null)
        {
            return;
        }

        foreach (ref var status in statuses->Status)
        {
            if (!MitTable.TryGet(status.StatusId, out var info) || info.OnAttacker != onAttacker)
            {
                continue;
            }

            var reduction = MitTable.Reduction(info, damageType, level);
            if (reduction <= 0)
            {
                continue;
            }

            var source = this.Owner(status.SourceObject.ObjectId);
            if (status.StatusId == InterventionStatus && this.strongInterventions.Contains((source, character->EntityId)))
            {
                reduction *= 2;
            }

            this.mits.Add((source, status.StatusId, info.ActionId));
            this.reductions.Add(reduction);
        }
    }

    // The packet's damage is the hit before shields, and the shield read here is from just before it lands.
    private void CreditShields(ICharacter target, uint damage)
    {
        var statuses = target.ShieldPercentage > 0 ? ((Character*)target.Address)->GetStatusManager() : null;
        if (statuses == null)
        {
            return;
        }

        this.shields.Clear();
        foreach (ref var status in statuses->Status)
        {
            if (MitTable.TryGetShield(status.StatusId, out var priority))
            {
                this.shields.Add((priority, this.Owner(status.SourceObject.ObjectId), status.StatusId));
            }
        }

        this.shields.Sort(static (a, b) => a.Priority.CompareTo(b.Priority));
        var absorbed = Mitigation.SplitAbsorbed(damage, target.ShieldPercentage / 100.0 * target.MaxHp, this.shields.Count);
        for (var k = 0; k < absorbed.Length; k++)
        {
            this.Log.Credit(DateTime.Now, this.shields[k].Source, this.shields[k].StatusId, 0, absorbed[k]);
        }
    }

    private uint Owner(uint id) =>
        !party.ContainsKey(id) && Services.ObjectTable.SearchByEntityId(id) is IBattleNpc pet && party.ContainsKey(pet.OwnerId) ? pet.OwnerId : id;
}
