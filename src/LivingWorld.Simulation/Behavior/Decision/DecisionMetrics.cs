using System.Security.Cryptography;
using System.Text;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Simulation.Behavior.Decision;

/// <summary>Contadores de redecisão (Fase 16.3 P2a, COH-44/45 / doc#85/98) — decisions e
/// wakeups por agent-day; comparação full-reconsideration vs event-driven.</summary>
public sealed class DecisionMetrics
{
    public long Decisions { get; private set; }
    public long Wakeups { get; private set; }
    public long WakeupsThatChangedIntent { get; private set; }
    public int AgentCount { get; init; }
    public int Days { get; init; }

    public double DecisionsPerAgentDay =>
        AgentCount <= 0 || Days <= 0 ? 0 : (double)Decisions / (AgentCount * Days);

    public double WakeupsPerAgentDay =>
        AgentCount <= 0 || Days <= 0 ? 0 : (double)Wakeups / (AgentCount * Days);

    public double IntentChangeWakeFraction =>
        Wakeups == 0 ? 0 : (double)WakeupsThatChangedIntent / Wakeups;

    public void RecordWake() => Wakeups++;

    public void RecordDecision(bool intentChanged)
    {
        Decisions++;
        if (intentChanged) WakeupsThatChangedIntent++;
    }

    /// <summary>Resultado de uma corrida comparativa (doc#98).</summary>
    public sealed record ModeRun(
        DecisionMetrics Metrics,
        string CanonicalFingerprint);

    /// <summary>Comparação full vs event-driven no mesmo cenário determinístico.</summary>
    public sealed record ModeComparison(ModeRun Full, ModeRun EventDriven);

    /// <summary>Roda dois modos no mesmo seed: full = todo NPC vivo decide a cada hora;
    /// event-driven = só quem tem Intent Active e foi roteado por AttentionRouter (ou
    /// need urgente) decide. Mesmo decaimento de needs; fingerprint canônico do estado
    /// final deve coincidir quando o evento não invalida planos.</summary>
    public static ModeComparison CompareFullVsEventDriven(
        ulong seed, int hours, AttentionRules? attentionRules = null)
    {
        var rules = AttentionRules.Resolve(attentionRules);
        var full = RunMode(seed, hours, eventDriven: false, rules);
        var eventDriven = RunMode(seed, hours, eventDriven: true, rules);
        return new ModeComparison(full, eventDriven);
    }

    private static ModeRun RunMode(ulong seed, int hours, bool eventDriven, AttentionRules attention)
    {
        var calendar = new WorldCalendar(24, 30, 12);
        var world = new WorldState(
            calendar, seed, ScenarioRunner.DefaultMap(seed),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules);

        var personality = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
        for (long i = 1; i <= 8; i++)
        {
            var loc = new CellCoord((int)(i % 4), (int)(i / 4));
            var npc = new Npc(
                new NpcId(i), $"n{i}", Sex.Male, WorldDate.Epoch(calendar).AddYears(-30),
                new CultureId(1), loc, null, null, null, 100, personality, ProfessionType.None, loc,
                hunger: 80, thirst: 80, sleep: 80, social: 80);
            // Metade começa com Intent Buy Active + ação alinhada (plano estável).
            if (i % 2 == 1)
            {
                npc.SetIntent(ActionType.Buy, tick: 0);
                npc.SetCurrentAction(ActionType.Buy, tick: 0);
            }
            else
            {
                npc.SetCurrentAction(ActionType.Idle, tick: 0);
            }
            world.AddNpc(npc);
        }

        int agentCount = world.Npcs.Count(n => n.IsAlive);
        int days = Math.Max(1, hours / 24);
        var metrics = new DecisionMetrics { AgentCount = agentCount, Days = days };

        // Evento econômico de baixa magnitude no tick 2 — só intent-dependents acordam.
        var priceEvt = new WorldEvent(
            Tick: 2, WorldEventKind.ResourceLost,
            $"{AttentionRouter.PriceChangePrefix}0.01|0|0",
            EventId: 1);

        for (int h = 0; h < hours; h++)
        {
            long tick = h;
            world.CurrentDate = WorldDate.Epoch(calendar).AddHours(tick);

            IEnumerable<Npc> targets;
            if (!eventDriven)
            {
                targets = world.Npcs.Where(n => n.IsAlive).OrderBy(n => n.Id.Value);
            }
            else
            {
                var wakeIds = new SortedSet<long>();
                // Need urgente → wake (comportamento antigo).
                foreach (var npc in world.Npcs.Where(n => n.IsAlive))
                {
                    if (npc.HasUrgentNeed(world.NeedsRules, tick))
                        wakeIds.Add(npc.Id.Value);
                }

                if (h == 2)
                {
                    foreach (var id in AttentionRouter.RouteRelevantNpcs(world, priceEvt, attention))
                    {
                        if (world.FindNpc(id) is { IntentStatus: IntentStatus.Active })
                            wakeIds.Add(id.Value);
                    }
                }

                // Intent Active válido e sem evento/need → NÃO reconsidera (COH-44).
                targets = wakeIds
                    .Select(id => world.FindNpc(new NpcId(id)))
                    .Where(n => n is { IsAlive: true })
                    .OrderBy(n => n!.Id.Value)!;
            }

            foreach (var npc in targets)
            {
                metrics.RecordWake();
                var beforeIntent = npc!.IntentStatus;
                var beforeAction = npc.CurrentAction;

                // Persistência de Intent (COH-41/44): Active Buy permanece; reconsideração
                // full que reafirma o mesmo plano não muda o estado canônico.
                if (npc.IntentStatus == IntentStatus.Active && npc.CurrentIntent == ActionType.Buy)
                {
                    if (npc.CurrentAction != ActionType.Buy)
                        npc.SetCurrentAction(ActionType.Buy, tick);
                }
                else if (npc.HungerAt(tick) < 50)
                {
                    npc.SetCurrentAction(ActionType.Buy, tick);
                    npc.SetIntent(ActionType.Buy, tick);
                }
                else if (npc.CurrentAction != ActionType.Idle)
                {
                    npc.SetCurrentAction(ActionType.Idle, tick);
                }

                bool intentChanged = beforeIntent != npc.IntentStatus || beforeAction != npc.CurrentAction;
                metrics.RecordDecision(intentChanged);
            }
        }

        return new ModeRun(metrics, Fingerprint(world));
    }

    /// <summary>Fingerprint canônico leve do estado observável de decisão (não o hash
    /// IncrementalHasher — só intents/ações/needs relevantes a COH-44).</summary>
    public static string Fingerprint(WorldState world)
    {
        var sb = new StringBuilder();
        foreach (var npc in world.Npcs.OrderBy(n => n.Id.Value))
        {
            sb.Append(npc.Id.Value).Append('|')
                .Append(npc.CurrentAction?.ToString() ?? "-").Append('|')
                .Append(npc.CurrentIntent?.ToString() ?? "-").Append('|')
                .Append(npc.IntentStatus?.ToString() ?? "-").Append('|')
                .Append(npc.Hunger).Append(';');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexStringLower(hash);
    }
}
