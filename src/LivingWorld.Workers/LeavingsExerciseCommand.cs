using LivingWorld.Simulation.Observation;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Workers;

/// <summary>Modo CLI do exercício do espólio (AD-182, 08c item 155): <c>leavings-exercise &lt;seed-inicial&gt; &lt;seeds&gt; &lt;anos&gt; &lt;horas&gt;</c>;
/// ver <see cref="LeavingsExercise"/>. Mesmas chaves de ambiente dos relatórios (<c>LW_PROVISION</c>, <c>LW_LEAVINGS</c>, <c>LW_GROUND</c>).</summary>
public static class LeavingsExerciseCommand
{
    public static int Run(ulong firstSeed, int seeds, int years, int everyHours)
    {
        var lines = new List<string>();
        Parallel.For(0, seeds, i =>
        {
            ulong seed = firstSeed + (ulong)i;
            var (world, clock) = ScenarioRunner.Create(seed, familyRules: PressureSwitch.FamilyRules(), needsRules: ProvisionSwitch.NeedsRules(),
                inventionRules: ProvisionSwitch.InventionRules(), agricultureRules: AgricultureSwitch.AgricultureRules(),
                ontogenyRules: ExperimentSwitch.OntogenyRules(), cityLayout: CityLayoutSwitch.Rules());
            var result = LeavingsExercise.Run(world, clock, years, everyHours);
            lock (lines) lines.AddRange(result.Select(l => $"seed={seed} {l}"));
        });
        lines.Sort(StringComparer.Ordinal);
        foreach (var l in lines) Console.WriteLine(l);
        return 0;
    }
}
