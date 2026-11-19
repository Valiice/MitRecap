namespace MitRecap.Core;

// Level is the synced level.
public sealed record PlayerInfo(uint Id, string Name, string Job, uint JobId = 0, JobRole Role = JobRole.None, byte Level = 0);
