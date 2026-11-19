using System.IO.Compression;
using System.Text.Json;

namespace MitRecap.Core;

// Unlike the export, a share code holds character names.
public static class RecapShare
{
    private const string Prefix = "MITRECAP1:";

    // A code comes from a stranger's clipboard, so its size is capped before and after unpacking.
    private const int MaxCodeLength = 8_000_000;
    private const int MaxJsonBytes = 64_000_000;
    private const int MaxPlayers = 64;

    public static string Encode(RecapLog log)
    {
        using var packed = new MemoryStream();
        using (var gzip = new GZipStream(packed, CompressionLevel.SmallestSize))
        {
            JsonSerializer.Serialize(gzip, log.ToData());
        }

        return Prefix + Convert.ToBase64String(packed.ToArray());
    }

    public static RecapLog? Decode(string code)
    {
        code = code.Trim();
        if (!code.StartsWith(Prefix, StringComparison.Ordinal) || code.Length > MaxCodeLength)
        {
            return null;
        }

        try
        {
            using var gzip = new GZipStream(new MemoryStream(Convert.FromBase64String(code[Prefix.Length..])), CompressionMode.Decompress);
            using var json = new MemoryStream();
            var chunk = new byte[81920];
            for (int read; (read = gzip.Read(chunk)) > 0;)
            {
                if (json.Length + read > MaxJsonBytes)
                {
                    return null;
                }

                json.Write(chunk, 0, read);
            }

            var data = JsonSerializer.Deserialize<LogData>(json.ToArray());
            return data != null && IsValid(data) ? RecapLog.FromData(data) : null;
        }
        catch (Exception e) when (e is FormatException or InvalidDataException or JsonException or NotSupportedException)
        {
            return null;
        }
    }

    // The window indexes pulls by number and never expects a null, so a code that breaks either is refused.
    private static bool IsValid(LogData d) =>
        d is { DutyName: not null, Wipes: not null, Players: not null, Pulls: not null, Events: not null } &&
        d.Started != default &&
        d.Players.Count <= MaxPlayers &&
        d.Events.Count <= RecapLog.MaxEvents &&
        d.Players.All(p => p is { Name: not null, Job: not null }) &&
        d.Pulls.Select((p, i) => p is { AutoAttacked: not null } && p.Number == i + 1).All(ok => ok) &&
        d.Events.All(e =>
            e is { Targets: not null, StatusIds: not null, LastHits: not null } &&
            e.Pull >= 0 && e.Pull <= d.Pulls.Count &&
            e.Targets.All(t => t.Name != null) &&
            e.LastHits.All(h => h.Source != null));
}
