using System.Globalization;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

/// <summary>A procura de quem deu falta do desaparecido (Fase 25). No logout agenda-se uma busca por dia, por
/// <see cref="SearchDays"/> dias, no <see cref="EventScheduler"/> (nada varre NPCs a cada tick). Em cada dia, cada vínculo
/// próximo vivo que mora na mesma cidade do desaparecido o procura com chance proporcional à força do vínculo, sorteada de um
/// stream por (desaparecido, buscador, dia); achar reencontra, reforça o laço e encerra a busca. Sem achar até o último dia,
/// quem ficou sofre (memória mais forte) e segue em frente. O desaparecido vive pelas regras de sempre o tempo todo; se
/// morrer no meio, a busca acaba e a herança corre por <c>NpcDeath</c>.</summary>
[LivingCapability("PLAYER_SEARCH", Events = [WorldEventKind.DisappearedFound, WorldEventKind.SearchGaveUp], Consumers = ["timeline.knowledge"])]
public sealed class PlayerSearchSystem : ISimulationSystem
{
    public const string SystemName = "player-search";
    public const int SearchDays = 30;

    /// <summary>Chance diária máxima de um vínculo de força 100 achar o desaparecido.</summary>
    public const double DailyFindChanceAtFullBond = 0.2;

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Yearly;

    public void Tick(WorldState world, TickContext ctx)
    {
    }

    internal static void Begin(TickContext ctx, WorldState world, Npc missing, IReadOnlyList<(NpcId Id, double Strength)> bonds)
    {
        if (bonds.Count == 0) return;
        ctx.ScheduleEvent(ctx.CurrentTick + world.Calendar.HoursPerDay, SystemName, Payload(missing.Id, 1, bonds));
    }

    public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt)
    {
        var parts = evt.Payload?.Split('|');
        if (parts is not { Length: 3 }) return;

        var missing = world.FindNpc(new NpcId(long.Parse(parts[0], CultureInfo.InvariantCulture)));
        int day = int.Parse(parts[1], CultureInfo.InvariantCulture);
        var bonds = parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split(':'))
            .Select(pair => (Id: new NpcId(long.Parse(pair[0], CultureInfo.InvariantCulture)), Strength: double.Parse(pair[1], CultureInfo.InvariantCulture)))
            .ToList();
        if (missing is not { IsAlive: true, DisappearedAtTick: not null }) return; // morreu ou já foi achado

        foreach (var (searcherId, strength) in bonds)
        {
            if (world.FindNpc(searcherId) is not { IsAlive: true } searcher || searcher.City != missing.City) continue;

            double roll = ctx.Rng($"player-search-{missing.Id.Value}-{searcherId.Value}-{day}").NextDouble();
            if (roll >= DailyFindChanceAtFullBond * strength / 100.0) continue;

            Found(world, ctx, missing, searcher, day);
            return;
        }

        if (day < SearchDays)
            ctx.ScheduleEvent(ctx.CurrentTick + world.Calendar.HoursPerDay, SystemName, Payload(missing.Id, day + 1, bonds));
        else
            GiveUp(world, ctx, missing, bonds, day);
    }

    private static void Found(WorldState world, TickContext ctx, Npc missing, Npc searcher, int day)
    {
        missing.Reappear();
        long now = ctx.CurrentTick;
        foreach (var key in new[] { new RelationshipKey(searcher.Id, missing.Id), new RelationshipKey(missing.Id, searcher.Id) })
        {
            var tie = world.GetOrCreateRelationship(key, now);
            tie.ApplyEvent(RelationshipEventType.Help, world.FamilyRules);
            tie.MarkContact(now);
        }

        world.AddNpcMemory(
            searcher.Id, MemoryCategory.Episodic, $"{searcher.Name} reencontrou {missing.Name} depois de {day} dias de procura",
            90, now, [missing.Id], searcher.CurrentLocation, LlmRules.Default.CanonicalMemoryImportanceThreshold);
        ctx.LogEvent(
            WorldEventKind.DisappearedFound, $"{missing.Id.Value}|{searcher.Id.Value}|{day}", sourceSystem: SystemName);
    }

    private static void GiveUp(WorldState world, TickContext ctx, Npc missing, List<(NpcId Id, double Strength)> bonds, int day)
    {
        foreach (var (id, strength) in bonds)
        {
            if (world.FindNpc(id) is not { IsAlive: true } mourner) continue;
            world.AddNpcMemory(
                id, MemoryCategory.Episodic, $"{mourner.Name} deixou de procurar {missing.Name} depois de {day} dias",
                (int)Math.Round(Math.Min(100, strength + 10)), ctx.CurrentTick, [missing.Id], mourner.CurrentLocation,
                LlmRules.Default.CanonicalMemoryImportanceThreshold);
        }

        ctx.LogEvent(WorldEventKind.SearchGaveUp, $"{missing.Id.Value}|{bonds.Count}|{day}", sourceSystem: SystemName);
    }

    private static string Payload(NpcId missing, int day, IReadOnlyList<(NpcId Id, double Strength)> bonds) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{missing.Value}|{day}|{string.Join(',', bonds.Select(b => $"{b.Id.Value}:{b.Strength:0.##}"))}");
}
