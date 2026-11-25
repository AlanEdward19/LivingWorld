using System.Diagnostics;
using System.Globalization;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.LongRunning.Performance;

/// <summary>AD-197t2: o custo do criador de personagem numa metrópole (10.000 pessoas numa cidade). <c>GET /players/origins/{cityId}</c>
/// (famílias, laços, teto de dinheiro) e a prévia (<c>POST .../character/preview</c>) rodam sob a trava do laço do relógio, então
/// precisam caber folgados num tick: o teto declarado é a mediana de 3 rodadas abaixo de <see cref="OptionsCeilingMs"/> e
/// <see cref="PreviewCeilingMs"/>. Fora do tick, sob demanda, só lê.</summary>
[Collection(ScalePerformanceCollection.Name)]
[Trait("Category", "Scenario")]
[Trait("Category", "Measure")]
[Trait("Category", "Timing")] // mede tempo de relógio: fase quieta do verify.sh
public class CharacterCreatorCostTests
{
    public const int Population = 10_000;
    public const double OptionsCeilingMs = 250; // medido: 10 ms
    public const double PreviewCeilingMs = 50; // medido: 2 ms

    private static double MedianMs(Action action)
    {
        action(); // aquecido fora da medição
        var samples = new List<double>();
        for (int i = 0; i < 3; i++)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            var watch = Stopwatch.StartNew();
            action();
            watch.Stop();
            samples.Add(watch.Elapsed.TotalMilliseconds);
        }
        return samples.Order().ElementAt(1);
    }

    [Fact]
    public void Options_and_preview_in_a_ten_thousand_person_city_stay_under_the_declared_ceiling()
    {
        var (world, _) = ScenarioRunner.Create(42, initialPopulation: Population);
        var city = world.ActiveCities().First();
        var families = CharacterFamilies.List(world, city);
        Assert.NotEmpty(families);
        Assert.Equal(CharacterFamilies.MaxFamilies, families.Count); // o teto de famílias vale: a lista não cresce com a cidade
        var parent = families.SelectMany(f => f.Members).First(m => m.Roles[CharacterRole.Child].Available);
        var spec = new CharacterSpec(city.Id.Value, Role: new CharacterRole(CharacterRole.Child, parent.NpcId));
        Assert.True(CharacterBuilder.Resolve(world, "metro", spec).IsSuccess);

        double options = MedianMs(() =>
        {
            CharacterFamilies.List(world, city);
            CharacterBuilder.MoneyCap(world, city);
        });
        double preview = MedianMs(() => CharacterBuilder.Resolve(world, "metro", spec));

        if (Environment.GetEnvironmentVariable("LW_CHARACTER_COST_OUT") is { } path)
            File.WriteAllText(path, string.Create(CultureInfo.InvariantCulture, $"options={options:F1} preview={preview:F1}"));
        Assert.True(options < OptionsCeilingMs, $"opções {options:F1} ms (teto {OptionsCeilingMs} ms)");
        Assert.True(preview < PreviewCeilingMs, $"prévia {preview:F1} ms (teto {PreviewCeilingMs} ms)");
    }
}
