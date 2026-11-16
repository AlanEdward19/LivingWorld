using LivingWorld.Simulation.Observation;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Workers;

/// <summary>Modo CLI <c>demography-report &lt;seed-inicial&gt; &lt;seeds&gt; &lt;anos&gt; &lt;aquecimento&gt;</c> (AD-130): roda o cenário
/// padrão de código (<see cref="ScenarioRunner.Create"/>) em seeds consecutivas, em paralelo, e imprime natalidade,
/// mortalidade, 1q0, e0, intervalo entre partos, razão de sexo e calendário dos nascimentos.</summary>
public static class DemographyReportCommand
{
    public static int Run(ulong firstSeed, int seeds, int years, int warmupYears)
    {
        var runs = new DemographyReport.SeedResult[seeds];
        // AD-142: LW_BRAIN=1 liga o cérebro; LW_BRAIN_BASE_FLOOR=n muda o piso do grupo base (variante do experimento)
        // AD-160: LW_BRAIN_VARIANT escolhe a variante medida (ver BrainVariant)
        // AD-160b: o cenário liga o cérebro; LW_BRAIN=0 desliga (braço de comparação), LW_BRAIN=1 + LW_BRAIN_VARIANT escolhe a variante
        string? brainEnv = Environment.GetEnvironmentVariable("LW_BRAIN");
        var brain = brainEnv == "0" ? LivingWorld.Domain.Cognition.BrainRules.Off
            : brainEnv != "1" ? null
            : BrainVariant.FromEnvironment() is { } variant ? variant
            : int.TryParse(Environment.GetEnvironmentVariable("LW_BRAIN_BASE_FLOOR"), out int floor)
                ? LivingWorld.Domain.Cognition.BrainRules.Default with { BaseMinGain = floor }
                : LivingWorld.Domain.Cognition.BrainRules.Default;
        // AD-156: LW_POWERS=1 liga os poderes do cenário de referência e imprime a herança por geração
        var powers = Environment.GetEnvironmentVariable("LW_POWERS") == "1" ? PowerGeneticsReport.ReferenceScenario : (Func<LivingWorld.Domain.Extraordinary.ExtraordinaryScenarioData>?)null;
        var powerTallies = new PowerGeneticsReport.Tally?[seeds];
        Parallel.For(0, seeds, i => runs[i] = DemographyReport.Run(firstSeed + (ulong)i, years, warmupYears, brain, PressureSwitch.FamilyRules(), needsRules: ProvisionSwitch.NeedsRules(), inventionRules: ProvisionSwitch.InventionRules(),
            agricultureRules: AgricultureSwitch.AgricultureRules(), economyRules: AgricultureSwitch.EconomyRules(), crimeRules: CrimeSwitch.CrimeRules(),
            extraordinary: powers?.Invoke(),
            observe: powers is null ? null : world => { powerTallies[i] = new PowerGeneticsReport.Tally(world); return null; },
            afterMonth: powers is null ? null : (_, m) => powerTallies[i]!.SampleMonth(m)));
        Console.Write(DemographyReport.Summarize(runs, years, warmupYears, ScenarioRunner.DefaultCalendar.HoursPerYear));
        if (powers is not null)
            Console.Write(PowerGeneticsReport.Summarize(powerTallies.Select(t => t!).ToList(), powers().Descriptors.Select(d => d.Id).ToList()));
        return 0;
    }
}
