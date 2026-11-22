using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Cities.Works;

/// <summary>Obra em curso para o cliente (AD-184): id, o que levanta (casa, oficina, predio-N, calcamento), a célula, quanto já foi
/// feito (0–1), se está parada, quantos diaristas e ajudantes, a oferta de jornal (milésimos do jornal do dia) e desde quando.</summary>
public sealed record WorkView(long Id, string Kind, CellCoord Location, double Progress, bool Stalled, int Crew, int Helpers, int OfferPm, long StartedTick)
{
    /// <summary>A obra do canteiro (prédio com obra em curso), ou nula se ele está pronto.</summary>
    public static WorkView? Of(WorldState world, Building building)
    {
        if (building.UnderWork is not { } id || world.Ground is not { } ground) return null;
        foreach (var work in ground.Builds)
            if (work.Id == id) return Of(work);
        foreach (var city in world.ActiveCities())
            foreach (var project in city.ConstructionQueue)
                if (project.WorkId == id && world.CityCatalog.BuildingRecipes.TryGetValue(project.BuildingTypeId, out var recipe))
                    return new WorkView(id, $"predio-{project.BuildingTypeId}", new CellCoord(project.SiteX, project.SiteY),
                        Math.Clamp(1.0 - project.TicksRemaining / (double)Math.Max(1, recipe.TicksToBuild), 0, 1), project.Stalled == true,
                        project.Crew.Count, 0, project.OfferPm ?? 1000, building.CompletedAtTick);
        return null;
    }

    public static WorkView Of(BuildWork work) => new(work.Id, BuildWorks.TypeOf(work), new CellCoord(work.X, work.Y),
        Math.Clamp(1.0 - Math.Max(0, work.HoursLeft) / (double)Math.Max(1, work.HoursTotal), 0, 1), work.Stalled, work.Crew.Count,
        work.Helpers.Count, work.OfferPm, work.StartTick);

    /// <summary>As obras encomendadas em curso de uma cidade, por id.</summary>
    public static IEnumerable<WorkView> InCity(WorldState world, LivingWorld.Domain.Shared.CityId city)
    {
        if (world.Ground is not { } ground) yield break;
        foreach (var work in ground.Builds.OrderBy(w => w.Id))
            if (work.City == city.Value) yield return Of(work);
    }
}
