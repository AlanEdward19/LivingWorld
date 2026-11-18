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
        var rules = Environment.GetEnvironmentVariable("LW_MATE") != "nohealth" ? baseRules : WithoutHealth(baseRules ?? ScenarioRunner.DefaultFamilyRules);
        // AD-165: LW_SUPPORT=idade liga o sustento depois da separação até essa idade do filho (0 desliga)
        if (int.TryParse(Environment.GetEnvironmentVariable("LW_SUPPORT"), CultureInfo.InvariantCulture, out int age))
        {
            var r = rules ?? ScenarioRunner.DefaultFamilyRules;
            rules = r with { Unions = r.Unions with { SupportChildUntilAge = age } };
        }
        if (Names(rules?.Names ?? ScenarioRunner.DefaultFamilyRules.Names) is { } names)
            rules = (rules ?? ScenarioRunner.DefaultFamilyRules) with { Names = names };
        return rules;
    }

    /// <summary>AD-178: <c>LW_NAMES=off</c> desliga tudo o que o nome faz; <c>LW_NAMES=-aid,-grief,-legacy,-lineage,-courtship,-infamy</c>
    /// desliga só os efeitos listados (xará na ajuda, luto do xará, legado ao xará, linhagem na ajuda, nome no namoro, fama
    /// da linhagem). AD-183: <c>-honor</c> (boa fama dos feitos e o que ela faz), <c>-founder</c> (fundador e nome da cidade-filha),
    /// <c>-matchrep</c> (nome da família no namoro), <c>-hirerep</c> (nome na contratação), <c>-refuse</c> (mesmo nome pesado também
    /// por quem é pedido). Vazio = padrão (todos ligados).</summary>
    public static NameBondRules? Names(NameBondRules baseRules)
    {
        var raw = Environment.GetEnvironmentVariable("LW_NAMES");
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (raw.Trim() == "off") return NameBondRules.Off;
        var r = baseRules;
        foreach (var key in raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            r = key switch
            {
                "-aid" => r with { NamesakeAidGain = 0 },
                "-grief" => r with { NamesakeGriefScale = 0 },
                "-legacy" => r with { NamesakeLegacyShare = 0 },
                "-lineage" => r with { LineageAidCloseness = 0 },
                "-courtship" => r with { SameFamilyCourtshipFactor = 1 },
                "-infamy" => r with { LineageInfamySpill = 0 },
                "-honor" => r with
                {
                    LineageHonorSpill = 0,
                    HonorPerAid = 0,
                    HonorPerInvention = 0,
                    HonorPerVenture = 0,
                    HonorForFounding = 0,
                    HonorOffsetsInfamy = 0,
                    HonorCreditTrust = 0,
                },
                "-founder" => r with { FounderHonor = false },
                "-matchrep" => r with { ReputationCourtshipGain = 0 },
                "-hirerep" => r with { ReputationHiringOrder = false },
                "-refuse" => r with { NameSignalOnAcceptance = false },
                _ => throw new ArgumentException($"LW_NAMES: chave desconhecida '{key}'"),
            };
        return r;
    }

    public static FamilyRules WithoutHealth(FamilyRules rules) => rules with
    {
        AttractionWeights = new Dictionary<AttractionFactor, double>(rules.AttractionWeights) { [AttractionFactor.Health] = 0 },
    };

    /// <summary><c>LW_FINEMOTOR=1</c> liga o motor fino no rendimento (AD-163, 08 item 50); <c>0</c> desliga.</summary>
    /// AD-182 (08c item 149): <c>LW_REGRESSION=old</c> volta o piso da regressão pela idade escalada pela taxa.
    public static LivingWorld.Domain.Ontogeny.OntogenyRules? OntogenyRules()
    {
        var rules = Environment.GetEnvironmentVariable("LW_FINEMOTOR") switch
        {
            "1" => LivingWorld.Domain.Ontogeny.OntogenyDefaults.Human with { FineMotorScalesWork = true },
            "0" => LivingWorld.Domain.Ontogeny.OntogenyDefaults.Human with { FineMotorScalesWork = false },
            _ => null,
        };
        if (Environment.GetEnvironmentVariable("LW_REGRESSION") == "old")
            rules = (rules ?? LivingWorld.Domain.Ontogeny.OntogenyDefaults.Human) with { RegressionByEffectiveAge = true };
        return rules;
    }

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
