using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Health;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Health;

/// <summary>Doença (disease-epidemics), <c>Daily</c>: fecha as infecções que terminam (cura com imunidade, ou morte pela
/// letalidade da doença), espalha por contato (quem está na mesma célula de um infectado, por sorteio semeado do par e do dia),
/// e abre surtos onde a cidade passou do limiar de população. Desligado no padrão do cenário. Nada aqui lê relógio nem
/// ordem de dicionário: tudo em ordem de id e com streams nomeados, então o mesmo mundo dá a mesma epidemia.</summary>
[LivingCapability("OUTBREAK", Events = [WorldEventKind.OutbreakStarted], Consumers = ["timeline.outbreak"])]
[RunsAfter(typeof(MortalitySystem))]
public sealed class OutbreakSystem : ISimulationSystem
{
    public const string SystemName = "health-outbreak";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    public void Tick(WorldState world, TickContext ctx)
    {
        var rules = world.DiseaseRules;
        if (!rules.Enabled || rules.Diseases.Count == 0) return;

        long now = ctx.CurrentTick;
        long day = now / world.CurrentDate.Calendar.HoursPerDay;

        ResolveEndedInfections(world, ctx, rules, now);
        world.PurgeExpiredImmunities();
        if (world.Infections.Count > 0) Spread(world, ctx, rules, now, day);
        StartOutbreaks(world, ctx, rules, now, day);
    }

    private static void ResolveEndedInfections(WorldState world, TickContext ctx, DiseaseRules rules, long now)
    {
        foreach (var infection in world.Infections.Where(i => i.EndTick <= now).ToList())
        {
            world.RemoveInfection(infection.Npc, infection.Disease);
            var profile = rules.Diseases.FirstOrDefault(d => d.Id == infection.Disease);
            if (profile is null || world.FindNpc(infection.Npc) is not { IsAlive: true } npc) continue;

            double roll = ctx.Rng($"disease-outcome-{infection.Npc.Value}-{infection.Disease}-{infection.EndTick}").NextDouble();
            if (roll < profile.Lethality)
            {
                ctx.LogEvent(WorldEventKind.Disease, $"{npc.Id.Value}|{profile.Id}", sourceSystem: SystemName);
                NpcDeath.Apply(world, ctx, npc, WorldEventKind.Disease);
            }
            else
                world.SetImmunity(npc.Id, profile.Id, now + (long)profile.ImmunityDays * world.CurrentDate.Calendar.HoursPerDay);
        }
    }

    private static void Spread(WorldState world, TickContext ctx, DiseaseRules rules, long now, long day)
    {
        var byCell = new Dictionary<CellCoord, List<Npc>>();
        foreach (var npc in world.Npcs)
        {
            if (!npc.IsAlive) continue;
            if (!byCell.TryGetValue(npc.CurrentLocation, out var group)) byCell[npc.CurrentLocation] = group = [];
            group.Add(npc);
        }

        var caught = new List<(NpcId Npc, DiseaseProfile Profile)>();
        foreach (var infection in world.Infections.ToList())
        {
            var profile = rules.Diseases.FirstOrDefault(d => d.Id == infection.Disease);
            if (profile is null || world.FindNpc(infection.Npc) is not { IsAlive: true } source) continue;
            if (!byCell.TryGetValue(source.CurrentLocation, out var neighbours)) continue;

            foreach (var other in neighbours)
            {
                if (other.Id == source.Id || world.IsInfected(other.Id, profile.Id) || world.IsImmune(other.Id, profile.Id)) continue;
                if (caught.Any(c => c.Npc == other.Id && c.Profile.Id == profile.Id)) continue;

                double roll = ctx.Rng($"contagion-{source.Id.Value}-{other.Id.Value}-{day}").NextDouble();
                if (roll < profile.ContagionPerContactDay) caught.Add((other.Id, profile));
            }
        }

        foreach (var (npc, profile) in caught.OrderBy(c => c.Npc.Value).ThenBy(c => c.Profile.Id))
            world.AddInfection(npc, profile.Id, now + (long)profile.DurationDays * world.CurrentDate.Calendar.HoursPerDay);
    }

    private static void StartOutbreaks(WorldState world, TickContext ctx, DiseaseRules rules, long now, long day)
    {
        foreach (var profile in rules.Diseases.OrderBy(d => d.Id))
        {
            if (world.Infections.Any(i => i.Disease == profile.Id)) continue;
            if (ctx.Rng($"outbreak-{profile.Id}-{day}").NextDouble() >= rules.OutbreakDailyChance) continue;

            // a maior cidade acima do limiar (empate: menor id de cidade), e nela o paciente zero é sorteado entre os vivos
            var crowded = world.Npcs.Where(n => n.IsAlive).GroupBy(n => n.City)
                .Select(g => (City: g.Key, Members: g.OrderBy(n => n.Id.Value).ToList()))
                .Where(g => g.Members.Count >= rules.OutbreakPopulationThreshold)
                .OrderByDescending(g => g.Members.Count).ThenBy(g => g.City.Value)
                .FirstOrDefault();
            if (crowded.Members is null) continue;

            int pick = (int)(ctx.Rng($"outbreak-zero-{profile.Id}-{day}").NextDouble() * crowded.Members.Count);
            var patientZero = crowded.Members[Math.Min(pick, crowded.Members.Count - 1)];
            if (world.IsImmune(patientZero.Id, profile.Id)) continue;

            world.AddInfection(patientZero.Id, profile.Id, now + (long)profile.DurationDays * world.CurrentDate.Calendar.HoursPerDay);
            ctx.LogEvent(WorldEventKind.OutbreakStarted, $"{profile.Id}|{patientZero.Id.Value}|{crowded.City.Value}", sourceSystem: SystemName);
        }
    }
}
