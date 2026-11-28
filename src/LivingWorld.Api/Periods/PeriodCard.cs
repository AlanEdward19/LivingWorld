using System.Text.Json.Nodes;
using LivingWorld.Simulation.Periods;

namespace LivingWorld.Api.Periods;

/// <summary>AD-175: o que o menu mostra de um período antes de escolhê-lo. <see cref="Name"/>, <see cref="Era"/>,
/// <see cref="Description"/> e <see cref="StartingTechnology"/> vêm do bloco opcional <c>Presentation</c> declarado no
/// próprio período (texto vazio quando o período não declara; o nome cai na fonte do cadastro); <c>Presentation.QuickStart</c> (#252) diz que o menu
/// oferece o período num botão que cria e abre o mundo direto, sem o assistente; os números saem da
/// definição validada, do mesmo jeito que <c>ScenarioLoaderV2</c> monta o mundo. Nenhum motor lê este bloco.</summary>
public sealed record PeriodCard(
    string Name,
    string Era,
    string Description,
    string StartingTechnology,
    int InitialPopulation,
    int SettlementCount,
    int ProfessionCount,
    IReadOnlyList<string> Professions,
    bool InventionEnabled,
    bool QuickStart = false);

public static class PeriodCards
{
    /// <summary>Cartão do período; um payload que não valida mais ainda tem nome, época e descrição, com os números zerados.</summary>
    public static PeriodCard From(string periodId, string source, string payloadJson)
    {
        var root = JsonNode.Parse(payloadJson)?.AsObject();
        var presentation = root?["Presentation"] as JsonObject;
        string name = Text(presentation, "Name") is { Length: > 0 } declared ? declared
            : !string.IsNullOrWhiteSpace(source) ? source : periodId;
        string era = Text(presentation, "Era");
        string description = Text(presentation, "Description");
        string technology = Text(presentation, "Technology");
        bool invention = root?["Invention"]?["Enabled"]?.GetValue<bool>() != false;
        bool quick = presentation?["QuickStart"] is JsonValue q && q.TryGetValue<bool>(out bool quickValue) && quickValue;

        var validation = PeriodDefinitionValidator.Validate(payloadJson);
        if (!validation.IsSuccess)
            return new(name, era, description, technology, 0, 0, 0, [], invention, quick);

        var definition = validation.Value!;
        var catalog = PeriodCatalog.From(definition);
        int authored = (root?["AuthoredHouseholds"] as JsonArray)?.Sum(f => (f?["Members"] as JsonArray)?.Count ?? 0) ?? 0; // #252: famílias autoradas
        int population = (int)Math.Min(int.MaxValue, definition.Population.InitialPopulation + authored);
        // Mesma regra do loader: as cidades autoradas, mais a vila inicial fundada quando nenhuma está na célula dela.
        int settlements = definition.City.Cities.Count;
        if (population > 0 && definition.City.Cities.All(c => c.Location != definition.Population.Village)) settlements++;
        var professions = catalog.ProfessionIds
            .Where(catalog.ProfessionNames.ContainsKey)
            .Select(id => catalog.ProfessionNames[id])
            .ToList();
        return new(name, era, description, technology, population, settlements, catalog.ProfessionIds.Count, professions, invention, quick);
    }

    /// <summary>Só o nome legível (para listas que não precisam do cartão inteiro, como <c>GET /worlds</c>).</summary>
    public static string NameOf(string periodId, string source, string payloadJson)
    {
        var presentation = JsonNode.Parse(payloadJson)?["Presentation"] as JsonObject;
        return Text(presentation, "Name") is { Length: > 0 } declared ? declared
            : !string.IsNullOrWhiteSpace(source) ? source : periodId;
    }

    private static string Text(JsonObject? block, string key) =>
        block?[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text.Trim() : "";
}
