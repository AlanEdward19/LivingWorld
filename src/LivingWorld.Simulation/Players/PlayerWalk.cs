using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

/// <summary>AD-197k (ADR-0049, posição contínua): o jogador anda solto, em frações de célula, à velocidade real do corpo. O cliente
/// manda o rumo (<c>walk {dx, dy}</c>, milésimos) a cada ~100 ms; o motor anda o corpo pelo rumo anterior durante o tempo de mundo
/// decorrido desde o último comando (no máximo <see cref="PlayerRules.WalkHold"/>: sem comando o corpo para) e grava o rumo novo. A
/// velocidade sai da mesma conta da viagem de qualquer NPC (<see cref="PlayerCommands.PaceMs"/>: caminho, terreno, corpo e saúde,
/// carga, chão do dia, cansaço) relativa ao passo de referência, em m/s (<see cref="SpeedMps"/>). Ao cruzar a borda de uma célula o
/// chão é o da grade (<see cref="PlayerGround.Step"/>: parede, porta, gente, canto); barrado, desliza no eixo livre ou para na borda.
/// Andar gasta sono na medida do tempo andado. Determinístico: tudo é inteiro em milésimos e o tempo é o do mundo.</summary>
public static partial class PlayerWalk
{
    /// <summary>Velocidade do corpo agora indo de <paramref name="from"/> para <paramref name="to"/> (vizinha), em m/s; correndo
    /// (<paramref name="running"/>, AD-197u), <see cref="PlayerRules.RunFactor"/> vezes, até o teto de <see cref="PlayerRules.RunMaxMps"/>.</summary>
    public static double SpeedMps(WorldState world, Npc npc, CellCoord from, CellCoord to, bool running = false)
    {
        if (!world.Map.TryGetCell(from, out _) || !world.Map.TryGetCell(to, out _)) return world.Players.WalkMps;
        // AD-197u: a referência é a passada NESTE par de células (terreno e subida do mapa, a conta de MovementCost.Between), não a do
        // terreno mais fácil do mapa: o peso de terreno e a altitude do mapa são a escala de 30 min por célula do mundo, e contá-los de
        // novo num passo de 1,5 m fazia um adulto descansado andar a 0,47 m/s em mata (3× a referência da planície) e a 0,19 em morro.
        // Ficam como freios o corpo e a saúde, a carga, o cansaço, a água, o tempo (lama, neve: WeatherNow) e o chão do dia.
        double reference = Math.Max(1.0, LivingWorld.Domain.Geography.Spatial.MovementCost.Between(world.Map, from, to) * WorldMillis.PerHour);
        // a rua calçada encurta a viagem longa (menos desvio, chão firme), mas ninguém anda mais que um passo largo: piso de 0,8
        // (1,4 m/s → no máximo 1,75 m/s)
        double factor = Math.Max(0.8, PlayerCommands.PaceMs(world, npc, from, to) / reference);
        double walk = world.Players.WalkMps / factor;
        return running ? Math.Max(walk, Math.Min(walk * world.Players.RunFactor, world.Players.RunMaxMps)) : walk;
    }

    /// <summary>AD-197u: o corpo aguenta esforço (correr, pular): descansado (sono não abaixo de <see cref="PlayerRules.Tired"/>) e com
    /// saúde (não abaixo de <see cref="PlayerRules.ExertionMinHealth"/>).</summary>
    public static bool CanExert(WorldState world, Npc npc) =>
        npc.SleepNeed.ValueAtMs(world.CurrentDate.TotalMilliseconds) >= world.Players.Tired && npc.Health >= world.Players.ExertionMinHealth;

    public static Result<Unit> Walk(WorldState world, TickContext ctx, Npc npc, int dx, int dy, bool run = false)
    {
        if (dx is < -1000 or > 1000 || dy is < -1000 or > 1000) return Result<Unit>.Fail("walk: rumo fora de −1000..1000");
        long now = world.CurrentDate.TotalMilliseconds;
        // #239: quem corta na mata está ocupado (o passo dura segundos; a tarefa, horas): não sai andando antes de acabar
        if ((dx != 0 || dy != 0) && (PlayerTasks.Of(world, npc) is not null || npc.StepReadyAtMs - now > PlayerInteractions.LongTaskMs))
            return Result<Unit>.Fail($"{PlayerCommands.Conflict} walk: o corpo está ocupado (faltam {npc.StepReadyAtMs - now} ms)");
        var cell = PlayerGround.PoseOf(world, npc);
        var pose = npc.Fine is { } f && f.CellX == cell.X && f.CellY == cell.Y
            ? f
            : new FinePose(cell.X * FinePose.PerCell + 500, cell.Y * FinePose.PerCell + 500, 0, 0, now); // começou solto, ou a grade o moveu
        if (dx != 0 || dy != 0 || pose.HeadingX != 0 || pose.HeadingY != 0) PlayerCommands.TakeTheWheel(world, npc);
        long elapsed = Math.Clamp(now - pose.AtMs, 0, world.Players.WalkHold);
        if (elapsed > 0 && (pose.HeadingX != 0 || pose.HeadingY != 0))
            pose = Advance(world, ctx, npc, pose, elapsed);
        var (hx, hy) = Normalized(dx, dy);
        // AD-197u: cansado ou ferido demais, o corpo anda em vez de correr (a resposta diz running: false)
        bool running = run && (hx != 0 || hy != 0) && CanExert(world, npc);
        npc.SetFine(pose with { HeadingX = hx, HeadingY = hy, AtMs = now, Run = running });
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>Anda <paramref name="elapsed"/> ms pelo rumo da pose, em trechos de no máximo um quarto de célula.</summary>
    private static FinePose Advance(WorldState world, TickContext ctx, Npc npc, FinePose pose, long elapsed)
    {
        var cell = new CellCoord(pose.CellX, pose.CellY);
        var ahead = new CellCoord(cell.X + Math.Sign(pose.HeadingX), cell.Y + Math.Sign(pose.HeadingY));
        bool running = pose.Run && CanExert(world, npc); // o fôlego pode acabar no meio do trecho: segue andando
        double mps = SpeedMps(world, npc, cell, ahead, running);
        double cellsPerMs = mps / world.Players.CellM / 1000.0;
        long distance = (long)Math.Round(cellsPerMs * elapsed * FinePose.PerCell); // milésimos de célula
        long x = pose.X, y = pose.Y, walked = 0;
        while (walked < distance)
        {
            long chunk = Math.Min(250, distance - walked);
            long nx = x + chunk * pose.HeadingX / 1000, ny = y + chunk * pose.HeadingY / 1000;
            if (!TryEnter(world, ctx, npc, ref x, ref y, nx, ny)
                && !TryEnter(world, ctx, npc, ref x, ref y, nx, y)
                && !TryEnter(world, ctx, npc, ref x, ref y, x, ny))
                break; // barrado nos dois eixos: para onde está
            walked += chunk;
        }
        if (walked > 0)
            npc.Step(npc.StepReadyAtMs,
                world.Players.StepSleep * (running ? world.Players.RunSleepFactor : 1.0) * elapsed * walked / Math.Max(1, distance) / (double)WorldMillis.PerHour,
                world.CurrentDate.TotalMilliseconds);
        return pose with { X = x, Y = y };
    }

    /// <summary>Vai a (<paramref name="nx"/>, <paramref name="ny"/>) se a célula de lá é a mesma ou se a grade deixa passar a ela.</summary>
    private static bool TryEnter(WorldState world, TickContext ctx, Npc npc, ref long x, ref long y, long nx, long ny)
    {
        if (nx == x && ny == y) return false;
        var from = new CellCoord((int)Math.Floor(x / (double)FinePose.PerCell), (int)Math.Floor(y / (double)FinePose.PerCell));
        var to = new CellCoord((int)Math.Floor(nx / (double)FinePose.PerCell), (int)Math.Floor(ny / (double)FinePose.PerCell));
        if (to != from)
        {
            if (!PlayerGround.Step(world, npc, from, to).IsSuccess) return false;
            PlayerCommands.TakeTheWheel(world, npc);
            npc.MoveTo(to, ctx.CurrentTick);
            if (npc.Interior is { } inside && world.FindBuilding(inside.Building) is { Position: { } origin })
                npc.MoveWithinBuilding(new CellCoord(to.X - origin.X, to.Y - origin.Y));
        }
        x = nx;
        y = ny;
        return true;
    }

    private static (int X, int Y) Normalized(int dx, int dy)
    {
        if (dx == 0 && dy == 0) return (0, 0);
        double length = Math.Sqrt((double)dx * dx + (double)dy * dy);
        return ((int)Math.Round(dx * 1000 / length), (int)Math.Round(dy * 1000 / length));
    }
}
