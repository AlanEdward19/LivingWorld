using System.Globalization;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

/// <summary>Estranhamento, descoberta e reação à posse (AD-197f, decisão do dono: "se agir imitando igual ninguém repara, se agir
/// diferente podem estranhar"; "nada hardcoded"). Vale para qualquer corpo conduzido de fora (posse comum ou plena), sem sorteio:
/// <list type="bullet">
/// <item><b>Desvio</b>: a ação pedida comparada com o costume da pessoa naquela hora (a rotina do ofício e da idade): igual não
/// desvia; diferente desvia <see cref="PossessionRules.OffRoutineDeviation"/>. Um golpe desvia na medida do quanto ferir vai
/// contra os valores dela (no mínimo <see cref="PossessionRules.StrikeDeviation"/>).</item>
/// <item><b>Quem repara</b>: quem convive com ela (mora junto) e quem tem vínculo com ela e está na mesma célula, na medida do
/// vínculo; quem vigia repara sempre. O estranhamento fica no <see cref="PossessionLedger"/>.</item>
/// <item><b>Boato</b>: passado <see cref="PossessionRules.RumorAt"/>, o observador comenta: quem mora com ele ouve, pela metade.</item>
/// <item><b>Conclusão</b> (crença, não verdade): estranhamento a partir de <see cref="PossessionRules.ConcludeAt"/> / crença do
/// lugar (o extraordinário existe no mundo: 1; não: 0,5; o cenário pode declarar).</item>
/// <item><b>Reação</b> de quem conclui: quem se importa com a pessoa apoia (a resistência dela sobe e ela pode expulsar a posse);
/// quem não se importa e é violento ataca o corpo (a briga e a justiça de sempre); o resto vigia. Ritual fica para quando o mundo
/// tiver religião e ofício de quem trata (issue).</item>
/// </list></summary>
public static class PossessionWitness
{
    private static bool On(WorldState world) => world.Players.Enabled && world.Players.Possession.Strangeness;

    /// <summary>O controlador pediu <paramref name="action"/>: desvia do costume da pessoa nesta hora?</summary>
    internal static void OnAction(WorldState world, TickContext ctx, Npc body, ActionType? action)
    {
        if (!On(world) || action is not { } chosen) return;
        var stage = world.LifeStageRules.LifeStageOf(body.AgeYears(world.CurrentDate));
        var routine = world.ActionCatalog.RoutineOf(body.Profession, stage, world.CurrentDate.Hour);
        if (chosen == routine) return; // imitou: ninguém repara
        Observe(world, ctx, body, world.Players.Possession.OffRoutineDeviation);
    }

    /// <summary>O corpo golpeou <paramref name="target"/>: desvio na medida dos valores da pessoa.</summary>
    internal static void OnStrike(WorldState world, TickContext ctx, Npc body, Npc target)
    {
        if (!On(world)) return;
        Observe(world, ctx, body, Math.Max(world.Players.Possession.StrikeDeviation, PossessionContest.HarmWeight(world, body, target)));
    }

    /// <summary>AD-197d: o corpo cumpriu uma ordem contra o que a pessoa valoriza (vender a casa, abandonar o filho): desvio na medida
    /// do peso dela, como o golpe.</summary>
    internal static void OnOrder(WorldState world, TickContext ctx, Npc body, double weight)
    {
        if (!On(world)) return;
        Observe(world, ctx, body, Math.Max(world.Players.Possession.StrikeDeviation, weight));
    }

    private static double Belief(WorldState world) =>
        Math.Clamp(world.Players.Possession.Belief ?? (world.Extraordinary.Enabled ? 1.0 : 0.5), 0.05, 1.0);

    private static void Observe(WorldState world, TickContext ctx, Npc body, double deviation)
    {
        if (deviation <= 0) return;
        var book = world.PossessionBook();
        var home = body.Household;
        foreach (var other in world.AliveNpcIndex.Alive)
        {
            if (other.Id == body.Id || other.City != body.City) continue;
            bool together = home is not null && other.Household == home;
            double familiarity = together ? 1.0 : Familiarity(world, other, body);
            bool vigil = book.Suspicions.Count > 0 && Find(book, other.Id.Value, body.Id.Value) is { Vigil: true };
            if (!vigil && (familiarity <= 0 || (!together && other.CurrentLocation != body.CurrentLocation))) continue;
            Add(world, ctx, book, other, body, deviation * Math.Max(familiarity, vigil ? 1.0 : 0));
        }
    }

    private static Suspicion? Find(PossessionLedger book, long observer, long body) =>
        book.Suspicions.FirstOrDefault(s => s.Observer == observer && s.Body == body);

    /// <summary>Quanto <paramref name="observer"/> conhece o jeito de <paramref name="body"/> (0..1): o laço que tem com ele, a partir
    /// do vínculo próximo da AD-196.</summary>
    private static double Familiarity(WorldState world, Npc observer, Npc body)
    {
        if (!world.Relationships.TryGetValue(new RelationshipKey(observer.Id, body.Id), out var tie)) return 0;
        double bond = (tie.Trust + tie.Affection) / 2;
        return bond >= Disappearance.CloseBond ? bond / 100.0 : 0;
    }

    private static void Add(WorldState world, TickContext ctx, PossessionLedger book, Npc observer, Npc body, double amount)
    {
        var rules = world.Players.Possession;
        var s = book.Of(observer.Id.Value, body.Id.Value);
        s.Value += amount;
        s.Tick = ctx.CurrentTick;
        if (!s.Rumored && s.Value >= rules.RumorAt)
        {
            s.Rumored = true;
            ctx.LogEvent(WorldEventKind.PossessionSuspected,
                string.Create(CultureInfo.InvariantCulture, $"{observer.Id.Value}|{body.Id.Value}|{s.Value:0}"), PossessionSystem.SystemName);
            if (observer.Household is { } oh && world.FindHousehold(oh) is { } house)
                foreach (var member in house.Members.OrderBy(m => m.Value))
                    if (member != observer.Id && member != body.Id && world.FindNpc(member) is { IsAlive: true } listener)
                        Add(world, ctx, book, listener, body, s.Value / 2); // o boato chega a quem mora com ele
        }
        if (!s.Concluded && s.Value >= rules.ConcludeAt / Belief(world)) Conclude(world, ctx, s, observer, body);
    }

    private static void Conclude(WorldState world, TickContext ctx, Suspicion s, Npc observer, Npc body)
    {
        s.Concluded = true;
        long now = ctx.CurrentTick;
        ctx.LogEvent(WorldEventKind.PossessionConcluded,
            string.Create(CultureInfo.InvariantCulture, $"{observer.Id.Value}|{body.Id.Value}|{s.Value:0}"), PossessionSystem.SystemName);
        world.AddNpcMemory(observer.Id, MemoryCategory.Episodic, $"{observer.Name} acredita que {body.Name} não é mais {body.Name}: está possuído",
            80, now, [body.Id], observer.CurrentLocation, LlmRules.Default.CanonicalMemoryImportanceThreshold);
        var rules = world.Players.Possession;
        if (!world.Players.Enabled || !rules.Reaction) return;

        double care = PossessionWill.CareFor(world, observer, body);
        string how;
        if (care >= rules.HelpCare)
        {
            how = "apoio"; // fica perto, chama a pessoa pelo nome: a vontade dela ganha força
            PossessionContest.Shift(world, body, now, rules.SupportBoost);
            PsycheMechanic.Experience(world, observer, LivingWorld.Domain.Cognition.LifeEventKind.KinDisappeared, body.Id.Value, now, care / 100.0,
                world.Players.KinDisappeared); // perdeu a pessoa para outra vontade: luto e medo como no sumiço
        }
        else if (world.CrimeOn
                 && LivingWorld.Simulation.Crime.Violence.Propensity(world, world.CrimeRules!.Violence, observer, now) >= rules.ViolentPropensity)
        {
            how = "ataque";
            if (observer.CurrentLocation == body.CurrentLocation || (observer.Household is { } oh && oh == body.Household))
                LivingWorld.Simulation.Crime.Violence.Fight(world, ctx, observer, body, "possessao",
                    ctx.OneShot($"possession-strike-{observer.Id.Value}-{body.Id.Value}-{now}"));
            else s.Vigil = true; // longe: fica de olho até achar o corpo
        }
        else
        {
            how = "vigilia";
            s.Vigil = true;
        }
        ctx.LogEvent(WorldEventKind.PossessionConfronted, $"{observer.Id.Value}|{body.Id.Value}|{how}", PossessionSystem.SystemName);
    }
}
