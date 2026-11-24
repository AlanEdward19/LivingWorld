using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Periods;

namespace LivingWorld.Api.Visual.Observable;

/// <summary>Nomes legíveis do período do mundo (ofício, tipo de prédio, tipo de local, recurso), os mesmos de
/// <c>/periods/{id}/catalog</c>. O motor decide só por id; id sem nome declarado no período fica sem nome (nulo).</summary>
public sealed record PresentationNames(
    IReadOnlyDictionary<int, string> Professions,
    IReadOnlyDictionary<int, string> BuildingTypes,
    IReadOnlyDictionary<int, string> LocationTypes,
    IReadOnlyDictionary<int, string> Resources)
{
    public static PresentationNames None { get; } = new(
        new Dictionary<int, string>(), new Dictionary<int, string>(), new Dictionary<int, string>(), new Dictionary<int, string>());

    public static PresentationNames From(PeriodCatalog catalog) => new(
        catalog.ProfessionNames,
        catalog.Descriptors.BuildingType.ToDictionary(d => d.Id, d => d.Name),
        catalog.Descriptors.LocationType.ToDictionary(d => d.Id, d => d.Name),
        catalog.Descriptors.Resource.ToDictionary(d => d.Id, d => d.Name));

    public string? Profession(int id) => Professions.GetValueOrDefault(id);
    public string? BuildingType(int id) => BuildingTypes.GetValueOrDefault(id);
    public string? LocationType(int id) => LocationTypes.GetValueOrDefault(id);
    public string? Resource(int id) => Resources.GetValueOrDefault(id);
}

/// <summary>Resolve os nomes do período do mundo uma vez por período (lido do repositório de templates) e guarda; mundo sem período
/// ou com template inválido fica com <see cref="PresentationNames.None"/>.</summary>
public sealed class PresentationNameResolver(IServiceScopeFactory scopes)
{
    private readonly object _gate = new();
    private string? _periodId;
    private PresentationNames _names = PresentationNames.None;

    public PresentationNames For(string periodId)
    {
        lock (_gate)
        {
            if (_periodId == periodId) return _names;
            _names = Load(periodId);
            _periodId = periodId;
            return _names;
        }
    }

    private PresentationNames Load(string periodId)
    {
        if (string.IsNullOrEmpty(periodId)) return PresentationNames.None;
        using var scope = scopes.CreateScope();
        var repository = scope.ServiceProvider.GetService<IPeriodTemplateRepository>();
        var template = repository?.FindLatestVersion(periodId);
        if (template is null) return PresentationNames.None;
        var validation = PeriodDefinitionValidator.Validate(template.PayloadJson);
        return validation.IsSuccess ? PresentationNames.From(PeriodCatalog.From(validation.Value!)) : PresentationNames.None;
    }
}
