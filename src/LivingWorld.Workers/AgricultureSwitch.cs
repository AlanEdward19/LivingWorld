using System.Globalization;
using LivingWorld.Domain.Economy;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Workers;

/// <summary>Medição da lavoura (AD-149), só para os relatórios: <c>LW_AG</c> com vírgulas. <c>noland</c> = terra sem limite;
/// <c>noreap</c> = sem a ceifa da vila toda; <c>presence</c> liga <c>EconomyRules.PresenceNeedsWork</c> (E1), <c>nopresence</c>
/// desliga; <c>land=x</c>, <c>gain=x</c>, <c>reap=n</c>, <c>keep=n</c>, <c>seed=n</c>, <c>yield=x</c> trocam o número.</summary>
public static class AgricultureSwitch
{
    private static HashSet<string> Keys() =>
        (Environment.GetEnvironmentVariable("LW_AG") ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet();

    private static double? Number(string key) =>
        Keys().FirstOrDefault(k => k.StartsWith(key + "=", StringComparison.Ordinal)) is { } raw
            ? double.Parse(raw[(key.Length + 1)..], CultureInfo.InvariantCulture) : null;

    public static AgricultureRules? AgricultureRules()
    {
        var keys = Keys();
        if (keys.Count == 0) return null;
        var ag = Domain.Economy.AgricultureRules.Default;
        if (keys.Contains("noland")) ag = ag with { SownHectaresPerFounder = 0 };
        if (keys.Contains("noreap")) ag = ag with { WholeVillageReaps = false };
        if (Number("land") is { } land) ag = ag with { SownHectaresPerFounder = land };
        if (Number("gain") is { } gain) ag = ag with { HandIntensityGain = gain };
        if (Number("reap") is { } reap) ag = ag with { ReapPerWorkerDay = (long)reap };
        if (Number("keep") is { } keep) ag = ag with { HandKeepPerYear = (long)keep };
        if (Number("seed") is { } seed) ag = ag with { SeedPerResident = (long)seed };
        if (Number("yield") is { } yield) ag = ag with { YieldPerSeed = yield };
        return ag;
    }

    public static EconomyRules? EconomyRules()
    {
        var keys = Keys();
        if (keys.Contains("presence")) return ScenarioRunner.DefaultEconomyRules with { PresenceNeedsWork = true };
        if (keys.Contains("nopresence")) return ScenarioRunner.DefaultEconomyRules with { PresenceNeedsWork = false };
        return null;
    }
}
