using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Cities;
using LivingWorld.Tests.Shared.Cities;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>#223: trabalhador não se empilha no prédio de trabalho: cada um fica num posto com lotação por célula, e quem lavra
/// fica no campo (fora da planta), não dentro do prédio.</summary>
public class WorkStandsTests
{
    [Fact]
    public void Workers_of_a_workday_respect_the_cell_crowd_and_farmers_stand_in_the_field()
    {
        var (world, clock) = MenuWorlds.Load("cidade-media", 2);
        var footprints = new HashSet<CellCoord>();
        int crowdLimit = world.Players.CellCrowd;
        int farmHands = 0;
        for (int hour = 0; hour < 24 * 4; hour++)
        {
            clock.Run(world, 1);
            if (hour % 24 < 9 || hour % 24 > 16) continue;
            footprints.Clear();
            foreach (var building in world.Buildings.Where(b => b.Position is not null))
                foreach (var cell in BuildingFootprintGenerator.Generate(building))
                    footprints.Add(new CellCoord(building.Position!.Value.X + cell.Cell.X, building.Position.Value.Y + cell.Cell.Y));

            var perCell = new Dictionary<CellCoord, int>();
            foreach (var npc in world.Npcs.Where(n => n.IsAlive && n.CurrentAction == ActionType.Work && n.Interior is { Floor.Value: 0 } && n.Employer is not null))
            {
                var origin = world.FindBuilding(npc.Interior!.Building)!.Position!.Value;
                var pose = new CellCoord(origin.X + npc.Interior.LocalCell.X, origin.Y + npc.Interior.LocalCell.Y);
                perCell[pose] = perCell.GetValueOrDefault(pose) + 1;
                var workplace = world.FindWorkplace(npc.Employer!.Value)!;
                if (workplace.Building != npc.Interior.Building) continue;
                if (workplace.LocationType.Id == LivingWorld.Simulation.Economy.Production.CropSystem.DefaultFarmLocationTypeId)
                {
                    farmHands++;
                    Assert.DoesNotContain(pose, footprints);
                }
            }

            foreach (var (cell, count) in perCell)
                Assert.True(count <= crowdLimit, $"célula {cell} com {count} pessoas (lotação {crowdLimit}) na hora {hour}");
        }

        Assert.True(farmHands > 20, $"o teste precisa de lavradores de verdade ({farmHands})");
    }

    [Fact]
    public void Pick_takes_the_emptiest_cell_and_never_passes_the_limit()
    {
        var (world, _) = MenuWorlds.Load("vila-pequena", 2);
        int limit = world.Players.CellCrowd;
        var stands = new[] { new CellCoord(1, 1), new CellCoord(2, 1), new CellCoord(3, 1) };
        var crowd = new Dictionary<CellCoord, int>();
        for (int n = 0; n < stands.Length * limit; n++)
        {
            var pick = WorkStands.Pick(world, stands, crowd);
            Assert.NotNull(pick);
            crowd[pick.Value] = crowd.GetValueOrDefault(pick.Value) + 1;
            if (n < stands.Length) Assert.Equal(1, crowd[pick.Value]); // enche um por célula antes de dobrar
        }

        Assert.Null(WorkStands.Pick(world, stands, crowd));
        Assert.All(crowd.Values, count => Assert.Equal(limit, count));
    }
}
