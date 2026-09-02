namespace LivingWorld.Domain;

/// <summary>Geração determinística de <c>Height</c>/<c>Weight</c>/<c>MuscleMass</c>
/// (Fase 16.3, COH-21) — normal truncada via Box-Muller + clamp em <see cref="BodyRules"/>.
/// Funções puras, mesmo molde de <see cref="HeredityService"/>.</summary>
public static class BodyGeneration
{
    /// <summary>Sorteia altura (metros) a partir do stream do chamador — sempre em
    /// <c>[HeightMin, HeightMax]</c>.</summary>
    public static double RollHeight(WorldRng rng, BodyRules rules) =>
        SampleTruncatedNormal(rng, rules.HeightMean, rules.HeightStdDev, rules.HeightMin, rules.HeightMax);

    /// <summary>Sorteia peso (kg) — sempre em <c>[WeightMin, WeightMax]</c>.</summary>
    public static double RollWeight(WorldRng rng, BodyRules rules) =>
        SampleTruncatedNormal(rng, rules.WeightMean, rules.WeightStdDev, rules.WeightMin, rules.WeightMax);

    /// <summary>Sorteia massa muscular (kg) — sempre em
    /// <c>[MuscleMassMin, MuscleMassMax]</c>.</summary>
    public static double RollMuscleMass(WorldRng rng, BodyRules rules) =>
        SampleTruncatedNormal(
            rng, rules.MuscleMassMean, rules.MuscleMassStdDev, rules.MuscleMassMin, rules.MuscleMassMax);

    private static double SampleTruncatedNormal(
        WorldRng rng, double mean, double stdDev, double min, double max)
    {
        if (stdDev <= 0)
            return Math.Clamp(mean, min, max);

        // Box-Muller: dois NextDouble → um z ~ N(0,1) determinístico.
        double u1 = Math.Max(rng.NextDouble(), 1e-12);
        double u2 = rng.NextDouble();
        double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        return Math.Clamp(mean + z * stdDev, min, max);
    }
}
