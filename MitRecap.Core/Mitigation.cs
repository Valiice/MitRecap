namespace MitRecap.Core;

public static class Mitigation
{
    // Mitigations multiply (20% and 10% leave 72%), so the unmitigated hit is damage / product.
    // What that saved is shared by each mitigation's log weight: it adds up and does not depend on order.
    public static double[] Split(double damage, IReadOnlyList<double> reductions)
    {
        var logs = reductions.Select(r => r is > 0 and < 1 ? Math.Log(1 - r) : 0).ToArray();
        var logProduct = logs.Sum();
        if (damage <= 0 || logProduct >= 0)
        {
            return new double[logs.Length];
        }

        var saved = (damage / Math.Exp(logProduct)) - damage;
        return [.. logs.Select(l => saved * l / logProduct)];
    }

    public static double[] SplitAbsorbed(double damage, double shieldHp, int shields)
    {
        var absorbed = new double[shields];
        if (shields == 0 || damage <= 0 || shieldHp <= 0)
        {
            return absorbed;
        }

        if (damage < shieldHp)
        {
            // Overcredits the first shield when it broke mid hit, which cannot be seen.
            absorbed[0] = damage;
        }
        else
        {
            Array.Fill(absorbed, shieldHp / shields);
        }

        return absorbed;
    }
}
