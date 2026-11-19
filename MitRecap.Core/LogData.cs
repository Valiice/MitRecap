namespace MitRecap.Core;

public sealed record LogData(DateTime Started, string DutyName, List<TimeSpan> Wipes, List<PlayerInfo> Players, List<PullData> Pulls, List<RecapEvent> Events);

public sealed record PullData(int Number, TimeSpan Start, TimeSpan? End, uint[] AutoAttacked);
