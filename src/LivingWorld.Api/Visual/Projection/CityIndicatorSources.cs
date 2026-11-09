using LivingWorld.Simulation.Cities.Queries;

namespace LivingWorld.Api.Visual.Projection;

/// <summary>De onde vem cada indicador de <see cref="CityIndicators"/>. Indicador sem entrada aqui não
/// pode existir: <c>CityIndicatorSourcesTests</c> reprova por reflexão o campo sem fonte declarada, o que
/// impede de voltar a exibir um número que não mede o que o nome diz (AD-121).</summary>
public static class CityIndicatorSources
{
    /// <summary>Medido: <see cref="Query"/> é o método de <see cref="CityPopulationQuery"/> que o computa.
    /// Não medido: <see cref="Query"/> é nulo e <see cref="PendingIssue"/> diz que issue dará a fonte.</summary>
    public sealed record Source(string? Query, string? PendingIssue)
    {
        public bool IsMeasured => Query is not null;
    }

    public static readonly IReadOnlyDictionary<string, Source> ByIndicator = new Dictionary<string, Source>
    {
        [nameof(CityIndicators.Population)] = Measured(nameof(CityPopulationQuery.Population)),
        [nameof(CityIndicators.Wealth)] = Measured(nameof(CityPopulationQuery.Wealth)),
        [nameof(CityIndicators.Health)] = Measured(nameof(CityPopulationQuery.Health)),
        [nameof(CityIndicators.Inequality)] = Measured(nameof(CityPopulationQuery.Inequality)),
        [nameof(CityIndicators.Economy)] = Measured(nameof(CityPopulationQuery.Economy)),
        [nameof(CityIndicators.Housing)] = Measured(nameof(CityPopulationQuery.Housing)),
        [nameof(CityIndicators.Security)] = NotMeasured("#118"),
        [nameof(CityIndicators.Education)] = NotMeasured("#122"),
        [nameof(CityIndicators.Infrastructure)] = NotMeasured("#124"),
    };

    private static Source Measured(string query) => new(query, null);
    private static Source NotMeasured(string issue) => new(null, issue);
}
