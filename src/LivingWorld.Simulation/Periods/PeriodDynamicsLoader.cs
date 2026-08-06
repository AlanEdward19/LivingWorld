using System.Text.Json.Nodes;
using LivingWorld.Domain;

namespace LivingWorld.Simulation.Periods;

/// <summary>Tipo de regra de transformação de profissão (Fase 13, T1): nascimento, fusão, divisão
/// ou desaparecimento em runtime — nunca literal de período em <c>src/</c>, só o dado do
/// cenário decide quando/quais.</summary>
public enum PeriodTransformationKind { Emerge, Merge, Split, Disappear }

/// <summary>Peso inicial de uma profissão no startpoint do período (PERIOD-01). Substitui o
/// sorteio uniforme de <see cref="PopulationCatalog.RollProfession"/> quando o período declara
/// viés — o motor só vê o id, nunca um nome (mesmo contrato de <see cref="ProfessionType"/>).</summary>
public sealed record ProfessionBias(int ProfessionId, double Weight);

/// <summary>Peso inicial de uma habilidade no startpoint do período (PERIOD-01/PERIOD-19). Mesmo
/// contrato de <see cref="ProfessionBias"/> — id inteiro aberto, nenhum nome fechado no motor
/// (Fase 13, T11a). <see cref="SkillType"/> em <c>src/</c> continua o enum fechado de 13 valores
/// da Fase 6 até T11b abrir o catálogo de verdade (ver tasks.md) — este bloco só solta a
/// exigência de nome no contrato de entrada do período, ainda não aplica o viés em runtime.</summary>
public sealed record SkillBias(int SkillId, double Weight);

/// <summary>Regra declarada de evolução de profissão em runtime (PERIOD-02/03): cardinalidade de
/// origem/destino varia por <see cref="Kind"/> e é validada em <see cref="PeriodDynamicsLoader"/>.
/// SPEC_DEVIATION (Fase 13, T1): habilidades não entram aqui — <see cref="SkillType"/> é enum
/// fechado do motor (Fase 6); transformação dinâmica de habilidade exigiria abrir esse catálogo,
/// fora do escopo de T1. Ver spec.md Success Criteria — reavaliar em fase futura se necessário.</summary>
public sealed record PeriodTransformationRule(
    PeriodTransformationKind Kind,
    IReadOnlyList<int> SourceProfessionIds,
    IReadOnlyList<int> TargetProfessionIds,
    long? TriggerTick);

/// <summary>Startpoint dinâmico de um período (PERIOD-01..03): vieses de profissão/habilidade +
/// regras de transformação. Bloco <c>Dynamics</c> é opcional no cenário — ausência equivale a
/// "sem viés declarado, sem evolução de conteúdo", mesmo padrão optional-com-default-vazio de
/// <see cref="PopulationScenarioLoader"/> pra conjuntos de id.</summary>
public sealed record PeriodDynamicsData(
    IReadOnlyList<ProfessionBias> ProfessionBiases,
    IReadOnlyList<SkillBias> SkillBiases,
    IReadOnlyList<PeriodTransformationRule> TransformationRules)
{
    public static readonly PeriodDynamicsData Empty = new([], [], []);
}

/// <summary>Carrega o bloco <c>Dynamics</c> de um cenário (Fase 13, T1): mesmo padrão manual-parse
/// + <see cref="Result{T}"/> de <see cref="BehaviorScenarioLoader"/> — campo obrigatório ausente
/// ou regra estruturalmente inválida nomeia o campo/índice no erro. Checks referenciais entre
/// <see cref="PeriodDynamicsData"/> e os demais catálogos do período (ex.: id de profissão citado
/// numa regra mas ausente do <see cref="PopulationCatalog"/>) ficam para
/// <c>PeriodDefinitionValidator</c> (T2) — este loader só valida a forma do próprio bloco.</summary>
public static class PeriodDynamicsLoader
{
    public static Result<PeriodDynamicsData> Load(string json)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(json)?.AsObject() ?? throw new System.Text.Json.JsonException("corpo vazio");
        }
        catch (System.Text.Json.JsonException ex)
        {
            return Result<PeriodDynamicsData>.Fail($"json: {ex.Message}");
        }

        if (root["Dynamics"] is null)
            return Result<PeriodDynamicsData>.Ok(PeriodDynamicsData.Empty);

        if (root["Dynamics"] is not JsonObject dynamics)
            return Result<PeriodDynamicsData>.Fail("Dynamics: campo inválido");

        var biasesResult = ParseProfessionBiases(dynamics);
        if (!biasesResult.IsSuccess)
            return Result<PeriodDynamicsData>.Fail(biasesResult.Error!);

        var skillBiasesResult = ParseSkillBiases(dynamics);
        if (!skillBiasesResult.IsSuccess)
            return Result<PeriodDynamicsData>.Fail(skillBiasesResult.Error!);

        var rulesResult = ParseTransformationRules(dynamics);
        if (!rulesResult.IsSuccess)
            return Result<PeriodDynamicsData>.Fail(rulesResult.Error!);

        return Result<PeriodDynamicsData>.Ok(new PeriodDynamicsData(
            biasesResult.Value!, skillBiasesResult.Value!, rulesResult.Value!));
    }

    private static Result<IReadOnlyList<ProfessionBias>> ParseProfessionBiases(JsonObject dynamics)
    {
        if (dynamics["ProfessionBiases"] is null)
            return Result<IReadOnlyList<ProfessionBias>>.Ok([]);

        if (dynamics["ProfessionBiases"] is not JsonArray array)
            return Result<IReadOnlyList<ProfessionBias>>.Fail("Dynamics.ProfessionBiases: campo inválido");

        var biases = new List<ProfessionBias>();
        foreach (var node in array)
        {
            if (node is not JsonObject item)
                return Result<IReadOnlyList<ProfessionBias>>.Fail("Dynamics.ProfessionBiases[]: item inválido");
            if (!TryGetInt(item, "ProfessionId", out var professionId))
                return Result<IReadOnlyList<ProfessionBias>>.Fail("Dynamics.ProfessionBiases[].ProfessionId: campo obrigatório ausente ou inválido");
            if (!TryGetDouble(item, "Weight", out var weight))
                return Result<IReadOnlyList<ProfessionBias>>.Fail("Dynamics.ProfessionBiases[].Weight: campo obrigatório ausente ou inválido");
            if (weight <= 0)
                return Result<IReadOnlyList<ProfessionBias>>.Fail("Dynamics.ProfessionBiases[].Weight: deve ser maior que zero");

            biases.Add(new ProfessionBias(professionId, weight));
        }

        return Result<IReadOnlyList<ProfessionBias>>.Ok(biases);
    }

    private static Result<IReadOnlyList<SkillBias>> ParseSkillBiases(JsonObject dynamics)
    {
        if (dynamics["SkillBiases"] is null)
            return Result<IReadOnlyList<SkillBias>>.Ok([]);

        if (dynamics["SkillBiases"] is not JsonArray array)
            return Result<IReadOnlyList<SkillBias>>.Fail("Dynamics.SkillBiases: campo inválido");

        var biases = new List<SkillBias>();
        foreach (var node in array)
        {
            if (node is not JsonObject item)
                return Result<IReadOnlyList<SkillBias>>.Fail("Dynamics.SkillBiases[]: item inválido");
            if (!TryGetInt(item, "SkillId", out var skillId))
                return Result<IReadOnlyList<SkillBias>>.Fail("Dynamics.SkillBiases[].SkillId: campo obrigatório ausente ou inválido");
            if (!TryGetDouble(item, "Weight", out var weight))
                return Result<IReadOnlyList<SkillBias>>.Fail("Dynamics.SkillBiases[].Weight: campo obrigatório ausente ou inválido");
            if (weight <= 0)
                return Result<IReadOnlyList<SkillBias>>.Fail("Dynamics.SkillBiases[].Weight: deve ser maior que zero");

            biases.Add(new SkillBias(skillId, weight));
        }

        return Result<IReadOnlyList<SkillBias>>.Ok(biases);
    }

    private static Result<IReadOnlyList<PeriodTransformationRule>> ParseTransformationRules(JsonObject dynamics)
    {
        if (dynamics["TransformationRules"] is null)
            return Result<IReadOnlyList<PeriodTransformationRule>>.Ok([]);

        if (dynamics["TransformationRules"] is not JsonArray array)
            return Result<IReadOnlyList<PeriodTransformationRule>>.Fail("Dynamics.TransformationRules: campo inválido");

        var rules = new List<PeriodTransformationRule>();
        foreach (var node in array)
        {
            if (node is not JsonObject item)
                return Result<IReadOnlyList<PeriodTransformationRule>>.Fail("Dynamics.TransformationRules[]: item inválido");
            if (item["Kind"] is not JsonValue kindNode || !kindNode.TryGetValue<string>(out var kindText)
                || !Enum.TryParse<PeriodTransformationKind>(kindText, out var kind))
                return Result<IReadOnlyList<PeriodTransformationRule>>.Fail("Dynamics.TransformationRules[].Kind: campo obrigatório ausente ou inválido");

            if (!TryGetIntArray(item, "SourceProfessionIds", out var sources))
                return Result<IReadOnlyList<PeriodTransformationRule>>.Fail("Dynamics.TransformationRules[].SourceProfessionIds: campo inválido");
            if (!TryGetIntArray(item, "TargetProfessionIds", out var targets))
                return Result<IReadOnlyList<PeriodTransformationRule>>.Fail("Dynamics.TransformationRules[].TargetProfessionIds: campo inválido");

            long? triggerTick = null;
            if (item["TriggerTick"] is JsonValue triggerNode)
            {
                if (!triggerNode.TryGetValue<long>(out var trigger))
                    return Result<IReadOnlyList<PeriodTransformationRule>>.Fail("Dynamics.TransformationRules[].TriggerTick: valor inválido");
                if (trigger < 0)
                    return Result<IReadOnlyList<PeriodTransformationRule>>.Fail("Dynamics.TransformationRules[].TriggerTick: deve ser maior ou igual a zero");
                triggerTick = trigger;
            }

            var cardinalityError = ValidateCardinality(kind, sources, targets);
            if (cardinalityError is not null)
                return Result<IReadOnlyList<PeriodTransformationRule>>.Fail(cardinalityError);

            rules.Add(new PeriodTransformationRule(kind, sources, targets, triggerTick));
        }

        return Result<IReadOnlyList<PeriodTransformationRule>>.Ok(rules);
    }

    private static string? ValidateCardinality(PeriodTransformationKind kind, IReadOnlyList<int> sources, IReadOnlyList<int> targets) => kind switch
    {
        PeriodTransformationKind.Emerge when sources.Count != 0 || targets.Count != 1 =>
            "Dynamics.TransformationRules[]: Emerge exige SourceProfessionIds vazio e exatamente 1 TargetProfessionIds",
        PeriodTransformationKind.Disappear when targets.Count != 0 || sources.Count != 1 =>
            "Dynamics.TransformationRules[]: Disappear exige TargetProfessionIds vazio e exatamente 1 SourceProfessionIds",
        PeriodTransformationKind.Merge when sources.Count < 2 || targets.Count != 1 =>
            "Dynamics.TransformationRules[]: Merge exige 2+ SourceProfessionIds e exatamente 1 TargetProfessionIds",
        PeriodTransformationKind.Split when sources.Count != 1 || targets.Count < 2 =>
            "Dynamics.TransformationRules[]: Split exige exatamente 1 SourceProfessionIds e 2+ TargetProfessionIds",
        _ => null,
    };

    private static bool TryGetInt(JsonObject root, string field, out int value)
    {
        value = 0;
        return root[field] is JsonValue v && v.TryGetValue(out value);
    }

    private static bool TryGetDouble(JsonObject root, string field, out double value)
    {
        value = 0;
        return root[field] is JsonValue v && v.TryGetValue(out value);
    }

    private static bool TryGetIntArray(JsonObject root, string field, out List<int> value)
    {
        value = [];
        if (root[field] is null) return true;
        if (root[field] is not JsonArray array) return false;
        foreach (var node in array)
        {
            if (node is not JsonValue v || !v.TryGetValue<int>(out var id)) return false;
            value.Add(id);
        }
        return true;
    }
}
