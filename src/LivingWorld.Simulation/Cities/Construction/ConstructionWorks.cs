using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Works;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Cities.Construction;

/// <summary>Eventos de obra do prédio da fila da cidade (AD-184): parada (sem hora de turma por <c>StallDays</c>), retomada e fim,
/// todos com o id da obra e a célula do lote.</summary>
internal static class ConstructionWorks
{
    private static string TypeOf(ConstructionProject project) => $"predio-{project.BuildingTypeId}";

    internal static void CheckStall(WorldState world, TickContext ctx, City city, ConstructionProject project)
    {
        if (project.WorkId is not { } id || project.Stalled == true || project.LastProgressTick is not { } last) return;
        long idle = ctx.CurrentTick - last;
        if (idle <= world.StreetRules.StallDays * (long)world.Calendar.HoursPerDay) return;
        project.Stalled = true;
        WorkEvents.Log(ctx, WorldEventKind.WorkStalled, id, city.Id.Value, TypeOf(project), project.SiteX, project.SiteY,
            (idle / world.Calendar.HoursPerDay).ToString(System.Globalization.CultureInfo.InvariantCulture), cause: project.StartEventId);
    }

    internal static bool Abandoned(WorldState world, TickContext ctx, City city, ConstructionProject project)
    {
        if (project.LastProgressTick is not { } last) return false;
        long id = project.WorkId ?? -1;
        long idle = ctx.CurrentTick - last;
        if (idle <= world.StreetRules.SiteStaleMonths * world.Calendar.HoursPerMonth) return false;
        if (project.Canteiro is { } b && world.FindBuilding(new BuildingId(b)) is { UnderWork: not null } canteiro) world.RemoveBuilding(canteiro);
        foreach (long npc in project.Crew)
            ctx.LogEvent(WorldEventKind.SiteWorkLeft, $"{npc}|{id}|desfeita", sourceSystem: LivingWorld.Simulation.Cities.Streets.SiteWork.SourceSystem);
        project.Crew.Clear();
        ReturnLeftover(city, project); // AD-195: o material que estava no lote volta ao largo
        city.RemoveConstructionProject(project);
        LivingWorld.Simulation.Cities.Streets.SiteWork.Touch(world);
        WorkEvents.Log(ctx, WorldEventKind.WorkAbandoned, id, city.Id.Value, TypeOf(project), project.SiteX, project.SiteY,
            $"sem-mao|{project.HoursWorked}", cause: project.StartEventId);
        world.CanonicalHashCache.MarkPropertyDirty(nameof(WorldState.Cities));
        return true;
    }

    internal static void Progressed(WorldState world, TickContext ctx, City city, ConstructionProject project, long now)
    {
        if (project.WorkId is not { } id) return;
        if (project.Stalled == true)
        {
            project.Stalled = null;
            WorkEvents.Log(ctx, WorldEventKind.WorkResumed, id, city.Id.Value, TypeOf(project), project.SiteX, project.SiteY,
                ((now - (project.LastProgressTick ?? now)) / world.Calendar.HoursPerDay).ToString(System.Globalization.CultureInfo.InvariantCulture), cause: project.StartEventId);
        }
        project.LastProgressTick = now;
    }

    /// <summary>AD-195: o material da receita que ficou no lote (e o que ia nas mãos da turma) volta ao estoque da cidade.</summary>
    internal static void ReturnLeftover(City city, ConstructionProject project)
    {
        foreach (var (resource, units) in project.MaterialOnSite ?? [])
            if (units > 0) city.DepositStock(new LivingWorld.Domain.Geography.ResourceType(resource), units);
        project.MaterialOnSite = null;
        project.Hauls = null;
    }

    internal static void Done(WorldState world, TickContext ctx, City city, ConstructionProject project, Building built)
    {
        ReturnLeftover(city, project);
        if (project.WorkId is not { } id) return;
        WorkEvents.Log(ctx, WorldEventKind.BuildWorkDone, id, city.Id.Value, TypeOf(project), project.SiteX, project.SiteY,
            $"{built.Id.Value}|{project.HoursWorked}|0|{project.Paid}|-", cause: project.StartEventId);
    }
}
