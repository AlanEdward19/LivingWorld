using System.Globalization;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

/// <summary>A posse comum como disputa de vontade (AD-197f, decisão do dono: "ela tem que ser mais forte que o controle da pessoa").
/// Uma regra só, sem sorteio, para qualquer possuído (NPC hoje, personagem de jogador no futuro):
/// <list type="bullet">
/// <item><b>Força de vontade</b> (0..100) da pessoa: estabilidade emocional, conscienciosidade e pouca impulsividade, na medida do
/// corpo (saúde) e da mente (medo e luto a enfraquecem).</item>
/// <item><b>Força do controle</b> de quem possui: o jogador começa em <see cref="PossessionRules.ControlBase"/> e ganha perícia com o
/// tempo naquele corpo até <see cref="PossessionRules.ControlGrowth"/> a mais. A posse por poder (Fase 16) entra pela mesma conta
/// quando for ligada aqui, com a força tirada da confiabilidade do descritor do poder (issue).</item>
/// <item><b>Resistência</b>: começa na vontade (menos o cansaço que sobrou de uma posse anterior), cansa sob controle e recupera
/// solta; quem possui pode persuadir (tira resistência, uma vez por hora, na escala da amabilidade da pessoa); quem a apoia de
/// fora devolve resistência (<see cref="PossessionWitness"/>).</item>
/// <item><b>Ordem contra os valores</b>: o peso de ferir alguém é o maior entre o cuidado com aquela pessoa (vínculo × lealdade,
/// AD-197e) e o altruísmo dela (quem ajuda a todos recusa ferir qualquer um). Com peso a partir de
/// <see cref="PlayerRules.RefuseHarmCare"/> ela recusa se resistência × peso / 100 passar de
/// <see cref="PossessionRules.RefuseFactor"/> × controle.</item>
/// <item><b>Impedir e expulsar</b>: vontade a partir de <see cref="PossessionRules.PreventFactor"/> × controle inicial impede a
/// entrada; resistência acima de <see cref="PossessionRules.ExpelFactor"/> × controle em checagens seguidas expulsa.</item>
/// </list></summary>
public static class PossessionContest
{
    internal static PossessionRules Rules(WorldState world) => world.Players.Possession;

    internal static bool On(WorldState world) => world.Players.Enabled && Rules(world).WillContest;

    public static double Willpower(WorldState world, Npc npc, long tick)
    {
        var p = npc.Personality;
        double trait = 0.45 * p.EmotionalStability + 0.35 * p.Conscientiousness + 0.20 * (100 - p.Impulsivity);
        var affect = PsycheMechanic.AffectOf(world, npc, tick);
        double mind = 1 - 0.4 * Math.Clamp(Math.Max(affect.Fear, affect.Grief), 0, 1);
        double body = 0.5 + 0.5 * npc.Health / 100.0;
        return Math.Clamp(trait * mind * body, 0, 100);
    }

    public static double Control(WorldState world, Npc npc, long tick)
    {
        var rules = Rules(world);
        long since = npc.Will is { Controlled: true } will ? will.SinceTick : tick;
        double skill = Math.Min(1.0, (tick - since) / (double)Math.Max(1, rules.ControlGrowthHours));
        return rules.ControlBase + rules.ControlGrowth * skill;
    }

    /// <summary>Resistência agora: cansa sob controle (na medida do controle), recupera solta até a vontade.</summary>
    public static double Resistance(WorldState world, Npc npc, long tick)
    {
        var rules = Rules(world);
        double will = Willpower(world, npc, tick);
        if (npc.Will is not { } contest) return will;
        long hours = Math.Max(0, tick - contest.AtTick);
        if (contest.Controlled)
            return Math.Max(0, contest.Resistance - rules.DrainPerHour * hours * Control(world, npc, tick) / rules.ControlBase);
        return Math.Min(will, contest.Resistance + rules.RecoverPerHour * hours);
    }

    /// <summary>Peso (0..100) de uma ordem de ferir <paramref name="target"/> contra os valores e os vínculos da pessoa.</summary>
    public static double HarmWeight(WorldState world, Npc npc, Npc target) =>
        Math.Max(PossessionWill.CareFor(world, npc, target), npc.Personality.Altruism * Rules(world).AltruismHarmWeight);

    /// <summary>A pessoa recusa ferir <paramref name="target"/> agora? (Só na posse comum; a plena nunca recusa.)</summary>
    public static bool Refuses(WorldState world, Npc npc, Npc target, long tick)
    {
        var rules = world.Players;
        if (!rules.Enabled) return false;
        if (!rules.Possession.WillContest) return PossessionWill.CareFor(world, npc, target) >= rules.RefuseHarmCare; // AD-197e
        double weight = HarmWeight(world, npc, target);
        return weight >= rules.RefuseHarmCare
            && Resistance(world, npc, tick) * weight / 100 > rules.Possession.RefuseFactor * Control(world, npc, tick);
    }

    /// <summary>Vontade bastante impede a entrada (posse comum).</summary>
    public static bool Prevents(WorldState world, Npc npc, long tick) =>
        On(world) && Resistance(world, npc, tick) >= Rules(world).PreventFactor * Rules(world).ControlBase;

    /// <summary>A posse comum começou: a disputa parte da resistência de agora (com o cansaço que sobrou). Nada é agendado: a
    /// checagem roda quando o possuído desperta (<see cref="OnWake"/>).</summary>
    internal static void Begin(WorldState world, TickContext ctx, Npc npc)
    {
        if (!On(world)) return;
        long now = ctx.CurrentTick;
        npc.SetWill(new WillContest(now, Resistance(world, npc, now), now, Controlled: true, LastCheckTick: now));
    }

    /// <summary>A posse acabou (saída, expulsão): o cansaço fica e recupera solto; sem cansaço não sobra nada para guardar (a posse
    /// de zero horas deixa o mundo como o de quem nunca foi possuído, AD-196: sair é só desaparecer).</summary>
    internal static void End(WorldState world, Npc npc, long tick)
    {
        if (npc.Will is not { Controlled: true }) return;
        double left = Resistance(world, npc, tick);
        npc.SetWill(left >= Willpower(world, npc, tick) && npc.Will.LastPersuadedTick < 0
            ? null
            : new WillContest(npc.Will.SinceTick, left, tick, Controlled: false));
    }

    internal static void Shift(WorldState world, Npc npc, long tick, double delta, bool persuaded = false)
    {
        if (npc.Will is not { Controlled: true } contest) return;
        double now = Math.Clamp(Resistance(world, npc, tick) + delta, 0, 100);
        npc.SetWill(contest with { Resistance = now, AtTick = tick, LastPersuadedTick = persuaded ? tick : contest.LastPersuadedTick });
    }

    /// <summary>Quem possui tenta convencer a pessoa a ceder: tira resistência (amabilidade dela), uma vez por hora.</summary>
    public static bool Persuade(WorldState world, Npc npc, long tick)
    {
        if (!On(world) || npc.Will is not { Controlled: true } contest || contest.LastPersuadedTick == tick) return false;
        Shift(world, npc, tick, -Rules(world).PersuadeAmount * (0.5 + npc.Personality.Agreeableness / 200.0), persuaded: true);
        return true;
    }

    /// <summary>Checagem quando o possuído desperta, no máximo a cada <see cref="PossessionRules.CheckEveryHours"/>: vontade acima do
    /// controle conta; seguidas o bastante, expulsa.</summary>
    internal static void OnWake(WorldState world, TickContext ctx, Npc npc, long now)
    {
        if (!On(world) || npc.Will is not { Controlled: true } contest || npc.ControllerId is null || npc.ControlMode == PossessionMode.Full
            || now - contest.LastCheckTick < Math.Max(1, Rules(world).CheckEveryHours))
            return;
        long since = contest.SinceTick;
        double resistance = Resistance(world, npc, now), control = Control(world, npc, now);
        int struggle = resistance >= Rules(world).ExpelFactor * control ? contest.Struggle + 1 : 0;
        if (struggle >= Rules(world).ExpelChecks)
        {
            End(world, npc, now);
            npc.ReleaseController();
            if (world.NeedsRules.Psyche.Enabled)
                PsycheMechanic.Experience(world, npc, LivingWorld.Domain.Cognition.LifeEventKind.Possessed, 0, now, 0.3, world.Players.Possessed);
            ctx.LogEvent(WorldEventKind.PossessionExpelled,
                string.Create(CultureInfo.InvariantCulture, $"{npc.Id.Value}|{now - since}|{resistance:0}|{control:0}"), PossessionSystem.SystemName);
            return;
        }
        npc.SetWill(contest with { Struggle = struggle, LastCheckTick = now });
    }
}
