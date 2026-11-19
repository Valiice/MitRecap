namespace MitRecap.Core;

public sealed class Pull(int number, TimeSpan start)
{
    public int Number { get; } = number;

    public TimeSpan Start { get; } = start;

    public TimeSpan? End { get; internal set; }

    public HashSet<uint> AutoAttacked { get; } = [];
}
