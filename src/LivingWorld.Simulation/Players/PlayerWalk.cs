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
public static class PlayerWalk
{
    /// <summary>Velocidade do corpo agora indo de <paramref name="from"/> para <paramref name="to"/> (vizinha), em m/s.</summary>
    public static double SpeedMps(WorldState world, Npc npc, CellCoord from, CellCoord to)
    {
        // a referência é a passada no chão mais fácil do mapa (o terreno de menor peso): o fator diz quanto este chão, este corpo, esta
        // carga, este tempo e este cansaço alongam o passo
        var weights = world.Map.Cost.TerrainWeight;
        double easiest = weights.Count == 0 ? 1.0 : Math.Max(0.01, weights.Values.Min());
        double reference = world.Map.Cost.Base * easiest * WorldMillis.PerHour * (from.X != to.X && from.Y != to.Y ? 1.41421356237309515 : 1.0);
        double factor = Math.Max(0.25, PlayerCommands.PaceMs(world, npc, from, to) / Math.Max(1.0, reference));
        return world.Players.WalkMps / factor;
    }

    public static Result<Unit> Walk(WorldState world, TickContext ctx, Npc npc, int dx, int dy)
    {
        if (dx is < -1000 or > 1000 || dy is < -1000 or > 1000) return Result<Unit>.Fail("walk: rumo fora de −1000..1000");
        long now = world.CurrentDate.TotalMilliseconds;
        var cell = PlayerGround.PoseOf(world, npc);
        var pose = npc.Fine is { } f && f.CellX == cell.X && f.CellY == cell.Y
            ? f
            : new FinePose(cell.X * FinePose.PerCell + 500, cell.Y * FinePose.PerCell + 500, 0, 0, now); // começou solto, ou a grade o moveu
        if (dx != 0 || dy != 0 || pose.HeadingX != 0 || pose.HeadingY != 0) PlayerCommands.TakeTheWheel(world, npc);
        long elapsed = Math.Clamp(now - pose.AtMs, 0, world.Players.WalkHold);
        if (elapsed > 0 && (pose.HeadingX != 0 || pose.HeadingY != 0))
            pose = Advance(world, ctx, npc, pose, elapsed);
        var (hx, hy) = Normalized(dx, dy);
        npc.SetFine(pose with { HeadingX = hx, HeadingY = hy, AtMs = now });
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>Anda <paramref name="elapsed"/> ms pelo rumo da pose, em trechos de no máximo um quarto de célula.</summary>
    private static FinePose Advance(WorldState world, TickContext ctx, Npc npc, FinePose pose, long elapsed)
    {
        var cell = new CellCoord(pose.CellX, pose.CellY);
        var ahead = new CellCoord(cell.X + Math.Sign(pose.HeadingX), cell.Y + Math.Sign(pose.HeadingY));
        double mps = SpeedMps(world, npc, cell, ahead);
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
            npc.Step(npc.StepReadyAtMs, world.Players.StepSleep * elapsed * walked / Math.Max(1, distance) / (double)WorldMillis.PerHour,
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
