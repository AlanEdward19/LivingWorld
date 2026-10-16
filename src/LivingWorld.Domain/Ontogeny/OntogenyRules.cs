using System.Text.Json.Serialization;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Ontogeny;

/// <summary>Regras de ontogenia do cenário (Fase 21): marcos (dado), o que cada ação exige e a curva de progresso.
/// Não é machine learning: curva por marco + janela de idade. O progresso acompanha a idade escalada pelo
/// <c>RateGene</c> do NPC, mas só avança com exposição (convívio com cuidador); janela perdida reduz o teto para sempre (21b). <see cref="Disabled"/> deixa todo mundo completo.</summary>
[JsonConverter(typeof(OntogenyRulesJsonConverter))]
public sealed record OntogenyRules
{
    private static readonly ActionType[] AllActions = Enum.GetValues<ActionType>();

    public bool Enabled { get; }
    public IReadOnlyList<MilestoneDefinition> Milestones { get; }
    public IReadOnlyDictionary<ActionType, MilestoneRequirement> Requirements { get; }

    /// <summary>Fração do atraso (curva da idade − progresso) que a exposição tardia recupera por mês; sempre limitada pelo teto.</summary>
    public double LateRecoveryFactor { get; }

    /// <summary>Fração da distância restante até 1 que continua alcançável depois que uma janela fecha sem o marco (o resto se perde para sempre).</summary>
    public double MissedWindowKeep { get; }

    /// <summary>Fração do progresso perdida por mês de doença (21c); marcos consolidados (janela fechada) formam um piso que não se perde.</summary>
    public double RegressionLoss { get; }

    /// <summary>Exposição que um cuidador sozinho dá, em (0,1] (21d); vários se somam por <c>1 − Π(1 − atenção)</c>. 1,0 = a exposição binária da 21b.</summary>
    public double AttentionPerCaregiver { get; }

    /// <summary>Se a criança sem adulto é acolhida (na orfandade e no sweep mensal). Desligar devolve o comportamento anterior à issue #93: só parente próximo, senão domicílio unitário.</summary>
    public bool OrphanCare { get; }

    private readonly Dictionary<DevelopmentAxis, (double Age, double Progress)[]> _curves;

    /// <summary>Idade (anos, já escalada pela taxa) a partir da qual o desenvolvimento está completo em todos os eixos.</summary>
    public double CompleteAtYears { get; }

    private OntogenyRules(
        bool enabled, IReadOnlyList<MilestoneDefinition> milestones,
        IReadOnlyDictionary<ActionType, MilestoneRequirement> requirements,
        Dictionary<DevelopmentAxis, (double, double)[]> curves, double completeAt,
        double lateRecoveryFactor, double missedWindowKeep, double regressionLoss, double attentionPerCaregiver, bool orphanCare)
    {
        OrphanCare = orphanCare;
        RegressionLoss = regressionLoss;
        AttentionPerCaregiver = attentionPerCaregiver;
        LateRecoveryFactor = lateRecoveryFactor;
        MissedWindowKeep = missedWindowKeep;
        Enabled = enabled;
        Milestones = milestones;
        Requirements = requirements;
        _curves = curves;
        CompleteAtYears = completeAt;
    }

    public static OntogenyRules Disabled { get; } = new(false, [], new Dictionary<ActionType, MilestoneRequirement>(), [], 0, DefaultLateRecovery, DefaultMissedWindowKeep, DefaultRegressionLoss, DefaultAttention, true);

    /// <summary>Valores autorados do cenário padrão (não há medição humana que os fixe): os critérios comparam braços, nunca o número.</summary>
    public const double DefaultLateRecovery = 0.25, DefaultMissedWindowKeep = 0.5, DefaultRegressionLoss = 0.15, DefaultAttention = 1.0;

    public static Result<OntogenyRules> Create(
        bool enabled, IReadOnlyList<MilestoneDefinition> milestones,
        IReadOnlyDictionary<ActionType, MilestoneRequirement> requirements,
        double lateRecoveryFactor = DefaultLateRecovery, double missedWindowKeep = DefaultMissedWindowKeep,
        double regressionLoss = DefaultRegressionLoss, double attentionPerCaregiver = DefaultAttention, bool orphanCare = true)
    {
        if (attentionPerCaregiver is <= 0 or > 1)
            return Result<OntogenyRules>.Fail("Ontogeny.AttentionPerCaregiver: deve estar em (0,1]");
        if (regressionLoss is <= 0 or >= 1)
            return Result<OntogenyRules>.Fail("Ontogeny.RegressionLoss: deve estar em (0,1)");
        if (lateRecoveryFactor is <= 0 or > 1)
            return Result<OntogenyRules>.Fail("Ontogeny.LateRecoveryFactor: deve estar em (0,1]");
        if (missedWindowKeep is <= 0 or >= 1)
            return Result<OntogenyRules>.Fail("Ontogeny.MissedWindowKeep: deve estar em (0,1) — 1 não perderia nada e 0 tiraria tudo");
        foreach (var action in Enum.GetValues<ActionType>())
            if (!requirements.ContainsKey(action))
                return Result<OntogenyRules>.Fail($"Ontogeny.Requirements: falta marco exigido para {action}");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in milestones)
        {
            if (string.IsNullOrWhiteSpace(m.Id) || !ids.Add(m.Id))
                return Result<OntogenyRules>.Fail($"Ontogeny.Milestones: id vazio ou repetido ({m.Id})");
            if (m.Threshold is <= 0 or > 1)
                return Result<OntogenyRules>.Fail($"Ontogeny.Milestones[{m.Id}]: Threshold deve estar em (0,1]");
            if (m.WindowStartYears < 0 || m.WindowMedianYears < m.WindowStartYears || m.WindowLimitYears < m.WindowMedianYears)
                return Result<OntogenyRules>.Fail($"Ontogeny.Milestones[{m.Id}]: janela exige 0 <= início <= mediana <= limite");
        }

        foreach (var m in milestones)
            foreach (var req in m.Requires)
                if (!ids.Contains(req))
                    return Result<OntogenyRules>.Fail($"Ontogeny.Milestones[{m.Id}]: pré-requisito '{req}' não existe");
        if (HasCycle(milestones))
            return Result<OntogenyRules>.Fail("Ontogeny.Milestones: pré-requisitos formam um ciclo");

        foreach (var axis in Enum.GetValues<DevelopmentAxis>())
        {
            var onAxis = milestones.Where(m => m.Axis == axis).OrderBy(m => m.WindowMedianYears).ThenBy(m => m.Id, StringComparer.Ordinal).ToList();
            if (onAxis.Count == 0)
                return Result<OntogenyRules>.Fail($"Ontogeny.Milestones: eixo {axis} sem marco");
            for (int i = 1; i < onAxis.Count; i++)
                if (onAxis[i].Threshold < onAxis[i - 1].Threshold)
                    return Result<OntogenyRules>.Fail($"Ontogeny.Milestones[{onAxis[i].Id}]: Threshold menor que o de um marco anterior no eixo {axis}");
            if (onAxis[^1].Threshold < 1.0)
                return Result<OntogenyRules>.Fail($"Ontogeny.Milestones: o último marco do eixo {axis} deve ter Threshold 1");
        }

        foreach (var (action, req) in requirements)
        {
            if (req.Threshold is < 0 or > 1)
                return Result<OntogenyRules>.Fail($"Ontogeny.Requirements[{action}]: Threshold deve estar em [0,1]");
            if (req.Threshold > 0 && !milestones.Any(m => m.Axis == req.Axis && m.Threshold >= req.Threshold))
                return Result<OntogenyRules>.Fail($"Ontogeny.Requirements[{action}]: nenhum marco do eixo {req.Axis} alcança {req.Threshold}");
        }

        return Result<OntogenyRules>.Ok(Build(enabled, milestones, requirements, lateRecoveryFactor, missedWindowKeep, regressionLoss, attentionPerCaregiver, orphanCare));
    }

    /// <summary>Sem validação: só para ler dado já gravado pelo próprio motor (snapshot). Quem aceita dado externo usa <see cref="Create"/>.</summary>
    internal static OntogenyRules Build(
        bool enabled, IReadOnlyList<MilestoneDefinition> milestones, IReadOnlyDictionary<ActionType, MilestoneRequirement> requirements,
        double lateRecoveryFactor = DefaultLateRecovery, double missedWindowKeep = DefaultMissedWindowKeep,
        double regressionLoss = DefaultRegressionLoss, double attentionPerCaregiver = DefaultAttention, bool orphanCare = true)
    {
        var curves = new Dictionary<DevelopmentAxis, (double, double)[]>();
        double completeAt = 0;
        foreach (var axis in Enum.GetValues<DevelopmentAxis>())
        {
            var onAxis = milestones.Where(m => m.Axis == axis).OrderBy(m => m.WindowMedianYears).ThenBy(m => m.Id, StringComparer.Ordinal).ToList();
            if (onAxis.Count == 0) continue;
            var points = new List<(double, double)> { (0, 0) };
            points.AddRange(onAxis.Select(m => (m.WindowMedianYears, m.Threshold)));
            curves[axis] = points.ToArray();
            completeAt = Math.Max(completeAt, onAxis[^1].WindowMedianYears);
        }
        return new OntogenyRules(enabled, milestones, requirements, curves, completeAt, lateRecoveryFactor, missedWindowKeep, regressionLoss, attentionPerCaregiver, orphanCare);
    }

    /// <summary>Progresso do eixo para uma idade efetiva (anos): interpolação linear entre as medianas dos marcos.</summary>
    public double ProgressAt(DevelopmentAxis axis, double effectiveAgeYears)
    {
        if (!_curves.TryGetValue(axis, out var points)) return 1.0;
        if (effectiveAgeYears <= 0) return 0;
        for (int i = 1; i < points.Length; i++)
        {
            if (effectiveAgeYears > points[i].Age) continue;
            var (a0, p0) = points[i - 1];
            var (a1, p1) = points[i];
            return a1 <= a0 ? p1 : p0 + (p1 - p0) * (effectiveAgeYears - a0) / (a1 - a0);
        }
        return points[^1].Progress;
    }

    public DevelopmentState StateAt(double effectiveAgeYears) => new(
        ProgressAt(DevelopmentAxis.GrossMotor, effectiveAgeYears), ProgressAt(DevelopmentAxis.FineMotor, effectiveAgeYears),
        ProgressAt(DevelopmentAxis.Language, effectiveAgeYears), ProgressAt(DevelopmentAxis.SelfCare, effectiveAgeYears),
        ProgressAt(DevelopmentAxis.SocialCognition, effectiveAgeYears), ProgressAt(DevelopmentAxis.Abstraction, effectiveAgeYears));

    /// <summary>Avança um mês de desenvolvimento (puro, sem sorteio: a variação individual é o <c>RateGene</c>, que já escala as idades).
    /// Com exposição o eixo ganha o que a curva da idade andou mais uma fração do atraso, até o teto; sem exposição não ganha nada.
    /// Janela que fecha com o marco ainda fora do alcance reduz o teto do eixo de forma permanente.</summary>
    public DevelopmentState Advance(
        DevelopmentState state, double previousAgeYears, double currentAgeYears, bool exposed,
        IReadOnlyList<int>? caregiverCultures = null) =>
        Advance(state, previousAgeYears, currentAgeYears, exposed ? 1.0 : 0.0, caregiverCultures);

    /// <summary>Exposição que <paramref name="caregivers"/> adultos dão no mês: <c>1 − Π(1 − atenção)</c>; 0 sem cuidador.</summary>
    public double ExposureFor(int caregivers)
    {
        double unreached = 1.0; // produto simples: a matemática do mundo não usa potência da plataforma (determinismo entre máquinas)
        for (int i = 0; i < caregivers; i++) unreached *= 1.0 - AttentionPerCaregiver;
        return caregivers <= 0 ? 0.0 : 1.0 - unreached;
    }

    /// <summary>Versão graduada (21d): <paramref name="exposure"/> em [0,1] escala o ganho do mês e a recuperação; 1 é a exposição plena.</summary>
    public DevelopmentState Advance(
        DevelopmentState state, double previousAgeYears, double currentAgeYears, double exposure,
        IReadOnlyList<int>? caregiverCultures)
    {
        bool exposed = exposure > 0;
        double languageBefore = state.Language;
        foreach (var axis in Enum.GetValues<DevelopmentAxis>())
        {
            if (!_curves.ContainsKey(axis)) continue;
            double before = ProgressAt(axis, previousAgeYears);
            double after = ProgressAt(axis, currentAgeYears);
            double ceiling = state.CeilingOf(axis);
            double progress = state.Get(axis);

            if (exposed)
                progress = Math.Min(ceiling, progress + exposure * ((after - before) + LateRecoveryFactor * Math.Max(0, before - progress)));

            foreach (var m in Milestones)
            {
                if (m.Axis != axis || m.WindowLimitYears <= previousAgeYears || m.WindowLimitYears > currentAgeYears) continue;
                if (progress + 1e-9 >= m.Threshold) continue;
                ceiling = Math.Min(ceiling, progress + (1.0 - progress) * MissedWindowKeep);
                progress = Math.Min(progress, ceiling);
            }

            state = state.With(axis, progress).WithCeiling(axis, ceiling);
        }

        // O ganho de linguagem do mês se reparte entre os idiomas de quem cria (um voto por cuidador): o idioma vem dos cuidadores, não da etnia.
        double languageGain = state.Language - languageBefore;
        if (exposed && languageGain > 0 && caregiverCultures is { Count: > 0 })
        {
            var fluency = state.Fluency ?? LanguageFluency.Empty;
            foreach (int culture in caregiverCultures)
                fluency = fluency.Add(culture, languageGain / caregiverCultures.Count);
            state = state with { Fluency = fluency };
        }
        return state;
    }

    /// <summary>Um mês de doença: o progresso de cada eixo cai <see cref="RegressionLoss"/>, mas não abaixo do piso — o maior marco já
    /// consolidado (atingido e com a janela fechada pela idade). O que ainda está dentro da janela é o que se perde. Puro.</summary>
    public DevelopmentState Regress(DevelopmentState state, double currentAgeYears)
    {
        foreach (var axis in Enum.GetValues<DevelopmentAxis>())
        {
            double progress = state.Get(axis);
            double floor = Milestones
                .Where(m => m.Axis == axis && m.WindowLimitYears <= currentAgeYears && progress + 1e-9 >= m.Threshold)
                .Select(m => m.Threshold).DefaultIfEmpty(0).Max();
            double lowered = Math.Max(floor, progress * (1.0 - RegressionLoss));
            if (lowered >= progress) continue;
            if (axis == DevelopmentAxis.Language && progress > 0 && state.Fluency is { } fluency)
                state = state with { Fluency = fluency.Scale(lowered / progress) };
            state = state.With(axis, lowered);
        }
        return state;
    }

    /// <summary><c>null</c> (completo) ou ligado sempre elegível; senão compara o progresso do eixo com o limiar da ação.</summary>
    public bool IsEligible(DevelopmentState? state, ActionType action)
    {
        if (!Enabled || state is null) return true;
        if (!Requirements.TryGetValue(action, out var req)) return true;
        return req.Threshold <= 0 || state.Get(req.Axis) >= req.Threshold;
    }

    /// <summary>Bits (1 &lt;&lt; <see cref="ActionType"/>) das ações que o estado ainda não alcançou; 0 se completo ou desligado.</summary>
    public uint BlockedMaskFor(DevelopmentState? state)
    {
        if (!Enabled || state is null) return 0;
        uint mask = 0;
        foreach (var action in AllActions)
            if (!IsEligible(state, action)) mask |= 1u << (int)action;
        return mask;
    }

    private static bool HasCycle(IReadOnlyList<MilestoneDefinition> milestones)
    {
        var byId = milestones.ToDictionary(m => m.Id, StringComparer.Ordinal);
        var state = new Dictionary<string, int>(StringComparer.Ordinal); // 1 = visitando, 2 = pronto
        bool Visit(string id)
        {
            if (state.TryGetValue(id, out int s)) return s == 1;
            state[id] = 1;
            foreach (var req in byId[id].Requires)
                if (Visit(req)) return true;
            state[id] = 2;
            return false;
        }
        return milestones.Any(m => Visit(m.Id));
    }
}
