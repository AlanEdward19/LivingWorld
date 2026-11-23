using System.Globalization;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

/// <summary>A procura de quem deu falta do desaparecido (Fase 25) e o que o cenário declara para quem nunca é achado (AD-196). No
/// sumiço agenda-se uma busca por dia no <see cref="EventScheduler"/> (nada varre NPCs a cada tick). Em cada dia, cada vínculo
/// próximo vivo que mora na mesma cidade do desaparecido o procura com chance proporcional à força do vínculo, sorteada de um
/// stream por (desaparecido, buscador, dia); achar reencontra, reforça o laço e encerra a busca. Com a AD-196 cada um procura por
/// dias na proporção do vínculo (<see cref="PlayerRules.SearchDaysAtFullBond"/>) e, ao parar, guarda a lembrança de ter desistido;
/// sem a AD-196 todos procuram <see cref="SearchDays"/> dias. A política de abandono do cenário agenda a morte presumida ou a volta
/// ao pool. O desaparecido vive pelas regras de sempre o tempo todo; se morrer no meio, a busca acaba e a herança corre por
/// <c>NpcDeath</c>.</summary>
[LivingCapability("PLAYER_SEARCH",
    Events = [WorldEventKind.DisappearedFound, WorldEventKind.SearchGaveUp, WorldEventKind.PresumedDead, WorldEventKind.Returned],
    Consumers = ["timeline.knowledge"])]
public sealed class PlayerSearchSystem : ISimulationSystem
{
    public const string SystemName = "player-search";
    public const int SearchDays = 30;

    /// <summary>Chance diária máxima de um vínculo de força 100 achar o desaparecido.</summary>
    public const double DailyFindChanceAtFullBond = 0.2;

    private const string Presume = "presume";
    private const string Pool = "pool";

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

    /// <summary>A política de abandono do cenário (AD-196): conta a janela a partir do sumiço; vale se ele ainda estiver sumido do
    /// mesmo sumiço quando ela vencer.</summary>
    internal static void ScheduleAbandonment(TickContext ctx, WorldState world, Npc missing)
    {
        var rules = world.Players;
        if (!rules.Enabled) return;
        var (kind, days) = rules.Abandonment switch
        {
            AbandonmentPolicy.DeclareDead => (Presume, rules.DeclareDeadAfterDays),
            AbandonmentPolicy.ReturnToPool => (Pool, rules.ReturnToPoolAfterDays),
            _ => (null, 0),
        };
        if (kind is null) return;
        ctx.ScheduleEvent(ctx.CurrentTick + (long)Math.Max(1, days) * world.Calendar.HoursPerDay, SystemName,
            string.Create(CultureInfo.InvariantCulture, $"{kind}|{missing.Id.Value}|{ctx.CurrentTick}"));
    }

    /// <summary>Dias que quem tem o vínculo <paramref name="strength"/> procura.</summary>
    internal static int SearchHorizon(PlayerRules rules, double strength) =>
        rules.Enabled ? Math.Max(1, (int)Math.Ceiling(rules.SearchDaysAtFullBond * strength / 100.0)) : SearchDays;

    public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt)
    {
        var parts = evt.Payload?.Split('|');
        if (parts is { Length: 3 } && parts[0] is Presume or Pool)
        {
            Abandon(world, ctx, parts[0], new NpcId(long.Parse(parts[1], CultureInfo.InvariantCulture)),
                long.Parse(parts[2], CultureInfo.InvariantCulture));
            return;
        }
        if (parts is not { Length: 3 }) return;

        var missing = world.FindNpc(new NpcId(long.Parse(parts[0], CultureInfo.InvariantCulture)));
        int day = int.Parse(parts[1], CultureInfo.InvariantCulture);
        var bonds = parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split(':'))
            .Select(pair => (Id: new NpcId(long.Parse(pair[0], CultureInfo.InvariantCulture)), Strength: double.Parse(pair[1], CultureInfo.InvariantCulture)))
            .ToList();
        if (missing is not { IsAlive: true, DisappearedAtTick: not null }) return; // morreu ou já foi achado

        var rules = world.Players;
        foreach (var (searcherId, strength) in bonds)
        {
            if (SearchHorizon(rules, strength) < day) continue; // já parou de procurar
            if (world.FindNpc(searcherId) is not { IsAlive: true } searcher || searcher.City != missing.City) continue;

            double roll = ctx.OneShot($"player-search-{missing.Id.Value}-{searcherId.Value}-{day}").NextDouble();
            if (roll >= DailyFindChanceAtFullBond * strength / 100.0) continue;

            Found(world, ctx, missing, searcher, day);
            return;
        }

        int last = bonds.Max(b => SearchHorizon(rules, b.Strength));
        foreach (var (id, strength) in bonds)
            if (SearchHorizon(rules, strength) == day) StopSearching(world, ctx, missing, id, strength, day);

        if (day < last)
            ctx.ScheduleEvent(ctx.CurrentTick + world.Calendar.HoursPerDay, SystemName, Payload(missing.Id, day + 1, bonds));
        else
            ctx.LogEvent(WorldEventKind.SearchGaveUp, $"{missing.Id.Value}|{bonds.Count}|{day}", sourceSystem: SystemName);
    }

    private static void Found(WorldState world, TickContext ctx, Npc missing, Npc searcher, int day)
    {
        if (world.Players.Enabled) LateReturn.Apply(world, ctx, missing, LateReturn.BySearch); // AD-196: achado, volta para casa
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

    private static void StopSearching(WorldState world, TickContext ctx, Npc missing, NpcId id, double strength, int day)
    {
        if (world.FindNpc(id) is not { IsAlive: true } mourner) return;
        world.AddNpcMemory(
            id, MemoryCategory.Episodic, $"{mourner.Name} deixou de procurar {missing.Name} depois de {day} dias",
            (int)Math.Round(Math.Min(100, strength + 10)), ctx.CurrentTick, [missing.Id], mourner.CurrentLocation,
            LlmRules.Default.CanonicalMemoryImportanceThreshold);
    }

    /// <summary>A janela do cenário venceu e ele segue sumido do mesmo sumiço: morte presumida ou volta ao pool.</summary>
    private static void Abandon(WorldState world, TickContext ctx, string kind, NpcId id, long disappearedAt)
    {
        if (world.FindNpc(id) is not { IsAlive: true } npc || npc.DisappearedAtTick != disappearedAt || npc.ControllerId is not null)
            return;
        int days = (int)((ctx.CurrentTick - disappearedAt) / world.Calendar.HoursPerDay);
        if (kind == Pool)
        {
            LateReturn.Apply(world, ctx, npc, LateReturn.ByPool);
            npc.Reappear();
            return;
        }
        if (npc.PresumedDeadAtTick is not null) return;

        if (!world.Players.PresumedDeadMayReturn)
        {
            // a declaração é a morte do personagem: herança, dívida, viuvez, luto e espólio pelo caminho de qualquer morte
            NpcDeath.Apply(world, ctx, npc, WorldEventKind.PresumedDead, string.Create(CultureInfo.InvariantCulture, $"{days}|0"));
            return;
        }

        // o cenário deixa voltar: o mundo o dá por morto (luto, herança, dívida, viuvez), mas ele segue vivo e sumido
        ctx.LogEvent(WorldEventKind.PresumedDead, string.Create(CultureInfo.InvariantCulture, $"{npc.Id.Value}|{days}|1"),
            sourceSystem: SystemName);
        EstateSystem.Settle(world, ctx, npc);
        if (npc.Spouse is { } spouseId && world.FindNpc(spouseId) is { } spouse && spouse.Spouse == npc.Id) spouse.EndMarriage();
        npc.EndMarriage();
        npc.MarkPresumedDead(ctx.CurrentTick);
    }

    private static string Payload(NpcId missing, int day, IReadOnlyList<(NpcId Id, double Strength)> bonds) =>
        string.Create(
            CultureInfo.InvariantCulture,
            // a força em cultura invariante: com vírgula decimal (pt-BR) "47,5" quebrava a lista separada por vírgula
            $"{missing.Value}|{day}|{string.Join(',', bonds.Select(b => b.Id.Value.ToString(CultureInfo.InvariantCulture) + ":" + b.Strength.ToString("0.##", CultureInfo.InvariantCulture)))}");
}
