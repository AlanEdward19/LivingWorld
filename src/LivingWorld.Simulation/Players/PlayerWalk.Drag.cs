using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Players;

/// <summary>#238: o que atrasa o passo agora, dito pelo motor para o cliente mostrar (nada inventado lá). <c>SlowPct</c>: quanto o passo
/// está abaixo do de referência (0 a 90); <c>Cause</c>: o maior freio entre <c>load</c> (carga), <c>tired</c> (sono), <c>weather</c> (lama,
/// neve), <c>body</c> (saúde, peso, idade) e <c>ground</c> (o chão e a subida do par de células); <c>none</c> sem atraso. <c>Terrain</c>
/// e <c>TerrainPct</c>: o terreno sob o corpo e o quanto ele pesa a mais que o terreno mais fácil do mapa (0 = o mais fácil).</summary>
public sealed record WalkDrag(int SlowPct, string Cause, int Terrain, int TerrainPct);

public static partial class PlayerWalk
{
    /// <summary>Abaixo disto o atraso de um freio não vira rótulo.</summary>
    public const double NoticeableFactor = 1.05;

    public static WalkDrag DragOf(WorldState world, Npc npc, CellCoord from, CellCoord to)
    {
        if (!world.Map.TryGetCell(from, out var cell)) return new WalkDrag(0, "none", 0, 0);
        double min = world.Map.Cost.TerrainWeight.Count == 0 ? 1.0 : world.Map.Cost.TerrainWeight.Values.Min();
        int terrainPct = (int)Math.Round(Math.Max(0, world.Map.Cost.WeightOf(cell.Terrain) / min - 1.0) * 100);
        double mps = SpeedMps(world, npc, from, to);
        int slow = (int)Math.Clamp(Math.Round((1.0 - mps / world.Players.WalkMps) * 100), 0, 90);
        if (slow < 5) return new WalkDrag(0, "none", cell.Terrain.Id, terrainPct);

        double tired = 1 + Math.Max(0, world.Players.Tired - npc.SleepAt(world.CurrentDate.TotalHours)) / Math.Max(1, world.Players.Tired) * 0.5;
        double weather = 1.0;
        if (LivingWorld.Simulation.Climate.WeatherNow.SlowsTravel(world, from, out var row) && !PlayerGround.Sheltered(world, from, to))
            weather = LivingWorld.Simulation.Climate.WeatherNow.TravelFactor(world, row,
                LivingWorld.Simulation.Cities.Streets.StreetRoutes.SoftPm(world, from, to));
        var factors = new (string Cause, double Factor)[]
        {
            ("load", LivingWorld.Simulation.Emergence.Mobility.LoadFactor(world, npc) * LivingWorld.Simulation.Objects.HomeFreight.LoadFactor(world, npc)),
            ("tired", tired),
            ("weather", weather),
            ("body", LivingWorld.Simulation.Behavior.Needs.BodyMechanic.MovementCostMultiplier(world, npc)),
        };
        var worst = factors.MaxBy(f => f.Factor);
        return new WalkDrag(slow, worst.Factor >= NoticeableFactor ? worst.Cause : "ground", cell.Terrain.Id, terrainPct);
    }
}
