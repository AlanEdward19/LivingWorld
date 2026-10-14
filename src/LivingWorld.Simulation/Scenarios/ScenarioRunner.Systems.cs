using LivingWorld.Domain.Extraordinary;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Founding;
using LivingWorld.Simulation.Cities.Migration;
using LivingWorld.Simulation.Climate;
using LivingWorld.Simulation.Ecology;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Economy.Production;
using LivingWorld.Simulation.Economy.Supply;
using LivingWorld.Simulation.Economy.Trade;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Extraordinary.Systems;
using LivingWorld.Simulation.Geography;
using LivingWorld.Simulation.Health;
using LivingWorld.Simulation.History.Books;
using LivingWorld.Simulation.History.Distortion;
using LivingWorld.Simulation.Llm;
using LivingWorld.Simulation.Narrative;
using LivingWorld.Simulation.Periods;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Population.Archive;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Population.Skills;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Scenarios;

// Parte de ScenarioRunner — a lista de sistemas e a ordem do relógio. Os padrões estáticos ficam em ScenarioRunner.cs (ordem de inicialização).
public static partial class ScenarioRunner
{
    /// <summary>Ordem: decaimento de necessidade roda antes da decisão de
    /// ação no mesmo tick — senão <see cref="BehaviorDecisionSystem"/> decidiria com o dado de
    /// necessidade do tick anterior. Ambos entram depois de Mortalidade/Natalidade (NPC recém
    /// nascido/morto neste tick já participa da conta certa: morto não decide, nascido decai a
    /// partir do próprio nascimento). Os 4 sistemas de economia entram depois de
    /// <see cref="BehaviorDecisionSystem"/>, nessa ordem — quem contratou hoje já pode produzir
    /// no mesmo dia (Employment antes de Production); preço reage ao estoque já atualizado
    /// (Production antes de MarketPricing); salário é o último evento do mês sobre o Treasury
    /// que a produção/venda alimentou o mês inteiro. <see
    /// cref="SkillPracticeSystem"/> e <see cref="SkillTeachingSystem"/> entram entre Employment e
    /// Production — quem contratou hoje já pratica hoje, e a produção do mesmo dia já lê a
    /// habilidade atualizada (mesmo raciocínio de Employment-antes-de-Production).
    /// <see cref="RelationshipSystem"/> entra depois de <see cref="EmploymentSystem"/> (convivência
    /// em workplace/household alimenta relações antes da prática de habilidade); <see
    /// cref="CourtshipSystem"/> entra antes de <see cref="NatalitySystem"/> (casal formado no
    /// cortejo é quem a natalidade consome via <c>Npc.Spouse</c>).</summary>
    public static IReadOnlyList<ISimulationSystem> DefaultSystems(
        IReadOnlyList<PeriodTransformationRule>? periodTransformationRules = null,
        ConversationSessionStore? conversationSessions = null,
        ChronicleGenerationSystem? chronicles = null,
        ExtraordinaryScenarioData? extraordinary = null)
    {
        var systems = new List<ISimulationSystem>
        {
        new PeriodEvolutionSystem(periodTransformationRules ?? []),
        new ClimateYearSystem(),
        new MortalitySystem(),
        new OutbreakSystem(),
        new FactToReportConversionScheduler(),
        new BookRediscoverySystem(),
        new ColdArchiveSystem(),
        new CourtshipSystem(),
        new NatalitySystem(),
        new NeedsDecaySystem(),
        new BehaviorDecisionSystem(DefaultSkillsRules),
        new TradeLedgerSystem(),
        new BuildingOccupancySystem(),
        new ResourceProcessSystem(),
        new EmploymentSystem(),
        new RelationshipSystem(),
        new MutualAidSystem(),
        new SkillPracticeSystem(DefaultSkillsRules),
        new SkillTeachingSystem(DefaultSkillsRules, DefaultLifeStageRules),
        new WorkHardeningSystem(),
        new InputSupplySystem(),
        new ProductionSystem(DefaultSkillsRules),
        new FaunaLifecycleSystem(),
        new FloraLifecycleSystem(),
        new TemperatureSeasonSystem(),
        new CropSystem(),
        new MarketPricingSystem(),
        new WagePaymentSystem(),
        new LodgingSystem(),
        new CityGrowthSystem(),
        new ConstructionDemandSystem(),
        new ConstructionSystem(),
        new MigrationSystem(),
        new RelocationArrivalSystem(),
        new MaterializationSystem(),
        new SettlementFoundingSystem(),
        new SpatialSettlementFoundingSystem(),
        new PlayerSearchSystem(),
        chronicles ?? new ChronicleGenerationSystem(),
        conversationSessions ?? new ConversationSessionStore(),
        };
        if (extraordinary?.Enabled == true)
        {
            systems.Add(new ExtraordinaryStateSystem());
            systems.Add(new ExtraordinaryPowerStageSystem());
            systems.Add(new ExtraordinaryPassiveTickSystem());
            systems.Add(new DimensionPortalSystem());
            systems.Add(new FaunaDominateSystem());
            systems.Add(new FloraGrowthSystem());
        }
        return systems;
    }
}
