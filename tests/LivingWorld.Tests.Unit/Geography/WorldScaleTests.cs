using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Geography;

/// <summary>ADR-0052 (AD-197o): a escala única do mundo gerado. Mede a pessoa contra o andar e a porta (que o motor declara e o cliente
/// desenha) e a largura das ruas; as medidas que ainda não batem com o alvo ficam registradas na medida (próximas partes do ADR).</summary>
public class WorldScaleTests
{
    [Fact]
    public void Adults_measure_like_people_and_fit_under_the_door_and_far_under_a_floor()
    {
        var (world, _) = ScenarioRunner.Create(7);
        var adults = world.Npcs.Where(n => n.IsAlive && world.LifeStageRules.LifeStageOf(n.AgeYears(world.CurrentDate)) == LifeStage.Adult)
            .Select(n => n.Height).ToList();
        Assert.NotEmpty(adults);
        adults.Sort();
        double median = adults[adults.Count / 2];
        Assert.InRange(median, 1.50, 1.90); // pessoa adulta pelo corpo do motor
        Assert.True(adults.Max() < WorldScale.FloorMeters, $"o mais alto ({adults.Max():0.00} m) cabe num andar de {WorldScale.FloorMeters} m");
        Assert.True(median <= WorldScale.DoorMeters, "a pessoa mediana passa pela porta sem se curvar");
        Assert.InRange(WorldScale.FloorMeters / WorldScale.CellMeters, 1.8, 2.1); // um andar: duas células de altura
    }

    /// <summary>Medida (não é alvo ainda: AD-197o3): largura das ruas do mundo gerado, em metros. <c>LW_MEASURE_OUT</c> grava.</summary>
    [Fact]
    public void The_street_width_of_the_generated_village_is_measured_in_meters()
    {
        var (world, _) = ScenarioRunner.Create(7);
        var city = world.ActiveCities().First();
        var net = CityStreets.Of(world, city);
        // largura no ponto: a menor das duas corridas de rua (de leste a oeste e de norte a sul) que passam pela célula; a mediana
        int Run(CellCoord c, int dx, int dy)
        {
            int n = 1;
            for (int k = 1; net.Contains(new CellCoord(c.X + dx * k, c.Y + dy * k)); k++) n++;
            for (int k = 1; net.Contains(new CellCoord(c.X - dx * k, c.Y - dy * k)); k++) n++;
            return n;
        }
        var widths = net.Cells.Select(c => Math.Min(Run(c, 1, 0), Run(c, 0, 1))).OrderBy(w => w).ToList();
        int widest = widths[widths.Count / 2];
        double meters = widest * WorldScale.CellMeters;
        string line = FormattableString.Invariant($"streetCells={net.Count} medianWidthCells={widest} medianWidthMeters={meters:0.0}");
        if (Environment.GetEnvironmentVariable("LW_MEASURE_OUT") is { Length: > 0 } path) File.WriteAllText(path, line + "\n");
        Assert.True(net.Count > 0, line);
    }
}
