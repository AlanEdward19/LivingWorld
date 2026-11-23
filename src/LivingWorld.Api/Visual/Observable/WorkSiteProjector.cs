using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Works;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Api.Visual.Observable;

/// <summary>AD-193: material da obra que o motor conta (só a obra da fila da cidade: a receita, o que já foi posto no prédio).</summary>
public sealed record WorkMaterialVisual(int ResourceId, string? Name, long Needed, long Placed);

/// <summary>AD-193: uma obra em andamento como o motor a tem, para o cliente desenhar o canteiro com verdade e a ficha dizer quem ergue,
/// para quem, o que falta e por que parou. Só leitura (nada aqui é guardado no mundo). <see cref="Halt"/>: <c>tempo</c> (chuva forte,
/// neve ou chão congelado no lote: ninguém vai nem conta hora, AD-180), <c>parada</c> (sem nenhuma hora por <c>StallDays</c>),
/// <c>material</c> (a obra da fila tem horas e o estoque da cidade não tem o insumo do passo); nulo = anda. <see cref="OnSite"/> = quem
/// da turma ou dos ajudantes está no lote trabalhando agora (as horas que <c>SiteWork.Clock</c> conta). <see cref="Payer"/>:
/// <c>casa</c>, <c>fundador</c> ou <c>cidade</c>.</summary>
public sealed record WorkSiteVisual(long Id, string Kind, string CityId, CellCoord Location, double Progress)
{
    public long? BuildingId { get; init; }
    public int? BuildingTypeId { get; init; }
    public string? TypeName { get; init; }
    public bool Stalled { get; init; }
    public string? Halt { get; init; }
    public string Payer { get; init; } = "casa";
    public long? OwnerId { get; init; }
    public long? HouseholdId { get; init; }
    public IReadOnlyList<long> Crew { get; init; } = ValueList<long>.Empty;
    public IReadOnlyList<long> Helpers { get; init; } = ValueList<long>.Empty;
    public IReadOnlyList<long> OnSite { get; init; } = ValueList<long>.Empty;
    public long HoursTotal { get; init; }
    public long HoursLeft { get; init; }
    public long HoursByCrew { get; init; }
    public long HoursByHelpers { get; init; }
    /// <summary>Horas de carregar o material do largo ao lote que ainda faltam (só a obra da fila).</summary>
    public long HaulHoursLeft { get; init; }
    public long Paid { get; init; }
    /// <summary>Moeda gasta ao abrir: material comprado e foro (casa), foro e equipamento (oficina).</summary>
    public long Spent { get; init; }
    public int OfferPm { get; init; }
    public long StartedTick { get; init; }
    public long LastWorkedTick { get; init; }
    public IReadOnlyList<WorkMaterialVisual> Materials { get; init; } = ValueList<WorkMaterialVisual>.Empty;
}

/// <summary>AD-193: as obras de uma cidade (casa, oficina e prédio da fila com lote) lidas do estado canônico, sem mexer nele.</summary>
public static class WorkSiteProjector
{
    public static IReadOnlyList<WorkSiteVisual> InCity(WorldState world, CityId cityId, PresentationNames names)
    {
        if (world.Ground is not { } ground) return [];
        var city = world.FindActiveCity(cityId);
        List<WorkSiteVisual>? result = null;
        foreach (var work in ground.Builds)
            if (work.City == cityId.Value)
                (result ??= []).Add(Of(world, work, names));
        if (city is not null)
            foreach (var project in city.ConstructionQueue)
                if (project.WorkId is not null && project.SiteX >= 0 && Of(world, city, project, names) is { } queued)
                    (result ??= []).Add(queued);
        if (result is null) return [];
        result.Sort((a, b) => a.Id.CompareTo(b.Id));
        return result;
    }

    /// <summary>A obra pelo id, em qualquer cidade (ficha da obra); nula se acabou ou nunca existiu.</summary>
    public static WorkSiteVisual? Find(WorldState world, long id, PresentationNames names)
    {
        if (world.Ground is not { } ground) return null;
        foreach (var work in ground.Builds)
            if (work.Id == id) return Of(world, work, names);
        foreach (var city in world.ActiveCities())
            foreach (var project in city.ConstructionQueue)
                if (project.WorkId == id && project.SiteX >= 0) return Of(world, city, project, names);
        return null;
    }

    public static WorkSiteVisual Of(WorldState world, BuildWork work, PresentationNames names)
    {
        var site = new CellCoord(work.X, work.Y);
        int? typeId = world.FindBuilding(new BuildingId(work.Building)) is { } canteiro ? canteiro.BuildingTypeId : null;
        return new WorkSiteVisual(work.Id, BuildWorks.TypeOf(work), work.City.ToString(), site,
            Fraction(work.HoursTotal - Math.Max(0, work.HoursLeft), work.HoursTotal))
        {
            BuildingId = work.Building,
            BuildingTypeId = typeId,
            TypeName = typeId is { } t ? names.BuildingType(t) : null,
            Stalled = work.Stalled,
            Halt = HaltOf(world, site, work.Stalled, materialShort: false),
            Payer = work.Kind == BuildKind.House ? "casa" : "fundador",
            OwnerId = work.Owner,
            HouseholdId = work.Household,
            Crew = ValueList<long>.Of(work.Crew),
            Helpers = ValueList<long>.Of(work.Helpers),
            OnSite = OnSite(world, site, work.Crew, work.Helpers),
            HoursTotal = work.HoursTotal,
            HoursLeft = Math.Max(0, work.HoursLeft),
            HoursByCrew = work.HoursByCrew,
            HoursByHelpers = work.HoursByHelpers,
            Paid = work.Paid,
            Spent = work.Spent,
            OfferPm = work.OfferPm,
            StartedTick = work.StartTick,
            LastWorkedTick = work.LastProgressTick,
        };
    }

    private static WorkSiteVisual? Of(WorldState world, LivingWorld.Domain.Cities.City city, ConstructionProject project, PresentationNames names)
    {
        if (!world.CityCatalog.BuildingRecipes.TryGetValue(project.BuildingTypeId, out var recipe)) return null;
        var site = new CellCoord(project.SiteX, project.SiteY);
        long total = Math.Max(1, recipe.TicksToBuild);
        var materials = new List<WorkMaterialVisual>(recipe.Inputs.Count);
        bool short_ = false;
        long tickIndex = recipe.TicksToBuild - project.TicksRemaining + 1;
        foreach (var (resource, needed) in recipe.Inputs.OrderBy(kv => kv.Key.Id))
        {
            long placed = project.Consumed.GetValueOrDefault(resource);
            materials.Add(new WorkMaterialVisual(resource.Id, names.Resource(resource.Id), needed, placed));
            // o mesmo passo da receita que a fila cobra do estoque da cidade (ConstructionSystem): sem ele, as horas ficam guardadas
            long target = tickIndex >= recipe.TicksToBuild ? needed : needed * tickIndex / Math.Max(1, recipe.TicksToBuild);
            long due = target - placed;
            if (project.TicksRemaining > 0 && project.HaulHoursLeft == 0 && project.HoursBanked > 0 && due > 0
                && city.Stock.GetValueOrDefault(resource) < due)
                short_ = true;
        }
        bool stalled = project.Stalled == true;
        return new WorkSiteVisual(project.WorkId!.Value, $"predio-{project.BuildingTypeId}", city.Id.ToString(), site,
            Fraction(total - project.TicksRemaining, total))
        {
            BuildingId = project.Canteiro,
            BuildingTypeId = project.BuildingTypeId,
            TypeName = names.BuildingType(project.BuildingTypeId),
            Stalled = stalled,
            Halt = HaltOf(world, site, stalled, short_),
            Payer = "cidade",
            Crew = ValueList<long>.Of(project.Crew),
            OnSite = OnSite(world, site, project.Crew, []),
            HoursTotal = total,
            HoursLeft = project.TicksRemaining,
            HoursByCrew = project.HoursWorked,
            HaulHoursLeft = project.HaulHoursLeft,
            Paid = project.Paid,
            OfferPm = project.OfferPm ?? 1000,
            LastWorkedTick = project.LastProgressTick ?? 0,
            Materials = ValueList<WorkMaterialVisual>.Of(materials),
        };
    }

    private static double Fraction(long done, long total) => Math.Round(Math.Clamp(done / (double)Math.Max(1, total), 0, 1), 3);

    private static string? HaltOf(WorldState world, CellCoord site, bool stalled, bool materialShort) =>
        !LivingWorld.Simulation.Climate.WeatherNow.OutdoorWorkable(world, site) ? "tempo"
        : stalled ? "parada"
        : materialShort ? "material"
        : null;

    /// <summary>Quem está no lote trabalhando agora: as mesmas condições em que a hora conta para a obra (no lote, na ação de trabalho).</summary>
    private static IReadOnlyList<long> OnSite(WorldState world, CellCoord site, List<long> crew, List<long> helpers)
    {
        List<long>? here = null;
        foreach (var list in (ReadOnlySpan<List<long>>)[crew, helpers])
            foreach (long id in list)
                if (world.FindNpc(new NpcId(id)) is { IsAlive: true } npc && npc.CurrentLocation == site
                    && npc.CurrentAction == ActionType.Work && (here is null || !here.Contains(id)))
                    (here ??= []).Add(id);
        if (here is null) return ValueList<long>.Empty;
        here.Sort();
        return ValueList<long>.Of(here);
    }

    /// <summary>Alvo da ação de quem trabalha numa obra e está no lote dela agora (diarista ou ajudante): <c>work:id</c>. Nulo fora disso.</summary>
    public static long? SiteTargetOf(WorldState world, Npc npc)
    {
        if (npc.CurrentAction != ActionType.Work || !world.StreetsOn || world.Ground is null) return null;
        if (LivingWorld.Simulation.Cities.Streets.SiteWork.WorkOf(world, npc) is { } site)
        {
            if (site.Build is { } build) return npc.CurrentLocation == new CellCoord(build.X, build.Y) ? build.Id : null;
            if (site.Project is { WorkId: { } queued } project) return npc.CurrentLocation == new CellCoord(project.SiteX, project.SiteY) ? queued : null;
            return null;
        }
        return LivingWorld.Simulation.Cities.Streets.SiteWork.HelpOf(world, npc) is { } help && npc.CurrentLocation == new CellCoord(help.X, help.Y)
            ? help.Id
            : null;
    }
}
