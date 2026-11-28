using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Careers;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Objects;

/// <summary>#268: a necessidade crítica do corpo conduzido, tudo pelo dado <c>NeedCritical</c> (limiar por necessidade, de sempre ou do catálogo). Duas coisas: o aviso <c>need-critical</c> (um
/// tipo de aviso que o cenário põe em <c>stopKinds</c>, e então o tempo desce a 1×) e o livre-arbítrio, como no Sims: o corpo sem ordem do jogador, com a necessidade crítica, se cuida sozinho
/// (usa o móvel da casa que a atende) em vez de passar a noite com a bexiga a 4. O jogador liga e desliga no HUD; uma ordem dele (andar, usar, fila) sempre vence.</summary>
public static class CriticalNeeds
{
    public const string NoticeKind = "need-critical";
    public const string AutoKind = "need-auto";

    public static NeedCritical? RulesOf(WorldState world) => HomeLife.Catalog(world)?.Critical;

    /// <summary>O livre-arbítrio do corpo está ligado? O padrão é do dado; o livro guarda só quem inverteu o padrão.</summary>
    public static bool FreeWill(WorldState world, Npc npc) =>
        RulesOf(world) is { } rules && rules.FreeWillDefault != (world.Careers?.FreeWillSet.Contains(npc.Id.Value) == true);

    public static void SetFreeWill(WorldState world, Npc npc, bool on)
    {
        if (RulesOf(world) is not { } rules) return;
        var ledger = world.CareersLedger();
        bool flipped = rules.FreeWillDefault != on;
        ledger.FreeWillSet.Remove(npc.Id.Value);
        if (flipped)
        {
            int at = ledger.FreeWillSet.FindIndex(id => id > npc.Id.Value);
            if (at < 0) ledger.FreeWillSet.Add(npc.Id.Value); else ledger.FreeWillSet.Insert(at, npc.Id.Value);
        }

        world.CareersChanged();
    }

    private static LazyNeed? LazyOf(Npc npc, string id) => id switch
    {
        "hunger" => npc.HungerNeed,
        "thirst" => npc.ThirstNeed,
        "sleep" => npc.SleepNeed,
        "social" => npc.SocialNeed,
        _ => npc.ExtraNeeds is { } extra && extra.TryGetValue(id, out var need) ? need : null,
    };

    /// <summary>As necessidades críticas de agora, a mais abaixo do limiar primeiro (empate: id).</summary>
    public static IReadOnlyList<(string Id, double Value, double Line)> Now(WorldState world, Npc npc, long ms)
    {
        var list = new List<(string Id, double Value, double Line)>();
        if (RulesOf(world) is not { } rules) return list;
        foreach (var (id, line) in rules.Below)
            if (HomeLife.Value(world, npc, id, ms) is { } value && value < line) list.Add((id, value, line));
        list.Sort((a, b) =>
        {
            int by = (b.Line - b.Value).CompareTo(a.Line - a.Value);
            return by != 0 ? by : string.CompareOrdinal(a.Id, b.Id);
        });
        return list;
    }

    /// <summary>Quando olhar de novo: o instante em que a necessidade que ainda cai cruza o limiar; já cruzou, de hora em hora (a autonomia tenta de novo se não achou como). <c>long.MaxValue</c>: nada.</summary>
    public static long NextCheckMs(WorldState world, Npc npc, long nowMs)
    {
        if (RulesOf(world) is not { } rules || npc.ControllerId is null) return long.MaxValue;
        long next = long.MaxValue;
        foreach (var (id, line) in rules.Below)
        {
            if (LazyOf(npc, id) is not { DecayRatePerHour: > 0 } need) continue;
            double v = need.ValueAtMs(nowMs);
            long at = v < line ? nowMs + WorldMillis.PerHour : nowMs + Math.Max(1L, (long)Math.Ceiling((v - line) / need.DecayRatePerHour * WorldMillis.PerHour));
            if (at < next) next = at;
        }

        return next;
    }

    private static string TextOf(WorldState world, NeedCritical rules, string id)
    {
        if (rules.Texts is not null && rules.Texts.TryGetValue(id, out var text)) return text;
        return HomeLife.Catalog(world)?.Find(id) is { } def ? def.Alert ?? def.Name : id;
    }

    /// <summary>Na hora de decidir do corpo conduzido: avisa a necessidade crítica (uma vez por <c>RepeatHours</c>) e, com o livre-arbítrio e sem ordem do jogador, o corpo vai atendê-la. Verdadeiro se começou um uso.</summary>
    public static bool OnWake(WorldState world, TickContext ctx, Npc npc, long nowMs)
    {
        if (npc.ControllerId is null || RulesOf(world) is not { } rules || CareerWork.CatalogOf(world) is not { } catalog) return false;
        var critical = Now(world, npc, nowMs);
        if (critical.Count == 0) return false;
        var ledger = world.CareersLedger();
        foreach (var (id, value, _) in critical)
        {
            string head = TextOf(world, rules, id);
            bool recent = false;
            foreach (var n in ledger.Notices)
                if (n.Npc == npc.Id.Value && n.Kind == NoticeKind && n.Text.StartsWith(head, StringComparison.Ordinal) && nowMs - n.Ms < rules.RepeatHours * (long)WorldMillis.PerHour) { recent = true; break; }
            if (recent) continue;
            ledger.Notify(nowMs, npc.Id.Value, npc.Household?.Value ?? 0, NoticeKind, $"{head} ({(int)Math.Round(value)}%)", (long)Math.Round(value), null, catalog.Rules.NoticeKeep);
            world.CareersChanged();
        }

        if (!FreeWill(world, npc) || npc.ControlledAction is { } order && order != ActionType.Idle || PlayerTasks.Of(world, npc) is not null || npc.UseQueue is { Count: > 0 }) return false;
        foreach (var (id, _, _) in critical)
        {
            if (ObjectUsage.TryCritical(world, ctx, npc, id) is not { } use) continue;
            ledger.Notify(nowMs, npc.Id.Value, npc.Household?.Value ?? 0, AutoKind, $"{npc.GivenName} cuidou de si: {use}.", 0, null, catalog.Rules.NoticeKeep);
            world.CareersChanged();
            return true;
        }

        return false;
    }
}
