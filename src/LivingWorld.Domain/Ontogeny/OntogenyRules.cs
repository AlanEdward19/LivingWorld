using System.Text.Json.Serialization;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Ontogeny;

/// <summary>Regras de ontogenia do cenário (Fase 21): marcos (dado), o que cada ação exige e a curva de progresso.
/// Não é machine learning: curva por marco + janela de idade. Na 21a o progresso acompanha a idade escalada pelo
/// <c>RateGene</c> do NPC; a 21b troca isso por exposição. <see cref="Disabled"/> deixa todo mundo completo.</summary>
[JsonConverter(typeof(OntogenyRulesJsonConverter))]
public sealed record OntogenyRules
{
    private static readonly ActionType[] AllActions = Enum.GetValues<ActionType>();

    public bool Enabled { get; }
    public IReadOnlyList<MilestoneDefinition> Milestones { get; }
    public IReadOnlyDictionary<ActionType, MilestoneRequirement> Requirements { get; }

    private readonly Dictionary<DevelopmentAxis, (double Age, double Progress)[]> _curves;

    /// <summary>Idade (anos, já escalada pela taxa) a partir da qual o desenvolvimento está completo em todos os eixos.</summary>
    public double CompleteAtYears { get; }

    private OntogenyRules(
        bool enabled, IReadOnlyList<MilestoneDefinition> milestones,
        IReadOnlyDictionary<ActionType, MilestoneRequirement> requirements,
        Dictionary<DevelopmentAxis, (double, double)[]> curves, double completeAt)
    {
        Enabled = enabled;
        Milestones = milestones;
        Requirements = requirements;
        _curves = curves;
        CompleteAtYears = completeAt;
    }

    public static OntogenyRules Disabled { get; } = new(false, [], new Dictionary<ActionType, MilestoneRequirement>(), [], 0);

    public static Result<OntogenyRules> Create(
        bool enabled, IReadOnlyList<MilestoneDefinition> milestones,
        IReadOnlyDictionary<ActionType, MilestoneRequirement> requirements)
    {
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

        return Result<OntogenyRules>.Ok(Build(enabled, milestones, requirements));
    }

    /// <summary>Sem validação: só para ler dado já gravado pelo próprio motor (snapshot). Quem aceita dado externo usa <see cref="Create"/>.</summary>
    internal static OntogenyRules Build(
        bool enabled, IReadOnlyList<MilestoneDefinition> milestones, IReadOnlyDictionary<ActionType, MilestoneRequirement> requirements)
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
        return new OntogenyRules(enabled, milestones, requirements, curves, completeAt);
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
