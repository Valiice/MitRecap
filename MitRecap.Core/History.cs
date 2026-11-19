namespace MitRecap.Core;

public sealed class History
{
    public const int MaxLogs = 20;

    private readonly List<RecapLog> logs = [new RecapLog()];

    public IReadOnlyList<RecapLog> Logs => this.logs;

    public RecapLog Current => this.logs[^1];

    public RecapLog StartNew(DateTime now)
    {
        if (this.Current.Started != default)
        {
            this.logs.Add(new RecapLog());
            if (this.logs.Count > MaxLogs)
            {
                this.logs.RemoveAt(0);
            }
        }

        this.Current.StartSession(now);
        return this.Current;
    }

    public void Import(RecapLog log)
    {
        this.logs.Insert(this.logs.Count - 1, log);
        if (this.logs.Count > MaxLogs)
        {
            this.logs.RemoveAt(0);
        }
    }
}
