using LivingWorld.Domain.Emergence;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Cities.Streets;

/// <summary>Obra de calçamento, <c>Daily</c> (AD-176): os diaristas do dia (adultos da cidade sem emprego, até
/// <see cref="LivingWorld.Domain.Cities.StreetRules.CrewMax"/>) trabalham <see cref="LivingWorld.Domain.Cities.StreetRules.CrewHoursPerDay"/>
/// horas cada e recebem o jornal de quem paga (o tesouro ou as casas de frente, em partes iguais). Sem diarista ou sem dinheiro
/// no dia, a obra para. Pronta, a célula ganha o calçamento com condição cheia (a rede muda na revisão seguinte).</summary>
[LivingCapability("STREETS", Events = [WorldEventKind.StreetWorkDone], Consumers = ["streets.surface", "economy.wallet"])]
public sealed class PavingWorksSystem : ISimulationSystem
{
    public const string SystemName = "paving-works";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.StreetsOn || world.Ground!.Works.Count == 0) return;
        var ground = world.Ground;
        var rules = world.StreetRules;
        long dayWage = PavingDecision.DayWage(world);
        foreach (var work in ground.Works.OrderBy(w => w.Id).ToList())
        {
            var cityId = new CityId(work.City);
            var city = world.FindActiveCity(cityId);
            if (city is null) { ground.Works.Remove(work); continue; }
            var crew = PavingDecision.DayLaborers(world, city.Id, rules.CrewMax);
            if (crew.Count == 0) continue;
            long due = dayWage * crew.Count;
            if (!Pay(world, ctx, city, work, due)) continue;
            foreach (var worker in crew) worker.CreditWallet(new Money(dayWage));
            work.Paid += due;
            work.HoursLeft -= (long)crew.Count * rules.CrewHoursPerDay;
            if (work.HoursLeft > 0) continue;

            foreach (int cell in work.Cells) ground.SetPaving(cell, work.Surface, 1000);
            ground.Works.Remove(work);
            ground.WorksDone++;
            ctx.LogEvent(WorldEventKind.StreetWorkDone,
                $"{work.Id}|{work.City}|{work.Surface}|{(work.Repair ? 1 : 0)}|{work.Cells.Count}|{work.Paid}", sourceSystem: SystemName,
                causeEventId: work.StartEventId);
        }
        world.CanonicalHashCache.MarkPropertyDirty(nameof(WorldState.Ground));
    }

    private static bool Pay(WorldState world, TickContext ctx, LivingWorld.Domain.Cities.City city, PavingWork work, long due) =>
        PavingDecision.PayFrom(world, ctx, city, work.Payers, due);
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
            if (!residents.TryGetValue(city.Id, out var people)) continue;
            var bounds = CityOccupancy.ResolveGrownBounds(world, city, people.Count).Bounds;
            int dirt = 0;
            for (int i = 0; i < ground.Count; i++)
                if (ground.SurfaceAt(i) >= GroundSurface.Dirt && bounds.Contains(ground.CellOf(i))) dirt++;
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
            ctx.ScheduleEvent(ctx.CurrentTick + world.StreetRules.PavingPrototypeTicks, systemName,
                $"{PayloadPrefix}{city.Id.Value}|{culture.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{string.Join('.', axes)}");
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
