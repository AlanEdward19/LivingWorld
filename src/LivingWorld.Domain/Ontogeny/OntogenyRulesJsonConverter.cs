using System.Text.Json;
using System.Text.Json.Serialization;
using LivingWorld.Domain.Behavior;

namespace LivingWorld.Domain.Ontogeny;

/// <summary>Grava só o dado (Enabled, Milestones, Requirements); a curva é recalculada ao ler, então o snapshot nunca carrega estado
/// derivado. O dado externo (cenário) é validado por <see cref="OntogenyRules.Create"/> no carregador, não aqui.</summary>
public sealed class OntogenyRulesJsonConverter : JsonConverter<OntogenyRules>
{
    private sealed record Dto(
        bool Enabled, List<MilestoneDefinition> Milestones, Dictionary<ActionType, MilestoneRequirement> Requirements);

    public override OntogenyRules Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dto = JsonSerializer.Deserialize<Dto>(ref reader, options)
            ?? throw new JsonException("OntogenyRules: corpo vazio");
        return OntogenyRules.Build(dto.Enabled, dto.Milestones, dto.Requirements);
    }

    public override void Write(Utf8JsonWriter writer, OntogenyRules value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, new Dto(
            value.Enabled, value.Milestones.ToList(),
            value.Requirements.OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key, kv => kv.Value)), options);
}
