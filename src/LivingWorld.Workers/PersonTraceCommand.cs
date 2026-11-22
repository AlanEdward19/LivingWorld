using LivingWorld.Simulation.Observation;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Workers;

/// <summary>Modo CLI da sonda de uma pessoa (AD-182): <c>person-trace &lt;seed&gt; &lt;npc&gt; &lt;anos&gt;</c>; ver <see cref="PersonTrace"/>.</summary>
public static class PersonTraceCommand
{
    public static int Run(ulong seed, long npcId, int years)
    {
        var (world, clock) = ScenarioRunner.Create(seed, familyRules: PressureSwitch.FamilyRules(), needsRules: ProvisionSwitch.NeedsRules(), inventionRules: ProvisionSwitch.InventionRules(), agricultureRules: AgricultureSwitch.AgricultureRules(), ontogenyRules: ExperimentSwitch.OntogenyRules(), cityLayout: CityLayoutSwitch.Rules(), naturalWaterRules: CityLayoutSwitch.NaturalWaterRules());
        return PersonTrace.Run(world, clock, npcId, years);
    }
}
