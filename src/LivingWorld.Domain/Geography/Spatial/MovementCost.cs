using LivingWorld.Domain.Geography.Map;

namespace LivingWorld.Domain.Geography.Spatial;

/// <summary>Custo de deslocamento entre duas células (task 3): distância euclidiana × peso
/// médio de terreno, mais penalidade de subida. Direcional por construção: subir custa mais
/// que descer a mesma diferença de altitude; sem diferença de altitude, é simétrico.</summary>
public static class MovementCost
{
    public static double Between(WorldMap map, CellCoord origin, CellCoord destination)
    {
        var from = map.CellAt(origin);
        var to = map.CellAt(destination);

        double dx = destination.X - origin.X;
        double dy = destination.Y - origin.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        double terrainFactor = (map.Cost.WeightOf(from.Terrain) + map.Cost.WeightOf(to.Terrain)) / 2.0;
        double climb = Math.Max(0, to.Altitude - from.Altitude) * map.Cost.AltitudeWeight;

        return map.Cost.Base * distance * terrainFactor + climb;
    }
}
