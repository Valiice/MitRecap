using Dalamud.Configuration;

namespace MitRecap;

public class Configuration : IPluginConfiguration
{
    public const int CurrentVersion = 5;

    private static HashSet<uint>? defaultHidden;

    public int Version { get; set; }

    public HashSet<uint> HiddenActions { get; set; } = [];

    public static HashSet<uint> DefaultHidden => defaultHidden ??= Actions.Resolve(
    [
        "Embrace", "Seraphic Veil",
        "Sprint", "Peloton", "Swiftcast", "Lucid Dreaming", "Surecast",
        "Provoke", "Shirk", "Low Blow", "Interject", "Rescue",
        "Shadowstride", "Trajectory", "Aetherial Shift", "Icarus", "Living Shadow", "Summon Eos",
        "Minor Arcana", "Play I", "Play II", "Play III",
        "Fight or Flight", "Requiescat", "Imperator", "Iron Will", "Release Iron Will",
        "Berserk", "Inner Release", "Infuriate", "Defiance", "Release Defiance",
        "Blood Weapon", "Delirium", "Grit", "Release Grit", "Salted Earth",
        "No Mercy", "Bloodfest", "Royal Guard", "Release Royal Guard",
        "Presence of Mind", "Thin Air",
        "Aetherflow", "Chain Stratagem",
        "Lightspeed", "Divination", "Astral Draw", "Umbral Draw", "The Balance", "The Spear",
        "Kardia", "Eukrasia", "Rhizomata",
    ]);

    public void Save() => Services.PluginInterface.SavePluginConfig(this);
}
