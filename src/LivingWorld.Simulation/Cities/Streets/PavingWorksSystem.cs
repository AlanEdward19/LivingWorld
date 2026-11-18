using LivingWorld.Domain.Emergence;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Cities.Streets;

/// <summary>Obra de calçamento, <c>Daily</c> (AD-176, AD-179): cada dia a turma de cada obra se refaz (<see cref="SiteWork.Staff"/>:
/// sai quem já não precisa, entra quem aceita pela utilidade dele, até <see cref="LivingWorld.Domain.Cities.StreetRules.CrewMax"/>).
/// A obra não anda sozinha: só as horas de quem está no trecho contam, pagas por hora a quem as trabalhou
/// (<see cref="SiteWork.Clock"/>, quando a jornada acaba). Pronta, a célula ganha o calçamento com condição cheia (a rede muda na
/// revisão seguinte).</summary>
[LivingCapability("STREETS", Events = [WorldEventKind.StreetWorkDone, WorldEventKind.SiteWorkJoined, WorldEventKind.SiteWorkLeft],
    Consumers = ["streets.surface", "economy.wallet", "behavior.work-destination"])]
public sealed class PavingWorksSystem : ISimulationSystem
{
    public const string SystemName = "paving-works";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.StreetsOn || world.Ground!.Works.Count == 0) return;
        foreach (var work in world.Ground.Works.OrderBy(w => w.Id).ToList())
        {
            var at = SiteWork.SiteOf(world, work);
            if (world.FindActiveCity(new CityId(work.City)) is null)
            {
                world.Ground.Works.Remove(work);
                SiteWork.Touch(world);
                if (world.StreetRules.WageBidding) Works.WorkEvents.Log(ctx, WorldEventKind.WorkAbandoned, work.Id, work.City, "calcamento", at.X, at.Y,
                    "cidade|0", SystemName, work.StartEventId);
                continue;
            }
            // AD-179: sem nenhuma hora de turma por meses (vila sem mão livre), a obra é desfeita; o material comprado fica gasto
            long idle = ctx.CurrentTick - work.LastProgressTick;
            if (idle > world.StreetRules.SiteStaleMonths * world.Calendar.HoursPerMonth)
            {
                world.Ground.Works.Remove(work);
                world.Ground.WorksAbandoned++;
                SiteWork.Touch(world);
                foreach (long id in work.Crew)
                    ctx.LogEvent(WorldEventKind.SiteWorkLeft, $"{id}|{work.Id}|desfeita", sourceSystem: SiteWork.SourceSystem);
                if (world.StreetRules.WageBidding) Works.WorkEvents.Log(ctx, WorldEventKind.WorkAbandoned, work.Id, work.City, "calcamento", at.X, at.Y,
                    $"sem-mao|{(work.HoursTotal <= 0 ? 0 : (work.HoursTotal - work.HoursLeft) * 1000 / work.HoursTotal)}", SystemName, work.StartEventId);
                continue;
            }
            // AD-184: a obra sem hora há dias conta como parada (evento com lugar); a retomada sai quando a turma volta
            if (world.StreetRules.WageBidding && work.Stalled != true && idle > world.StreetRules.StallDays * (long)world.Calendar.HoursPerDay)
            {
                work.Stalled = true;
                Works.WorkEvents.Log(ctx, WorldEventKind.WorkStalled, work.Id, work.City, "calcamento", at.X, at.Y,
                    (idle / world.Calendar.HoursPerDay).ToString(System.Globalization.CultureInfo.InvariantCulture), SystemName, work.StartEventId);
            }
            SiteWork.Staff(world, ctx, work);
        }
        world.CanonicalHashCache.MarkPropertyDirty(nameof(WorldState.Ground));
    }

    /// <summary>A obra acabou: o calçamento entra com condição cheia, a turma se desfaz (a obra sai da lista) e o evento conta.</summary>
    internal static void Finish(WorldState world, TickContext ctx, PavingWork work)
    {
        var ground = world.Ground!;
        foreach (int cell in work.Cells) ground.SetPaving(cell, work.Surface, 1000);
        ground.Works.Remove(work);
        // AD-184: o trecho de teste pronto é a descoberta (a cidade passa a saber calçar)
        if (work.Prototype is { } trial) PavingTechnique.Handle(world, ctx, $"{PavingTechnique.PayloadPrefix}{work.City}|{trial}");
        ground.WorksDone++;
        SiteWork.Touch(world);
        var at = ground.CellOf(work.Cells[0]);
        ctx.LogEvent(WorldEventKind.StreetWorkDone,
            $"{work.Id}|{work.City}|{work.Surface}|{(work.Repair ? 1 : 0)}|{work.Cells.Count}|{work.Paid}|{at.X}|{at.Y}", sourceSystem: SystemName,
            causeEventId: work.StartEventId);
    }
}

/// <summary>A técnica de calçar (AD-176) entra pelo sistema de tecnologia que já existe (<c>TechDiscoverySystem</c>): numa cidade
/// com <see cref="LivingWorld.Domain.Cities.StreetRules.PavingDirtCells"/> células de terra batida (a rua que a lama e o pó
/// atrapalham) e alguém hábil (<see cref="EmergenceRules.MinSkill"/>), abre o primeiro trecho de teste; ao concluir, nasce a
/// tecnologia de categoria <see cref="Category"/> adotada por ela, que se espalha às cidades da mesma cultura por vínculo, como a
/// de mobilidade. Calça com cascalho; com o eixo de habilidade a partir de
/// <see cref="LivingWorld.Domain.Cities.StreetRules.StoneSkillAxis"/>, com pedra.</summary>
public static class PavingTechnique
{
    public const string Category = "paving";
    internal const string PayloadPrefix = "paving|";

    internal static GroundSurface BestSurface(WorldState world, CityId city)
    {
        var best = GroundSurface.None;
        foreach (var tech in world.Technologies)
        {
            if (tech.Category != Category || !tech.AdoptedBy.Contains(city)) continue;
            var surface = tech.Vector.Axes.Length > 1 && tech.Vector.Axes[1] >= world.StreetRules.StoneSkillAxis
                ? GroundSurface.Stone : GroundSurface.Gravel;
            if (surface > best) best = surface;
        }
        return best;
    }

    /// <summary>Anual: cidades sem a técnica, com pressão (terra batida) e habilidade, abrem o trecho de teste.</summary>
    internal static void Tick(WorldState world, TickContext ctx, Dictionary<CityId, List<LivingWorld.Domain.Population.Npc>> residents,
        HashSet<string> pendingPayloads, string systemName)
    {
        if (!world.StreetsOn) return;
        var rules = world.EmergenceRules;
        var ground = world.Ground!;
        long year = ctx.CurrentTick / world.Calendar.HoursPerYear;
        foreach (var city in world.ActiveCities().OrderBy(c => c.Id.Value))
        {
            if (BestSurface(world, city.Id) != GroundSurface.None || pendingPayloads.Contains(city.Id.Value.ToString())) continue;
            if (ground.Works.Any(w => w.Prototype is not null && w.City == city.Id.Value)) continue; // AD-184: o trecho de teste já está em obra
            if (!residents.TryGetValue(city.Id, out var people)) continue;
            var bounds = CityOccupancy.ResolveGrownBounds(world, city, people.Count).Bounds;
            int dirt = 0, busiest = -1;
            for (int i = 0; i < ground.Count; i++)
                if (ground.SurfaceAt(i) >= GroundSurface.Dirt && bounds.Contains(ground.CellOf(i)))
                {
                    dirt++;
                    if (ground.PavingAt(i) == GroundSurface.None && (busiest < 0 || ground.PassesAt(i) > ground.PassesAt(busiest))) busiest = i;
                }
            if (dirt < world.StreetRules.PavingDirtCells) continue;
            double bestSkill = 0;
            foreach (var npc in people)
                foreach (var value in npc.Skills.Values.Values)
                    if (value > bestSkill) bestSkill = value;
            if (bestSkill < rules.MinSkill) continue;
            var rng = ctx.OneShot($"paving-{city.Id.Value}-{year}");
            int max = rules.AxisMax;
            int[] levels = [Math.Min(max, dirt * max / (4 * Math.Max(1, world.StreetRules.PavingDirtCells))), (int)Math.Min(max, bestSkill * max / 100.0), max / 2];
            var axes = new int[rules.AxisCount];
            for (int i = 0; i < axes.Length; i++)
                axes[i] = Math.Clamp(levels[i % levels.Length] + (int)Math.Floor(rng.NextDouble() * 5) - 2, 0, max);
            var culture = people[0].Culture;
            string trial = $"{culture.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{string.Join('.', axes)}";
            // AD-184: o trecho de teste é obra com turma no trecho mais passado (o tesouro paga o jornal); sem a regra, o prazo de antes
            if (world.StreetRules.PrototypeIsWork && busiest >= 0)
            {
                PavingDecision.OpenPrototype(world, ctx, city, busiest, world.StreetRules.PrototypeHours, trial);
                continue;
            }
            ctx.ScheduleEvent(ctx.CurrentTick + world.StreetRules.PavingPrototypeTicks, systemName, $"{PayloadPrefix}{city.Id.Value}|{trial}");
        }
    }

    internal static void Handle(WorldState world, TickContext ctx, string payload)
    {
        var parts = payload[PayloadPrefix.Length..].Split('|');
        if (parts.Length != 3) return;
        var cityId = new CityId(Guid.Parse(parts[0]));
        var culture = new LivingWorld.Domain.Population.CultureId(int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
        var vector = new TechnologyVector(parts[2].Split('.').Select(a => int.Parse(a, System.Globalization.CultureInfo.InvariantCulture)).ToArray());
        var adopter = world.FindActiveCity(cityId)?.Id ?? cityId;
        if (BestSurface(world, adopter) != GroundSurface.None) return;
        world.AddTechnology(new Technology(culture, Category, vector, ctx.CurrentTick, cityId, null, [adopter]));
        ctx.LogEvent(WorldEventKind.TechnologyDiscovered, $"{cityId.Value}|{culture.Id}|{vector.Key}|", sourceSystem: "tech-discovery");
    }
}
