using System.Globalization;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Crime;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

/// <summary>#234: o que o cliente precisa para animar um golpe: o que houve (<see cref="CombatOutcome"/>), o dano, se matou, o que o
/// alvo decidiu (<see cref="CombatReaction"/>) e em quanto tempo o corpo golpeia de novo.</summary>
public sealed record StrikeResult(
    long TargetId, CombatOutcome Outcome, int Damage, bool Lethal, CombatReaction Reaction, long ReactionUntilMs, long ReadyInMs);

/// <summary>#234: o último golpe recebido (resultado, dano, de quem, há quantos ms).</summary>
public sealed record IncomingView(string Outcome, int Damage, long By, long AgoMs);

/// <summary>#234: o que o cliente precisa do combate do corpo: recarga do golpe, guarda em curso e sua recarga, o último golpe
/// recebido e o alcance do golpe em metros.</summary>
public sealed record CombatView(long StrikeReadyInMs, string? Guard, long GuardMs, long GuardReadyInMs, IncomingView? LastIncoming, double ReachMeters);

/// <summary>#234: combate com tempo. Alcance em metros (célula de 1,5 m), recarga do golpe, esquiva e bloqueio do corpo do jogador, e a
/// reação de quem apanha decidida pelo motor (recuar, fugir, revidar, pedir ajuda) a partir de traços, saúde, medo, vínculo com o
/// agressor e força relativa. O revide (e a ajuda de quem acode) é um golpe pendente que sai depois de um tempo (a janela em que a
/// guarda do agressor vale) e fere pelo mesmo caminho do golpe do jogador (<see cref="Violence.Fight"/>). Só roda para quem foi
/// atingido: a decisão é tomada na hora do golpe e o golpe pendente é resolvido quando o NPC desperta (<see cref="OnWake"/>); nada
/// por tick. Sorteio só de <c>ctx.OneShot</c>; tempo só do relógio do mundo.</summary>
public static class PlayerCombat
{
    private const int MaxHelpers = 2;
    private const double HelperRangeCells = 4;

    private static long Now(WorldState world) => world.CurrentDate.TotalMilliseconds;

    private static double Capacity(WorldState world, Npc npc) => Math.Clamp(BodyMechanic.WorkCapacityMultiplier(world, npc), 0.8, 1.2);

    /// <summary>Posição do corpo em células (fracionária): a pose fina de quem anda solto, senão o centro da célula da pose.</summary>
    internal static (double X, double Y) Where(WorldState world, Npc npc)
    {
        var cell = world.AliveNpcIndex.PoseOf(npc) ?? npc.CurrentLocation; // a mesma pose do alcance em células (TravelPose.Reach)
        if (npc.Fine is { } fine && fine.CellX == cell.X && fine.CellY == cell.Y)
            return (fine.X / (double)FinePose.PerCell, fine.Y / (double)FinePose.PerCell);
        return (cell.X + 0.5, cell.Y + 0.5);
    }

    /// <summary>Distância (m) entre dois corpos agora.</summary>
    internal static double MetersBetween(WorldState world, Npc a, Npc b)
    {
        var (ax, ay) = Where(world, a);
        var (bx, by) = Where(world, b);
        double dx = ax - bx, dy = ay - by;
        return Math.Sqrt(dx * dx + dy * dy) * world.Players.CellM; // a raiz é exata (IEEE): vale na regra de determinismo
    }

    private static bool InReach(WorldState world, Npc a, Npc b) => MetersBetween(world, a, b) <= world.Players.StrikeReachM;

    /// <summary>Por que o golpe não sai agora (alcance, recarga), ou nulo. O alcance recusa com 400, a recarga com 409.</summary>
    public static string? StrikeRefusal(WorldState world, Npc npc, Npc target)
    {
        if (!world.Players.CombatOn) return null;
        double meters = MetersBetween(world, npc, target);
        if (meters > world.Players.StrikeReachM)
            return string.Create(CultureInfo.InvariantCulture,
                $"strike: alvo fora do alcance (a {meters:0.0} m; o golpe alcança {world.Players.StrikeReachM:0.0} m)");
        long wait = (npc.Combat?.StrikeReadyAtMs ?? 0) - Now(world);
        return wait > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{PlayerCommands.Conflict} strike: {npc.Name} ainda se recupera do golpe (faltam {wait} ms)")
            : null;
    }

    /// <summary>O golpe do jogador já aceito (alcance e vontade conferidos): fere pelo caminho da briga, marca a recarga e deixa o alvo
    /// vivo decidir o que faz.</summary>
    internal static StrikeResult Strike(WorldState world, TickContext ctx, Npc body, Npc target)
    {
        long now = Now(world);
        var rules = world.Players;
        if (rules.CombatOn)
            body.SetCombat((body.Combat ?? new CombatState()) with
            {
                StrikeReadyAtMs = now + (long)Math.Ceiling(rules.StrikeCooldownMs / Capacity(world, body)),
            });
        var (harm, lethal) = Violence.Fight(world, ctx, body, target, "jogador",
            ctx.OneShot($"player-strike-{body.Id.Value}-{target.Id.Value}-{ctx.CurrentTick}"));
        var reaction = CombatReaction.None;
        long until = 0;
        if (rules.CombatOn && target.IsAlive)
        {
            NoteIncoming(target, CombatOutcome.Hit, harm, body.Id.Value, now);
            reaction = React(world, ctx, target, body, now);
            until = target.Combat?.ReactionUntilMs ?? 0;
        }
        return new StrikeResult(target.Id.Value, CombatOutcome.Hit, harm, lethal, reaction, until, Math.Max(0, (body.Combat?.StrikeReadyAtMs ?? 0) - now));
    }

    // --- guarda do jogador ---

    /// <summary>Levanta a guarda (esquiva ou bloqueio) por uma janela curta; a guarda recarrega antes de outra.</summary>
    public static Result<Unit> Guard(WorldState world, Npc body, CombatGuard guard)
    {
        string name = guard == CombatGuard.Dodge ? "dodge" : "block";
        var rules = world.Players;
        if (!rules.CombatOn) return Result<Unit>.Fail($"{PlayerCommands.Conflict} {name}: combate com tempo desligado no cenário");
        long now = Now(world);
        long wait = (body.Combat?.GuardReadyAtMs ?? 0) - now;
        if (wait > 0)
            return Result<Unit>.Fail(string.Create(CultureInfo.InvariantCulture,
                $"{PlayerCommands.Conflict} {name}: a guarda ainda se recupera (faltam {wait} ms)"));
        if (guard == CombatGuard.Dodge && body.SleepNeed.ValueAtMs(now) < rules.JumpMinSleep)
            return Result<Unit>.Fail($"{PlayerCommands.Conflict} {name}: {body.Name} está exausto demais para esquivar");
        int window = guard == CombatGuard.Dodge ? rules.DodgeWindowMs : rules.BlockWindowMs;
        body.SetCombat((body.Combat ?? new CombatState()) with
        {
            Guard = guard,
            GuardFromMs = now,
            GuardUntilMs = now + window,
            GuardReadyAtMs = now + rules.GuardCooldownMs,
        });
        return Result<Unit>.Ok(Unit.Value);
    }

    private static void NoteIncoming(Npc who, CombatOutcome outcome, int damage, long by, long atMs) =>
        who.SetCombat((who.Combat ?? new CombatState()) with
        {
            LastIncoming = outcome,
            LastIncomingDamage = damage,
            LastIncomingBy = by,
            LastIncomingAtMs = atMs,
        });

    /// <summary>Um golpe de <paramref name="attacker"/> em <paramref name="victim"/> no instante <paramref name="atMs"/>: a guarda de
    /// quem apanha vale se o golpe cai na janela (esquiva: chance pelo corpo e pelo cansaço; bloqueio: passa só uma fração); o que passa
    /// fere pelo caminho de sempre, sem abrir caso (é revide ou ajuda).</summary>
    internal static (CombatOutcome Outcome, int Damage, bool Lethal) Incoming(WorldState world, TickContext ctx, Npc attacker, Npc victim,
        long atMs, string motive)
    {
        var rules = world.Players;
        double factor = 1.0;
        var outcome = CombatOutcome.Hit;
        if (victim.Combat is { Guard: not CombatGuard.None } c && atMs >= c.GuardFromMs && atMs <= c.GuardUntilMs)
        {
            double body = Capacity(world, victim);
            if (c.Guard == CombatGuard.Dodge)
            {
                double sleep = victim.SleepNeed.ValueAtMs(atMs);
                double tired = sleep >= rules.Tired ? 1.0 : 0.5 + 0.5 * sleep / Math.Max(1.0, rules.Tired);
                double chance = Math.Clamp(rules.DodgeChance * body * tired, 0.05, 0.95);
                var roll = ctx.OneShot($"combat-dodge-{attacker.Id.Value}-{victim.Id.Value}-{atMs}");
                if (roll.NextDouble() < chance) outcome = CombatOutcome.Dodged;
            }
            else
            {
                factor = Math.Clamp(rules.BlockPasses / body, 0.1, 0.9);
                outcome = CombatOutcome.Blocked;
            }
        }
        if (outcome == CombatOutcome.Dodged)
        {
            NoteIncoming(victim, CombatOutcome.Dodged, 0, attacker.Id.Value, atMs);
            return (outcome, 0, false);
        }
        var (harm, lethal) = Violence.Fight(world, ctx, attacker, victim, motive,
            ctx.OneShot($"combat-hit-{attacker.Id.Value}-{victim.Id.Value}-{atMs}"), factor, defense: true);
        if (victim.IsAlive) NoteIncoming(victim, outcome, harm, attacker.Id.Value, atMs);
        return (outcome, harm, lethal);
    }

    // --- reação de quem apanha ---

    /// <summary>A decisão (função dos traços, da saúde, do medo, do vínculo e da força): quem é forte, corajoso e briguento e não
    /// gosta do agressor revida; quem é fraco, assustado ou ferido foge; quem se importa com o agressor ou hesita recua. O sorteio só
    /// desempata de leve (±0,05).</summary>
    internal static CombatReaction Decide(WorldState world, TickContext ctx, Npc victim, Npc aggressor, long atMs)
    {
        var p = victim.Personality;
        double fear = world.NeedsRules.Psyche.Enabled ? PsycheMechanic.AffectOf(world, victim, ctx.CurrentTick).Fear : 0;
        double courage = Math.Clamp(((100 - p.RiskAversion) * 0.6 + p.EmotionalStability * 0.4) / 100.0 - 0.3 * fear, 0, 1);
        double wounded = Math.Clamp(1 - victim.Health / 100.0, 0, 1);
        double relative = Math.Clamp((victim.MuscleMass + 5) / Math.Max(5.0, aggressor.MuscleMass + 5), 0.4, 2.5);
        double strength = (relative - 0.4) / 2.1;
        double care = PossessionWill.CareFor(world, victim, aggressor) / 100.0;
        double propensity = Math.Clamp(Violence.Propensity(world, world.CrimeRules!.Violence, victim, ctx.CurrentTick) / 2.0, 0, 1);
        double impulse = p.Impulsivity / 100.0;
        var rng = ctx.OneShot($"combat-react-{victim.Id.Value}-{aggressor.Id.Value}-{atMs}");
        double Noise() => (rng.NextDouble() - 0.5) * 0.1;
        double retaliate = 0.35 * courage + 0.30 * strength + 0.20 * propensity + 0.15 * impulse - 0.6 * care - 0.6 * wounded + Noise();
        double flee = 0.45 * (1 - courage) + 0.25 * (1 - strength) + 0.5 * wounded + Noise();
        double recoil = 0.25 + 0.35 * care + 0.10 * (1 - impulse) + Noise();
        if (retaliate >= flee && retaliate >= recoil) return CombatReaction.Retaliate;
        return flee >= recoil ? CombatReaction.Flee : CombatReaction.Recoil;
    }

    private static CombatReaction React(WorldState world, TickContext ctx, Npc victim, Npc aggressor, long now)
    {
        var reaction = Decide(world, ctx, victim, aggressor, now);
        long until;
        switch (reaction)
        {
            case CombatReaction.Retaliate:
                until = ScheduleRetaliation(world, victim, aggressor, now);
                break;
            case CombatReaction.Flee:
                StepAway(world, ctx, victim, aggressor, 2);
                until = now + 2000;
                if (victim.Personality.Extroversion >= 40 && Helpers(world, ctx, victim, aggressor) is { Count: > 0 } helpers)
                {
                    reaction = CombatReaction.CallHelp; // grita por ajuda enquanto foge: quem se importa acode (e revida)
                    int threshold = LlmRules.Default.CanonicalMemoryImportanceThreshold;
                    foreach (var helper in helpers)
                    {
                        ScheduleRetaliation(world, helper, aggressor, now);
                        world.AddNpcMemory(helper.Id, MemoryCategory.Episodic, $"{helper.Name} ouviu {victim.Name} pedir ajuda e foi contra {aggressor.Name}",
                            70, ctx.CurrentTick, [victim.Id, aggressor.Id], helper.CurrentLocation, threshold);
                    }
                    world.AddNpcMemory(victim.Id, MemoryCategory.Episodic, $"{victim.Name} pediu ajuda: {aggressor.Name} o atacou",
                        70, ctx.CurrentTick, [aggressor.Id], victim.CurrentLocation, threshold);
                }
                break;
            default:
                StepAway(world, ctx, victim, aggressor, 1);
                until = now + 500;
                break;
        }
        victim.SetCombat((victim.Combat ?? new CombatState()) with { Reaction = reaction, ReactionUntilMs = until });
        if (reaction != CombatReaction.Retaliate) PlayerCommands.WakeNow(world, victim); // quem foge ou recua repensa já; o revide tem hora marcada
        return reaction;
    }

    /// <summary>Marca o revide (ou a ajuda): o golpe sai depois de um tempo, mais cedo em quem é impulsivo (0,5× a 1,5× do padrão), e o
    /// NPC desperta nesse instante.</summary>
    private static long ScheduleRetaliation(WorldState world, Npc who, Npc target, long now)
    {
        double calm = 1.5 - who.Personality.Impulsivity / 100.0;
        long at = now + Math.Max(1, (long)Math.Round(world.Players.RetaliateWindupMs * calm));
        who.SetCombat((who.Combat ?? new CombatState()) with
        {
            RetaliateAtMs = at,
            RetaliateTarget = target.Id.Value,
            Reaction = CombatReaction.Retaliate,
            ReactionUntilMs = at,
        });
        if (world.FineTime) world.ReplaceNpcWakeAtMs(who.Id.Value, at);
        return at;
    }

    /// <summary>Quem acode: vive perto, tem cuidado pela vítima a partir de <see cref="PossessionRules.HelpCare"/> (o de quem apoia a pessoa possuída) e não é pacato (propensão a briga de um terço da do violento); os mais próximos
    /// (o id desempata), no máximo <see cref="MaxHelpers"/>. Uma passada pelos vivos, só quando alguém pediu ajuda.</summary>
    private static List<Npc> Helpers(WorldState world, TickContext ctx, Npc victim, Npc aggressor)
    {
        var pose = world.AliveNpcIndex.PoseOf(victim) ?? victim.CurrentLocation;
        var found = new List<(double Distance, Npc Helper)>();
        foreach (var other in world.AliveNpcIndex.Alive)
        {
            if (other.Id == victim.Id || other.Id == aggressor.Id || other.City != victim.City) continue;
            if (other.Combat is { RetaliateAtMs: > 0 }) continue;
            var at = world.AliveNpcIndex.PoseOf(other) ?? other.CurrentLocation;
            double d = Math.Max(Math.Abs(at.X - pose.X), Math.Abs(at.Y - pose.Y));
            if (d > HelperRangeCells) continue;
            if (PossessionWill.CareFor(world, other, victim) < world.Players.Possession.HelpCare) continue;
            if (Violence.Propensity(world, world.CrimeRules!.Violence, other, ctx.CurrentTick) < world.Players.Possession.ViolentPropensity / 3) continue;
            found.Add((d, other));
        }
        return found.OrderBy(f => f.Distance).ThenBy(f => f.Helper.Id.Value).Take(MaxHelpers).Select(f => f.Helper).ToList();
    }

    /// <summary>Afasta a pessoa do agressor até <paramref name="cells"/> células, pela mesma regra de chão do passo do jogador (parede,
    /// porta, gente na célula). Quem caminhava para onde está; dentro de prédio não se afasta.</summary>
    private static void StepAway(WorldState world, TickContext ctx, Npc who, Npc from, int cells)
    {
        if (who.Interior is not null) return;
        var threat = Where(world, from);
        static double Sq(double a, double b) => (a - b) * (a - b);
        for (int i = 0; i < cells; i++)
        {
            var here = world.AliveNpcIndex.PoseOf(who) ?? who.CurrentLocation;
            CellCoord? best = null;
            double bestD = Sq(here.X + 0.5, threat.X) + Sq(here.Y + 0.5, threat.Y);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var cell = new CellCoord(here.X + dx, here.Y + dy);
                    double d = Sq(cell.X + 0.5, threat.X) + Sq(cell.Y + 0.5, threat.Y);
                    if (d <= bestD || !LivingWorld.Simulation.Visibility.PlayerMovementValidator.Validate(world, who, cell, here).IsSuccess) continue;
                    best = cell;
                    bestD = d;
                }
            if (best is not { } step) return;
            if (who.CurrentAction == ActionType.Travel) who.SetCurrentActionAtMs(ActionType.Idle, Now(world));
            who.MoveTo(step, ctx.CurrentTick);
        }
    }

    /// <summary>O NPC com revide pendente desperta: antes da hora, só reagenda; na hora, golpeia se o alvo ainda está ao alcance (senão
    /// erra) pelo mesmo caminho do golpe do jogador, com a guarda do alvo valendo no instante marcado.</summary>
    internal static void OnWake(WorldState world, TickContext ctx, Npc npc, long nowMs)
    {
        if (npc.Combat is not { RetaliateAtMs: > 0 } c) return;
        if (nowMs < c.RetaliateAtMs)
        {
            if (world.FineTime) world.ReplaceNpcWakeAtMs(npc.Id.Value, c.RetaliateAtMs);
            return;
        }
        npc.SetCombat(c with { RetaliateAtMs = 0, RetaliateTarget = 0 });
        if (!npc.IsAlive || !world.Players.CombatOn || world.FindNpc(new NpcId(c.RetaliateTarget)) is not { IsAlive: true } target) return;
        if (!InReach(world, npc, target))
        {
            NoteIncoming(target, CombatOutcome.Missed, 0, npc.Id.Value, c.RetaliateAtMs);
            return;
        }
        Incoming(world, ctx, npc, target, c.RetaliateAtMs, "revide");
    }

    /// <summary>A leitura do combate do corpo para o cliente animar, relativa a agora (ms); nula para quem nunca lutou.</summary>
    public static CombatView? View(WorldState world, Npc body)
    {
        if (body.Combat is not { } c) return null;
        long now = Now(world);
        bool guarding = c.Guard != CombatGuard.None && now <= c.GuardUntilMs;
        return new CombatView(
            Math.Max(0, c.StrikeReadyAtMs - now),
            guarding ? c.Guard.ToString() : null,
            guarding ? c.GuardUntilMs - now : 0,
            Math.Max(0, c.GuardReadyAtMs - now),
            c.LastIncoming == CombatOutcome.None
                ? null
                : new IncomingView(c.LastIncoming.ToString(), c.LastIncomingDamage, c.LastIncomingBy, Math.Max(0, now - c.LastIncomingAtMs)),
            world.Players.StrikeReachM);
    }
}
