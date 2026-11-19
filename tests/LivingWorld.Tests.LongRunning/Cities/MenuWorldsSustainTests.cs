using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Observation;
using LivingWorld.Tests.Shared.Cities;

namespace LivingWorld.Tests.LongRunning.Cities;

/// <summary>AD-190, regra do dono ("tem que funcionar no jogo"): os três mundos do menu se sustentam pelas próprias regras. Cada
/// mundo roda 10 anos numa seed (vila pequena 7, que colapsava: 40 → 14 em 20 anos em 57b59394; cidade média 2; metrópole 7, que
/// ia a 98 em 20 anos na AD-180) e precisa terminar com ao menos <see cref="PopulationFloor"/> da população inicial, mortes de fome
/// até <see cref="StarvationCeilingPerThousand"/> por mil pessoas-ano e nenhuma morte de sede. A junção que quebrou foi a AD-180 (o
/// ano de safra do menu com a faixa uniforme 500–1500). Medido com a AD-190: 38/40, 190/150, 471/400, 0 de fome e de sede. A cidade
/// média seed 1 e a metrópole seed 2 ainda passam fome em 20 anos (bloco de issue no decisions-log); o relatório de 20 anos × 3
/// seeds é <c>MenuWorldsYearsReport</c>. AD-191: de volta às seeds que colapsavam (cidade média seed 1, metrópole seed 2): a lavoura
/// que vale o grão, a ida de água que não volta vazia, a bolsa da casa a quem compra, o preço que olha o celeiro e a reserva de
/// fundação. E a cidade média atravessa uma quebra de safra de 1 em 50 anos sem perder mais de 10%.</summary>
[Trait("Category", "Scenario")]
public class MenuWorldsSustainTests
{
    public const double PopulationFloor = 0.9;
    public const double StarvationCeilingPerThousand = 1.0;
    public const int Years = 10;

    [Theory]
    [InlineData("vila-pequena", 7UL)]
    [InlineData("cidade-media", 1UL)]
    [InlineData("grande-metropole", 2UL)]
    public void Menu_world_feeds_itself_for_a_decade(string period, ulong seed)
    {
        var (world, baseClock) = MenuWorlds.Load(period, seed);
        var deaths = new Deaths();
        var clock = new WorldClock(baseClock.Systems, baseClock.MaxIterationsPerTick, deaths);
        int pop0 = world.AliveNpcIndex.Alive.Count;
        double personYears = 0;
        for (int y = 0; y < Years; y++)
        {
            int before = world.AliveNpcIndex.Alive.Count;
            clock.Run(world, world.Calendar.HoursPerYear);
            personYears += (before + world.AliveNpcIndex.Alive.Count) / 2.0;
        }
        int pop = world.AliveNpcIndex.Alive.Count;
        double starvation = deaths.Starved * 1000.0 / Math.Max(1, personYears);
        Assert.True(pop >= pop0 * PopulationFloor, $"{period} seed {seed}: {pop0} → {pop} em {Years} anos ({deaths.Starved} de fome, {deaths.Dehydrated} de sede)");
        Assert.True(starvation <= StarvationCeilingPerThousand, $"{period} seed {seed}: {starvation:F2} mortes de fome por mil pessoas-ano");
        Assert.Equal(0, deaths.Dehydrated);
    }

    /// <summary>AD-191: fator de safra de 1 em 50 anos (2% dos anos do menu ficam abaixo de 600).</summary>
    public const int OneInFiftyHarvest = 590;

    [Fact]
    public void The_middle_city_crosses_a_one_in_fifty_crop_failure_losing_at_most_a_tenth()
    {
        var (world, clock) = MenuWorlds.Load("cidade-media", 2UL);
        const long failYear = 4; // o quinto ano
        clock.Run(world, failYear * world.Calendar.HoursPerYear - 1);
        foreach (var region in world.Map.Regions) world.SetClimateYear(region.Id, failYear, OneInFiftyHarvest);
        int before = world.AliveNpcIndex.Alive.Count;
        clock.Run(world, 2 * world.Calendar.HoursPerYear + 1); // a colheita quebrada e o ano até a seguinte
        int after = world.AliveNpcIndex.Alive.Count;
        Assert.True(after >= before * 0.9, $"quebra de safra: {before} → {after}");
    }

    private sealed class Deaths : IWorldEventSink
    {
        public int Starved, Dehydrated;

        public void Record(WorldEvent evt)
        {
            if (evt.SourceSystem != YearEventTally.DeathSource) return;
            if (evt.Kind == WorldEventKind.Starvation) Starved++;
            else if (evt.Kind == WorldEventKind.Dehydration) Dehydrated++;
        }
    }
}
