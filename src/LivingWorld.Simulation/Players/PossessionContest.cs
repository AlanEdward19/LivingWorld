using System.Globalization;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
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

    /// <summary>Força do controle sobre <paramref name="npc"/> agora: a do poder que o possui (AD-197g) ou a do jogador.</summary>
    public static double Control(WorldState world, Npc npc, long tick)
    {
        long since = npc.Will is { Controlled: true } will ? will.SinceTick : tick;
        return PowerControl(world, npc, tick - since) ?? PlayerControl(world, tick - since);
    }

    /// <summary>Jogador: <see cref="PossessionRules.ControlBase"/> mais a perícia que ganha com as horas naquele corpo.</summary>
    internal static double PlayerControl(WorldState world, long hours)
    {
        var rules = Rules(world);
        return rules.ControlBase + rules.ControlGrowth * Math.Min(1.0, hours / (double)Math.Max(1, rules.ControlGrowthHours));
    }

    /// <summary>AD-197g, poder: a mesma conta do jogador, com a perícia antecipada pelo custo do poder (cada declaração de custo do
    /// descritor equivale a uma perícia inteira: quem paga mais, controla mais) e multiplicada pela confiabilidade (1 − risco do
    /// descritor: <c>Guaranteed</c> 0,9; <c>ResolutionCheck</c> 0,5 menos 0,1 por modo de falha). Nenhum número novo: risco e custo
    /// são os que <c>PowerOpportunity</c> já deriva do descritor. Nulo se ninguém possui o corpo por poder.</summary>
    internal static double? PowerControl(WorldState world, Npc npc, long hours)
    {
        if (LivingWorld.Simulation.Extraordinary.Mechanics.ControlMechanic.PossessingPower(world, npc) is not { } power) return null;
        var rules = Rules(world);
        double risk = LivingWorld.Simulation.Extraordinary.Opportunity.PowerOpportunity.EstimateRisk(power);
        double cost = (double)LivingWorld.Simulation.Extraordinary.Opportunity.PowerOpportunity.EstimateCost(power);
        double skill = Math.Min(1.0, cost + hours / (double)Math.Max(1, rules.ControlGrowthHours));
        return Math.Max(0, 1 - risk) * (rules.ControlBase + rules.ControlGrowth * skill);
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
    public static double HarmWeight(WorldState world, Npc npc, Npc target) => OrderWeight(world, npc, OrderHarm.Hurt(target));

    /// <summary>AD-197d: peso (0..100) de uma ordem qualquer contra o que a pessoa valoriza, pelo que ela faz e não pelo nome da ação:
    /// o maior entre o cuidado com cada pessoa ferida vezes o quanto a ordem a fere (vínculo × lealdade, <see cref="PossessionWill"/>),
    /// o altruísmo dela na medida do pior dano a alguém (quem ajuda a todos recusa ferir qualquer um) e, se a ordem tira a casa
    /// dela, o apego à segurança (aversão a risco) na medida de <see cref="PossessionRules.HomeValue"/>.</summary>
    public static double OrderWeight(WorldState world, Npc npc, OrderHarm harm)
    {
        var rules = Rules(world);
        double weight = 0, worst = 0;
        foreach (var (person, severity) in harm.Harmed)
        {
            weight = Math.Max(weight, PossessionWill.CareFor(world, npc, person) * severity);
            worst = Math.Max(worst, severity);
        }
        weight = Math.Max(weight, npc.Personality.Altruism * rules.AltruismHarmWeight * worst);
        if (harm.LosesHome) weight = Math.Max(weight, npc.Personality.RiskAversion * rules.HomeValue);
        return Math.Clamp(weight, 0, 100);
    }

    /// <summary>A pessoa recusa ferir <paramref name="target"/> agora? (Só na posse comum; a plena nunca recusa.)</summary>
    public static bool Refuses(WorldState world, Npc npc, Npc target, long tick) => RefusesOrder(world, npc, OrderHarm.Hurt(target), tick);

    /// <summary>AD-197d: a pessoa recusa a ordem agora? Na posse plena nunca. Sem a disputa de vontade (AD-197e) pesa só o vínculo;
    /// com ela, peso a partir de <see cref="PlayerRules.RefuseHarmCare"/> (o limiar de 60 do dono) e resistência × peso / 100 acima
    /// de <see cref="PossessionRules.RefuseFactor"/> × controle.</summary>
    public static bool RefusesOrder(WorldState world, Npc npc, OrderHarm harm, long tick)
    {
        var rules = world.Players;
        if (!rules.Enabled || npc.ControlMode == PossessionMode.Full) return false;
        if (!rules.Possession.WillContest) // AD-197e: só o vínculo
            return harm.Harmed.Any(h => PossessionWill.CareFor(world, npc, h.Person) * h.Severity >= rules.RefuseHarmCare);
        double weight = OrderWeight(world, npc, harm);
        return weight >= rules.RefuseHarmCare
            && Resistance(world, npc, tick) * weight / 100 > rules.Possession.RefuseFactor * Control(world, npc, tick);
    }

    /// <summary>Vontade bastante impede a entrada (posse comum do jogador): contra o controle inicial dele.</summary>
    public static bool Prevents(WorldState world, Npc npc, long tick) => Prevents(world, npc, tick, PlayerControl(world, 0));

    /// <summary>Vontade bastante impede a entrada contra um controle inicial dado (o do jogador ou o do poder, AD-197g).</summary>
    public static bool Prevents(WorldState world, Npc npc, long tick, double initialControl) =>
        On(world) && Resistance(world, npc, tick) >= Rules(world).PreventFactor * initialControl;

    /// <summary>A posse comum começou: a disputa parte da resistência de agora (com o cansaço que sobrou). Nada é agendado: a
    /// checagem roda quando o possuído desperta (<see cref="OnWake"/>).</summary>
    internal static void Begin(WorldState world, TickContext ctx, Npc npc) => Begin(world, npc, ctx.CurrentTick);

    /// <summary>Começa a disputa (jogador ou poder). Se a pessoa já disputa contra outro controle (um personagem de jogador possuído
    /// por poder), a resistência é a mesma pessoa: a disputa segue de onde está.</summary>
    internal static void Begin(WorldState world, Npc npc, long now)
    {
        if (!On(world) || npc.Will is { Controlled: true }) return;
        long nowMs = WorldMillis.FromHours(now);
        npc.SetWill(new WillContest(nowMs, Resistance(world, npc, now), nowMs, Controlled: true, LastCheckMs: nowMs));
    }

    /// <summary>A posse acabou (saída, expulsão): o cansaço fica e recupera solto; sem cansaço não sobra nada para guardar (a posse
    /// de zero horas deixa o mundo como o de quem nunca foi possuído, AD-196: sair é só desaparecer).</summary>
    internal static void End(WorldState world, Npc npc, long tick)
    {
        if (npc.Will is not { Controlled: true }) return;
        double left = Resistance(world, npc, tick);
        npc.SetWill(left >= Willpower(world, npc, tick) && npc.Will.LastPersuadedTick < 0
            ? null
            : new WillContest(npc.Will.SinceMs, left, WorldMillis.FromHours(tick), Controlled: false));
    }

    internal static void Shift(WorldState world, Npc npc, long tick, double delta, bool persuaded = false)
    {
        if (npc.Will is not { Controlled: true } contest) return;
        double now = Math.Clamp(Resistance(world, npc, tick) + delta, 0, 100);
        long tickMs = WorldMillis.FromHours(tick);
        npc.SetWill(contest with { Resistance = now, AtMs = tickMs, LastPersuadedMs = persuaded ? tickMs : contest.LastPersuadedMs });
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
        if (npc.ControllerId is null || npc.ControlMode == PossessionMode.Full) return;
        if (LivingWorld.Simulation.Extraordinary.Mechanics.ControlMechanic.PossessingPower(world, npc) is not null) return; // o poder conduz
        if (!Struggles(world, npc, now, out double resistance, out double control, out long since)) return;
        End(world, npc, now);
        npc.ReleaseController();
        Expelled(world, ctx, npc, now, since, resistance, control);
    }

    /// <summary>AD-197g: checagem da posse por poder, a cada hora do sistema extraordinário (no máximo a cada
    /// <see cref="PossessionRules.CheckEveryHours"/>). Verdadeiro se a vontade expulsou o poder (quem chama solta o corpo).</summary>
    internal static bool StrugglesAgainstPower(WorldState world, Npc npc, long now)
    {
        if (!Struggles(world, npc, now, out _, out _, out _)) return false;
        if (npc.ControllerId is null) End(world, npc, now);
        else npc.SetWill(npc.Will! with { Struggle = 0, LastCheckMs = WorldMillis.FromHours(now) }); // personagem de jogador: o jogador segue no corpo
        if (world.NeedsRules.Psyche.Enabled)
            PsycheMechanic.Experience(world, npc, LivingWorld.Domain.Cognition.LifeEventKind.Possessed, 0, now, 0.3, world.Players.Possessed);
        return true;
    }

    /// <summary>A regra única de expulsar: resistência ≥ fator × controle em checagens seguidas. Atualiza a contagem.</summary>
    private static bool Struggles(WorldState world, Npc npc, long now, out double resistance, out double control, out long since)
    {
        resistance = control = 0;
        since = 0;
        if (!On(world) || npc.Will is not { Controlled: true } contest
            || now - contest.LastCheckTick < Math.Max(1, Rules(world).CheckEveryHours))
            return false;
        since = contest.SinceTick;
        resistance = Resistance(world, npc, now);
        control = Control(world, npc, now);
        int struggle = resistance >= Rules(world).ExpelFactor * control ? contest.Struggle + 1 : 0;
        if (struggle >= Rules(world).ExpelChecks) return true;
        npc.SetWill(contest with { Struggle = struggle, LastCheckMs = WorldMillis.FromHours(now) });
        return false;
    }

    private static void Expelled(WorldState world, TickContext ctx, Npc npc, long now, long since, double resistance, double control)
    {
        if (world.NeedsRules.Psyche.Enabled)
            PsycheMechanic.Experience(world, npc, LivingWorld.Domain.Cognition.LifeEventKind.Possessed, 0, now, 0.3, world.Players.Possessed);
        ctx.LogEvent(WorldEventKind.PossessionExpelled,
            string.Create(CultureInfo.InvariantCulture, $"{npc.Id.Value}|{now - since}|{resistance:0}|{control:0}"), PossessionSystem.SystemName);
    }
}

/// <summary>AD-197d: o que uma ordem faz às pessoas e à vida da pessoa possuída: quem ela fere e quanto (0..1) e se tira a casa dela.
/// O peso contra os valores sai daqui (<see cref="PossessionContest.OrderWeight"/>), sem lista de ações.</summary>
public sealed record OrderHarm(IReadOnlyList<(Npc Person, double Severity)> Harmed, bool LosesHome = false)
{
    /// <summary>Ferir alguém com as próprias mãos (golpe): o dano inteiro nele.</summary>
    public static OrderHarm Hurt(Npc target) => new([(target, 1.0)]);
}
