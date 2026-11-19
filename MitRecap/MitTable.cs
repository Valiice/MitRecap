using MitRecap.Core;

namespace MitRecap;

// Percent removed. OnAttacker: a debuff on the enemy. ActionId: set for ground effects, whose status the press does not apply itself.
internal readonly record struct MitInfo(double Physical, double Magic, bool OnAttacker = false, uint ActionId = 0);

// Ids and numbers are from the game's status sheet and tooltips (7.x). Blocks, parries and invulnerabilities are left out.
internal static class MitTable
{
    // Troubadour, Tactician and Shield Samba: 10%, raised to 15% by a level 98 trait.
    private const double RangedScaled = -1;

    private static readonly Dictionary<uint, MitInfo> ByStatus = new()
    {
        // Role actions
        [1191] = new(20, 20), // Rampart
        [1193] = new(10, 10, OnAttacker: true), // Reprisal
        [1195] = new(10, 5, OnAttacker: true), // Feint
        [1203] = new(5, 10, OnAttacker: true), // Addle

        // Paladin
        [74] = new(30, 30), // Sentinel
        [3829] = new(40, 40), // Guardian
        [1856] = new(15, 15), // Sheltron
        [2674] = new(15, 15), // Holy Sheltron
        [2675] = new(15, 15), // Knight's Resolve
        [1174] = new(10, 10), // Intervention (doubled by MitCredit when pressed with Rampart or Guardian up)
        [1176] = new(15, 15, ActionId: 7385), // Arms Up, from Passage of Arms

        // Warrior
        [89] = new(30, 30), // Vengeance
        [3832] = new(40, 40), // Damnation
        [735] = new(10, 10), // Raw Intuition
        [2678] = new(10, 10), // Bloodwhetting
        [2679] = new(10, 10), // Stem the Flow
        [1858] = new(10, 10), // Nascent Glint

        // Dark Knight
        [747] = new(30, 30), // Shadow Wall
        [3835] = new(40, 40), // Shadowed Vigil
        [746] = new(10, 20), // Dark Mind
        [1894] = new(5, 10), // Dark Missionary
        [2682] = new(10, 10), // Oblation

        // Gunbreaker
        [1834] = new(30, 30), // Nebula
        [3838] = new(40, 40), // Great Nebula
        [1832] = new(10, 10), // Camouflage
        [1839] = new(5, 10), // Heart of Light
        [1840] = new(15, 15), // Heart of Stone
        [2683] = new(15, 15), // Heart of Corundum
        [2684] = new(15, 15), // Clarity of Corundum

        // White Mage
        [1872] = new(10, 10), // Temperance (caster)
        [1873] = new(10, 10), // Temperance (party)
        [2708] = new(15, 15), // Aquaveil
        [1219] = new(10, 10), // Confession, from Plenary Indulgence

        // Scholar
        [299] = new(10, 10, ActionId: 188), // Sacred Soil
        [317] = new(0, 5), // Fey Illumination
        [1875] = new(0, 5), // Seraphic Illumination
        [2711] = new(10, 10), // Desperate Measures, from Expedient

        // Astrologian
        [849] = new(10, 10, ActionId: 3613), // Collective Unconscious
        [2717] = new(10, 10), // Exaltation
        [3896] = new(10, 10), // Sun Sign
        [3890] = new(10, 10), // The Bole

        // Sage
        [2618] = new(10, 10), // Kerachole
        [2619] = new(10, 10), // Taurochole
        [3003] = new(10, 10), // Holos

        // DPS
        [1934] = new(RangedScaled, RangedScaled), // Troubadour
        [1951] = new(RangedScaled, RangedScaled), // Tactician
        [1826] = new(RangedScaled, RangedScaled), // Shield Samba
        [860] = new(10, 10, OnAttacker: true), // Dismantled
        [2707] = new(0, 10), // Magick Barrier
        [1179] = new(20, 20), // Riddle of Earth
        [1232] = new(10, 10), // Third Eye
        [3853] = new(10, 10), // Tengentsu
        [3854] = new(10, 10), // Tengentsu's Foresight
    };

    // Value is the order the game drains them in, lowest first (community wiki, Barrier page).
    private static readonly Dictionary<uint, int> Shields = new()
    {
        [2596] = 1, // Crest of Time Borrowed (11 below level 84, not modelled)
        [2597] = 1,
        [3686] = 1, // Tempera Coat
        [3687] = 2, // Tempera Grassa
        [1178] = 3, // The Blackest Night
        [2607] = 4, // Eukrasian Diagnosis
        [2612] = 5, // Haima
        [2613] = 6, // Panhaima
        [1898] = 7, // Brutal Shell
        [2680] = 8, // Stem the Tide, from Bloodwhetting and Nascent Flash
        [3830] = 9, // Guardian's Will, from Guardian
        [3903] = 10, // Divine Caress
        [168] = 11, // Manaward
        [488] = 11, // Shade Shift
        [1218] = 12, // Divine Benison
        [1889] = 13, // Intersection
        [3892] = 14, // The Spire
        [2609] = 15, // Eukrasian Prognosis
        [1917] = 16, // Seraphic Veil
        [3365] = 17, // Holosakos, from Holos
        [2702] = 18, // Radiant Aegis
        [1457] = 19, // Shake It Off
        [727] = 20, // Divine Veil
        [1362] = 20,
        [1921] = 21, // Neutral Sect
        [2697] = 22, // Improvised Finish
        [297] = 23, // Galvanize
        [2608] = 24, // Differential Diagnosis
        [1918] = 25, // Catalyze
    };

    private static readonly HashSet<uint> LinkedActions = [.. ByStatus.Values.Where(m => m.ActionId != 0).Select(m => m.ActionId)];

    public static bool TryGet(uint statusId, out MitInfo info) => ByStatus.TryGetValue(statusId, out info);

    public static bool TryGetShield(uint statusId, out int priority) => Shields.TryGetValue(statusId, out priority);

    public static double Reduction(MitInfo info, DamageType damageType, int level)
    {
        var percent = damageType switch
        {
            DamageType.Slashing or DamageType.Piercing or DamageType.Blunt or DamageType.Shot or DamageType.Physical => info.Physical,
            DamageType.Magic => info.Magic,

            // Untyped damage ignores type specific mitigation.
            _ => info.Physical == info.Magic ? info.Physical : 0,
        };

        return (percent == RangedScaled ? (level >= 98 ? 15 : 10) : percent) / 100;
    }

    public static bool Tracks(RecapEvent e)
    {
        if (LinkedActions.Contains(e.ActionId))
        {
            return true;
        }

        for (var i = 0; i < e.StatusIds.Count; i++)
        {
            if (ByStatus.ContainsKey(e.StatusIds[i]) || Shields.ContainsKey(e.StatusIds[i]))
            {
                return true;
            }
        }

        return false;
    }
}
