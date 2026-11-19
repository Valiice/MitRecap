using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using MitRecap.Core;

namespace MitRecap;

internal sealed unsafe class Capture : IDisposable
{
    private readonly History history;
    private readonly DutyTracker duty;
    private readonly MitCredit credit;
    private readonly Hook<ActionEffectHandler.Delegates.Receive> hook;

    private readonly HashSet<uint> dpsMits = Actions.Resolve(
    [
        "Feint", "Addle", "Second Wind", "Bloodbath", "Arm's Length",
        "Troubadour", "Tactician", "Shield Samba", "Dismantle", "Magick Barrier", "Mantra",
        "Nature's Minne", "The Warden's Paean", "Curing Waltz", "Improvisation", "Improvised Finish",
        "Third Eye", "Tengentsu", "Riddle of Earth", "Earth's Reply", "Shade Shift", "Arcane Crest",
        "Manaward", "Radiant Aegis", "Tempera Coat", "Tempera Grassa", "Vercure", "Rekindle", "Lux Solaris", "Star Prism",
    ]);

    // These raise max HP and heal the amount added, so their heal is measured against the raised maximum.
    private readonly Dictionary<uint, uint> maxHpRaisePercent = Actions.Resolve(new Dictionary<string, uint>
    {
        ["Thrill of Battle"] = 20,
        ["Great Nebula"] = 20,
        ["Protraction"] = 10,
    });

    public Capture(History history, DutyTracker duty)
    {
        this.history = history;
        this.duty = duty;
        this.credit = new MitCredit(history, duty.Party);
        this.duty.SessionStarted += this.credit.Reset;
        this.hook = Services.GameInterop.HookFromAddress<ActionEffectHandler.Delegates.Receive>(
            ActionEffectHandler.Addresses.Receive.Value,
            this.OnActionEffect);
        this.hook.Enable();
    }

    private RecapLog Log => this.history.Current;

    public void Dispose()
    {
        this.duty.SessionStarted -= this.credit.Reset;
        this.hook.Disable();
        this.hook.Dispose();
    }

    private void OnActionEffect(
        uint casterEntityId,
        Character* caster,
        Vector3* targetPos,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds)
    {
        this.hook.Original(casterEntityId, caster, targetPos, header, effects, targetEntityIds);

        try
        {
            if (this.Log.Recording && (ActionType)header->ActionType == ActionType.Action)
            {
                this.Record(casterEntityId, caster, header, effects, targetEntityIds);
            }
        }
        catch (Exception e)
        {
            Services.Log.Error(e, "MitRecap: action capture failed");
        }
    }

    private void Record(
        uint casterId,
        Character* caster,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetIds)
    {
        if (!this.duty.Party.TryGetValue(casterId, out var role))
        {
            if (caster == null || !this.duty.Party.TryGetValue(caster->OwnerId, out role))
            {
                this.credit.OnEnemyAction(caster, header, effects, targetIds);
                return;
            }

            casterId = caster->OwnerId;
        }

        var actionId = header->ActionId;
        int targetCount = header->NumTargets;
        var dpsNoise = role is JobRole.Melee or JobRole.Ranged && !this.dpsMits.Contains(actionId);

        if (targetCount == 0)
        {
            // Ground effects (Asylum, Sacred Soil) hit nobody when placed. Duty actions such as a mech's cannon have no job level.
            if (!dpsNoise && Actions.Row(actionId) is { TargetArea: true, ClassJobLevel: > 0 })
            {
                this.Log.AddAction(DateTime.Now, casterId, actionId, [], []);
            }

            return;
        }

        var summary = new ActionSummary();
        var healed = new uint[targetCount];
        uint casterHealed = 0;
        var casterIndex = 0;
        for (var i = 0; i < targetCount; i++)
        {
            casterIndex = targetIds[i].ObjectId == casterId ? i : casterIndex;
            for (var j = 0; j < Effects.PerTarget; j++)
            {
                ref var effect = ref effects[i].Effects[j];
                var amount = summary.AddEffect((EffectType)effect.Type, Effects.Value(effect));
                if (Effects.AppliesToCaster(effect))
                {
                    casterHealed += amount;
                }
                else
                {
                    healed[i] += amount;
                }
            }
        }

        var attack = summary.Attacked;
        if (attack)
        {
            this.credit.OnLifeSteal(casterId, casterHealed);
        }
        else
        {
            healed[casterIndex] += casterHealed;
        }

        var listed = attack
            ? !dpsNoise && role != JobRole.Tank && healed.Any(h => h > 0)
            : summary.IsMitOrHeal && !(dpsNoise && !summary.Healed);
        if (!listed)
        {
            return;
        }

        var targets = new List<TargetHit>(targetCount);
        var maxHpScale = 100 + this.maxHpRaisePercent.GetValueOrDefault(actionId);
        var anyFriendly = false;
        for (var i = 0; i < targetCount; i++)
        {
            if (attack && healed[i] == 0)
            {
                continue;
            }

            var id = targetIds[i].ObjectId;
            var obj = Services.ObjectTable.SearchByEntityId(id);

            var friendly = !(obj is IBattleNpc npc && (BattleNpcSubKind)(byte)npc.BattleNpcKind is BattleNpcSubKind.Combatant or BattleNpcSubKind.BNpcPart);
            anyFriendly |= friendly;
            var c = obj as ICharacter;
            var maxHp = (c?.MaxHp ?? 0) * maxHpScale / 100;
            targets.Add(new TargetHit(id, obj?.Name.TextValue ?? "?", friendly, c?.CurrentHp ?? 0, maxHp, c?.ShieldPercentage ?? 0, healed[i]));
        }

        // A status on enemies only is a mitigation when it comes from an ability and a DoT when it comes from a spell.
        if (!anyFriendly && Actions.Row(actionId)?.ActionCategory.RowId != ActionCategories.Ability)
        {
            return;
        }

        this.credit.OnPress(caster, casterId, actionId, targets);
        this.Log.AddAction(DateTime.Now, casterId, actionId, targets, attack ? [] : summary.StatusIds);
    }
}
