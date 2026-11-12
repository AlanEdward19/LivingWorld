using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Population.Family;

/// <summary>Criação (AD-132): o ambiente compartilhado de quem de fato criou a criança entra na personalidade adulta. A cada
/// <see cref="SampleEveryDays"/> dias da infância grava-se quem eram os adultos da casa; na passagem para a vida adulta a
/// personalidade herdada recebe a parte <see cref="SharedEnvironmentWeight"/> da média desses cuidadores, de forma que a
/// variância da população fique a mesma. Peso aproximado, a conferir: estudos de gêmeos e adoção dão herdabilidade de
/// personalidade na ordem de 40–50% e ambiente compartilhado pequeno (0–10% da variância); 0,3 aqui dá ~4,5% da variância
/// com dois cuidadores e ~9% com um.</summary>
public sealed record RearingRules(double SharedEnvironmentWeight, int SampleEveryDays)
{
    public static RearingRules Default { get; } = new(SharedEnvironmentWeight: 0.3, SampleEveryDays: 30);

    /// <summary>Sem ambiente compartilhado: a personalidade é só a herdada (o comportamento de antes).</summary>
    public static RearingRules Disabled { get; } = new(SharedEnvironmentWeight: 0, SampleEveryDays: 30);

    public bool Enabled => SharedEnvironmentWeight > 0;

    public static Result<RearingRules> Create(double sharedEnvironmentWeight, int sampleEveryDays)
    {
        if (sharedEnvironmentWeight is < 0 or > 1) return Result<RearingRules>.Fail("SharedEnvironmentWeight: fora de [0,1]");
        if (sampleEveryDays <= 0) return Result<RearingRules>.Fail("SampleEveryDays: deve ser > 0");
        return Result<RearingRules>.Ok(new RearingRules(sharedEnvironmentWeight, sampleEveryDays));
    }

    /// <summary>Personalidade adulta: <c>50 + a·(herdado − 50) + c·(cuidadores − 50)</c>, com <c>c</c> = <see
    /// cref="SharedEnvironmentWeight"/> e <c>a</c> resolvido para conservar a variância. Os cuidadores são a média de
    /// <c>n</c> adultos (variância V/n) e, na fração <c>q</c> em que são os pais biológicos, covariam com o herdado em
    /// <c>h·V/2</c>; daí <c>a² + a·c·q·h + c²/n = 1</c>. Sem amostra, devolve o herdado.</summary>
    public Personality Apply(Personality inherited, RearingRecord record, double heritability)
    {
        if (!Enabled || record.Samples <= 0) return inherited;
        double c = SharedEnvironmentWeight;
        double q = record.BiologicalShareSum / record.Samples;
        double n = Math.Max(1.0, record.CaregiverCountSum / record.Samples);
        double x = c * q * Math.Clamp(heritability, 0.0, 1.0) / 2;
        double a = -x + Math.Sqrt(x * x + 1 - c * c / n);
        var mean = record.TraitSums;
        int Mix(int trait, int i) => Math.Clamp(
            (int)Math.Round(50 + a * (trait - 50) + c * (mean[i] / record.Samples - 50), MidpointRounding.AwayFromZero), 0, 100);
        return new Personality(
            Mix(inherited.Extroversion, 0), Mix(inherited.Agreeableness, 1), Mix(inherited.Conscientiousness, 2),
            Mix(inherited.EmotionalStability, 3), Mix(inherited.Openness, 4), Mix(inherited.Ambition, 5),
            Mix(inherited.Loyalty, 6), Mix(inherited.Altruism, 7), Mix(inherited.Impulsivity, 8), Mix(inherited.RiskAversion, 9));
    }
}

/// <summary>Quem criou a criança, acumulado ao longo da infância (estado canônico do NPC; nulo fora da infância). Cada amostra
/// soma a média dos traços dos adultos da casa, quantos eram e que fração deles eram os pais biológicos.</summary>
public sealed record RearingRecord(IReadOnlyList<double> TraitSums, double Samples, double BiologicalShareSum, double CaregiverCountSum)
{
    public static RearingRecord Empty { get; } = new(new double[10], 0, 0, 0);

    /// <summary>Uma amostra: <paramref name="caregivers"/> não vazio; <paramref name="biological"/> quantos deles são pai ou mãe.</summary>
    public RearingRecord Add(IReadOnlyList<Personality> caregivers, int biological)
    {
        var sums = new double[10];
        for (int i = 0; i < 10; i++) sums[i] = TraitSums[i];
        foreach (var p in caregivers)
        {
            double w = 1.0 / caregivers.Count;
            sums[0] += p.Extroversion * w; sums[1] += p.Agreeableness * w; sums[2] += p.Conscientiousness * w;
            sums[3] += p.EmotionalStability * w; sums[4] += p.Openness * w; sums[5] += p.Ambition * w;
            sums[6] += p.Loyalty * w; sums[7] += p.Altruism * w; sums[8] += p.Impulsivity * w; sums[9] += p.RiskAversion * w;
        }
        return new RearingRecord(sums, Samples + 1, BiologicalShareSum + (double)biological / caregivers.Count,
            CaregiverCountSum + caregivers.Count);
    }
}
