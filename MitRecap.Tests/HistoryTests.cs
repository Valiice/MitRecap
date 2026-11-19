using MitRecap.Core;
using Xunit;

namespace MitRecap.Tests;

public class HistoryTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 20, 0, 0);

    private static DateTime At(double seconds) => Start.AddSeconds(seconds);

    [Fact]
    public void EachDutyGetsItsOwnLogAndOldOnesAreKept()
    {
        var history = new History();
        var first = history.StartNew(At(0));
        first.AddAction(At(1), 1, 10, [], []);
        first.StopSession(At(50));
        var second = history.StartNew(At(100));

        Assert.Equal([first, second], history.Logs);
        Assert.Same(second, history.Current);
        Assert.Single(first.Events);
        Assert.Empty(second.Events);

        for (var i = 0; i < History.MaxLogs + 5; i++)
        {
            history.StartNew(At(200 + i));
        }

        Assert.Equal(History.MaxLogs, history.Logs.Count);
    }
}
