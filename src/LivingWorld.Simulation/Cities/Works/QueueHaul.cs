using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Cities.Works;

/// <summary>AD-195: a obra da fila da cidade (prédio-N) com material de verdade: a turma busca no largo (o estoque da cidade) o insumo
/// da receita, carga por carga nas mãos, e o passo da receita só consome o que está no lote (<see cref="ConstructionProject.MaterialOnSite"/>).
/// Sem o insumo no estoque e nada no lote, trava com evento; o tempo ruim perde o dia com evento, uma vez por episódio.</summary>
internal static class QueueHaul
{
    public static bool Walking(WorldState world) =>
        world.StreetRules.HaulersWalk && LivingWorld.Simulation.Cities.Construction.ConstructionSystem.NeedsCrew(world);

    private static string TypeOf(ConstructionProject project) => $"predio-{project.BuildingTypeId}";

    public static CellCoord? TargetOf(City city, ConstructionProject project, Npc npc)
    {
        if (project.Hauls is not { Count: > 0 } hauls) return null;
        foreach (var trip in hauls)
            if (trip.Npc == npc.Id.Value) return trip.Stage == 0 ? city.Location : new CellCoord(project.SiteX, project.SiteY);
        return null;
    }

    public static bool AtSource(City city, ConstructionProject project, Npc npc)
    {
        if (project.Hauls is not { Count: > 0 } hauls) return false;
        foreach (var trip in hauls)
            if (trip.Npc == npc.Id.Value) return trip.Stage == 0 && npc.CurrentLocation == city.Location;
        return false;
    }

    /// <summary>Uma hora da obra da fila: as viagens andam e, se o lote não tem o insumo de um dia da turma, mais alguém vai ao largo.</summary>
    public static void Tick(WorldState world, TickContext ctx, City city, ConstructionProject project, BuildingRecipe recipe)
    {
        if (project.WorkId is not { } id || project.SiteX < 0 || project.TicksRemaining <= 0) return;
        var site = new CellCoord(project.SiteX, project.SiteY);
        if (project.Hauls is { Count: > 0 } hauls)
            for (int i = hauls.Count - 1; i >= 0; i--)
            {
                var trip = hauls[i];
                var npc = world.FindNpc(new NpcId(trip.Npc));
                if (npc is not { IsAlive: true } || !project.Crew.Contains(trip.Npc))
                {
                    if (npc is { IsAlive: true } && trip.Stage == 1 && npc.CarriedResourceId == trip.Resource) // a carga volta ao largo
                        city.DepositStock(new ResourceType(trip.Resource), npc.TakeCarried().Quantity);
                    hauls.RemoveAt(i);
                    continue;
                }
                if (trip.Stage == 0 && npc.CurrentLocation == city.Location)
                {
                    var resource = new ResourceType(trip.Resource);
                    long units = Math.Min(trip.Units, city.Stock.GetValueOrDefault(resource));
                    if (units <= 0 || !city.WithdrawStock(resource, units).IsSuccess)
                    {
                        hauls.RemoveAt(i);
                        continue;
                    }
                    if (!npc.PickUp(resource, units, Math.Max(units, npc.CarryCapacity)).IsSuccess)
                    {
                        city.DepositStock(resource, units);
                        hauls.RemoveAt(i);
                        continue;
                    }
                    trip.Units = units;
                    trip.Stage = 1;
                }
                else if (trip.Stage == 1 && npc.CurrentLocation == site)
                {
                    var (res, units) = npc.TakeCarried();
                    hauls.RemoveAt(i);
                    if (res != trip.Resource || units <= 0) continue;
                    project.MaterialOnSite ??= [];
                    project.MaterialOnSite[res] = project.MaterialOnSite.GetValueOrDefault(res) + units;
                    WorkEvents.Log(ctx, WorldEventKind.WorkMaterialArrived, id, city.Id.Value, TypeOf(project), site.X, site.Y,
                        $"{res}|{units}|{npc.Id.Value}|largo", cause: project.StartEventId);
                    if (project.MaterialShort == true)
                    {
                        project.MaterialShort = null;
                        WorkEvents.Log(ctx, WorldEventKind.WorkResumed, id, city.Id.Value, TypeOf(project), site.X, site.Y, "0|material",
                            cause: project.StartEventId);
                    }
                }
            }
        if (project.Hauls is { Count: 0 }) project.Hauls = null;
        Assign(world, ctx, city, project, recipe, id, site);
    }

    private static void Assign(WorldState world, TickContext ctx, City city, ConstructionProject project, BuildingRecipe recipe, long id, CellCoord site)
    {
        if (project.Crew.Count == 0) return;
        long ticks = Math.Max(1, recipe.TicksToBuild);
        long dayHours = (long)world.StreetRules.CrewHoursPerDay * project.Crew.Count;
        int? want = null;
        long wantUnits = 0, worst = long.MaxValue;
        bool lacking = false;
        foreach (var (resource, total) in recipe.Inputs.OrderBy(kv => kv.Key.Id))
        {
            long placed = project.Consumed.GetValueOrDefault(resource);
            long onSite = project.MaterialOnSite?.GetValueOrDefault(resource.Id) ?? 0;
            long coming = 0;
            foreach (var trip in project.Hauls ?? []) if (trip.Resource == resource.Id) coming += trip.Units;
            long missing = total - placed - onSite - coming;
            if (missing <= 0) continue;
            long day = Math.Min(total - placed, (total * dayHours + ticks - 1) / ticks);
            if (onSite >= Math.Max(1, day)) continue; // o que vem a caminho ainda não serve à parede
            if (city.Stock.GetValueOrDefault(resource) <= 0) { lacking |= onSite <= 0 && coming <= 0; continue; } // o que vem a caminho não falta
            long slack = onSite + coming - day; // o mais atrasado primeiro
            if (slack < worst) { worst = slack; want = resource.Id; wantUnits = missing; }
        }
        if (want is null)
        {
            if (lacking && project.MaterialShort != true)
            {
                project.MaterialShort = true;
                WorkEvents.Log(ctx, WorldEventKind.WorkMaterialShort, id, city.Id.Value, TypeOf(project), site.X, site.Y,
                    "estoque-da-cidade|0", cause: project.StartEventId);
            }
            return;
        }
        foreach (long npcId in project.Crew)
        {
            bool busy = false;
            foreach (var trip in project.Hauls ?? []) if (trip.Npc == npcId) { busy = true; break; }
            if (busy || world.FindNpc(new NpcId(npcId)) is not { IsAlive: true, IsCarrying: false } npc) continue;
            long units = Math.Min(wantUnits, WorkHaul.CapacityUnits(world, npc, new ResourceType(want.Value)));
            (project.Hauls ??= []).Add(new ConstructionHaul { Npc = npcId, Resource = want.Value, Units = units });
            LivingWorld.Simulation.Cities.Streets.SiteWork.Touch(world);
            return;
        }
    }

    /// <summary>O tempo do dia na obra da fila: perdido (uma vez por episódio) e a volta.</summary>
    public static void WeatherDay(TickContext ctx, City city, ConstructionProject project, bool workable, string reason)
    {
        if (project.WorkId is not { } id || project.SiteX < 0) return;
        if (!workable && project.WeatherLost != true && project.Crew.Count > 0)
        {
            project.WeatherLost = true;
            WorkEvents.Log(ctx, WorldEventKind.WorkWeatherLost, id, city.Id.Value, TypeOf(project), project.SiteX, project.SiteY, reason,
                cause: project.StartEventId);
        }
        else if (workable && project.WeatherLost == true)
        {
            project.WeatherLost = null;
            WorkEvents.Log(ctx, WorldEventKind.WorkResumed, id, city.Id.Value, TypeOf(project), project.SiteX, project.SiteY, "0|tempo",
                cause: project.StartEventId);
        }
    }
}
