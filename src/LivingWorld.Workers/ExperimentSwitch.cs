using System.Globalization;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Simulation.Population.Skills;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Workers;

/// <summary>Experimentos de causa do AD-163 (`a-medir` grupo 3), só para os relatórios. <c>LW_MATE=nohealth</c> tira a saúde
/// da escolha de parceiro (08 item 2); <c>LW_VOCATION=x</c> troca a pressão dos pais na escolha do ofício (08 item 8; o padrão é
/// 0,7, o moderno 0,15). Vazio = padrão.</summary>
public static class ExperimentSwitch
{
    public static FamilyRules? FamilyRules(FamilyRules? baseRules)
    {
        if (Environment.GetEnvironmentVariable("LW_MATE") != "nohealth") return baseRules;
        return WithoutHealth(baseRules ?? ScenarioRunner.DefaultFamilyRules);
    }

    public static FamilyRules WithoutHealth(FamilyRules rules) => rules with
    {
        AttractionWeights = new Dictionary<AttractionFactor, double>(rules.AttractionWeights) { [AttractionFactor.Health] = 0 },
    };

    /// <summary><c>LW_FINEMOTOR=1</c> liga o motor fino no rendimento (AD-163, 08 item 50); <c>0</c> desliga.</summary>
    public static LivingWorld.Domain.Ontogeny.OntogenyRules? OntogenyRules() => Environment.GetEnvironmentVariable("LW_FINEMOTOR") switch
    {
        "1" => LivingWorld.Domain.Ontogeny.OntogenyDefaults.Human with { FineMotorScalesWork = true },
        "0" => LivingWorld.Domain.Ontogeny.OntogenyDefaults.Human with { FineMotorScalesWork = false },
        _ => null,
    };

    public static Func<ISimulationSystem, ISimulationSystem>? SwapSystem()
    {
        var raw = Environment.GetEnvironmentVariable("LW_VOCATION");
        if (string.IsNullOrWhiteSpace(raw)) return null;
        double pressure = double.Parse(raw, CultureInfo.InvariantCulture);
        var skills = ScenarioRunner.DefaultSkillsRules with
        {
            Vocation = ScenarioRunner.DefaultSkillsRules.Vocation with { ParentalPressure = pressure },
        };
        return s => s is SkillTeachingSystem ? new SkillTeachingSystem(skills, ScenarioRunner.DefaultLifeStageRules) : s;
    }
}
