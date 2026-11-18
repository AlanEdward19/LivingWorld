using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Observation;
using LivingWorld.Tests.Shared.Cities;

namespace LivingWorld.Tests.LongRunning.Cities;

/// <summary>AD-190, regra do dono ("tem que funcionar no jogo"): os três mundos do menu se sustentam pelas próprias regras. Cada
/// mundo roda 10 anos na seed em que ele colapsava (cidade média 1, metrópole 2, vila pequena 7) e precisa terminar com ao menos
/// <see cref="PopulationFloor"/> da população inicial, mortes de fome abaixo de <see cref="StarvationCeilingPerThousand"/> por mil
/// pessoas-ano e nenhuma morte de sede. Antes da AD-190 (57b59394), 20 anos: cidade média seed 1 150 → 66 (63 de fome), vila
/// pequena seed 7 40 → 14, metrópole seed 2 23 mortes de sede; a junção que quebrou foi a AD-180 (o ano de safra do menu com a
/// faixa uniforme 500–1500 e a ceifa parada pela chuva). O relatório de 20 anos × 3 seeds é <c>MenuWorldsYearsReport</c>.</summary>
[Trait("Category", "Scenario")]
public class MenuWorldsSustainTests
{
    public const double PopulationFloor = 1.0;
    public const double StarvationCeilingPerThousand = 3.0;
    public const int Years = 10;

    [Theory]
    [InlineData("cidade-media", 1UL)]
    [InlineData("grande-metropole", 2UL)]
    [InlineData("vila-pequena", 7UL)]
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
