using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Behavior;

/// <summary>Qualidade de recuperação por tipo de lugar de descanso (Fase 15.1, Stage 4, T12,
/// LWV-03.1). <see cref="GroundEfficiency"/> substitui o papel de
/// <c>NeedsRules.HomelessSleepEfficiency</c> in-place — sem contrato v2 paralelo.</summary>
public enum RestPlaceKind
{
    Ground = 0,
    Dwelling = 1,
    Bed = 2,
}

public sealed record RestPlaceRef(RestPlaceKind Kind, long TargetId, CellCoord Location, double RecoveryEfficiency);

/// <summary>Cama/móvel de descanso no mundo. Chão e moradia não precisam de linha: o chão é o
/// local atual do sem-teto, a moradia é <see cref="Household.Location"/>.</summary>
public sealed class RestPlace(RestPlaceId id, RestPlaceKind kind, CellCoord location, HouseholdId ownerHousehold)
{
    public RestPlaceId Id { get; } = id;
    public RestPlaceKind Kind { get; } = kind;
    public CellCoord Location { get; } = location;
    public HouseholdId OwnerHousehold { get; } = ownerHousehold;
}

public sealed record RestPlaceCatalog(double GroundEfficiency, double DwellingEfficiency, double BedEfficiency)
{
    public static RestPlaceCatalog FromGround(double groundEfficiency) =>
        Create(groundEfficiency, dwellingEfficiency: 1.0, bedEfficiency: 1.0).Value!;

    public static Result<RestPlaceCatalog> Create(
        double groundEfficiency, double dwellingEfficiency, double bedEfficiency)
    {
        if (groundEfficiency is < 0 or > 1)
            return Result<RestPlaceCatalog>.Fail("RestPlaces.Ground: fora de [0,1]");
        if (dwellingEfficiency is < 0 or > 1)
            return Result<RestPlaceCatalog>.Fail("RestPlaces.Dwelling: fora de [0,1]");
        if (bedEfficiency is < 0 or > 1)
            return Result<RestPlaceCatalog>.Fail("RestPlaces.Bed: fora de [0,1]");

        return Result<RestPlaceCatalog>.Ok(new RestPlaceCatalog(groundEfficiency, dwellingEfficiency, bedEfficiency));
    }

    public double EfficiencyOf(RestPlaceKind kind) => kind switch
    {
        RestPlaceKind.Ground => GroundEfficiency,
        RestPlaceKind.Dwelling => DwellingEfficiency,
        RestPlaceKind.Bed => BedEfficiency,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "RestPlaceKind desconhecido"),
    };
}
