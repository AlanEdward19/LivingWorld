using LivingWorld.Domain.History;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Founding;
using LivingWorld.Simulation.Cities.Migration;
using LivingWorld.Simulation.Ecology;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Economy.Production;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Extraordinary.Systems;
using LivingWorld.Simulation.Geography;
using LivingWorld.Simulation.History.Books;
using LivingWorld.Simulation.History.Distortion;
using LivingWorld.Simulation.Llm;
using LivingWorld.Simulation.Narrative;
using LivingWorld.Simulation.Periods;
using LivingWorld.Simulation.Population.Archive;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Population.Skills;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Api.Visual.Catalogs;

public enum CapabilityKind
{
    LivingWorld,
    DiagnosticOnly,
}

public sealed record LivingWorldCapability(
    string Id,
    CapabilityKind Kind,
    IReadOnlyList<Type> Systems,
    IReadOnlyList<WorldEventKind> Events,
    IReadOnlyList<string> ConsumerKeys,
    string? DiagnosticReason = null);

public static class LivingWorldCapabilityCatalog
{
    public static IReadOnlyList<LivingWorldCapability> All { get; } =
    [
        Living("TIME", consumers: ["hud.clock"]),
        Living("GEO", systems: [typeof(TemperatureSeasonSystem)], consumers: ["map.geography"]),
        Living("NEEDS", systems: [typeof(NeedsDecaySystem)], consumers: ["inspector.npc.needs"]),
        Living("BEHAVIOR", systems: [typeof(BehaviorDecisionSystem)], consumers: ["map.npc.action"]),
        Living("REST", consumers: ["map.npc.rest"]),
        Living("FOOD", consumers: ["inspector.npc.food"]),
        Living("CROPS", systems: [typeof(CropSystem)], consumers: ["map.crop"]),
        Living("FAUNA", systems: [typeof(FaunaLifecycleSystem)], consumers: ["map.fauna"]),
        Living("FLORA", systems: [typeof(FloraLifecycleSystem)],
            events: [WorldEventKind.PlantMatured], consumers: ["map.flora"]),
        Living("WATER", consumers: ["map.water"]),
        Living("EMPLOYMENT", [typeof(EmploymentSystem)], [WorldEventKind.Hired, WorldEventKind.Fired], ["inspector.employment"]),
        Living("PRODUCTION", [typeof(ProductionSystem), typeof(ResourceProcessSystem)], [WorldEventKind.ResourceLost], ["inspector.production"]),
        Living("MARKET", systems: [typeof(MarketPricingSystem)], consumers: ["inspector.market"]),
        Living("WAGES", [typeof(WagePaymentSystem)], [WorldEventKind.WageUnpaid], ["timeline.wages"]),
        Living("MONEY", events: [WorldEventKind.Minted, WorldEventKind.Destroyed], consumers: ["timeline.money"]),
        Living("SKILLS", systems: [typeof(SkillPracticeSystem), typeof(SkillTeachingSystem), typeof(WorkHardeningSystem)], consumers: ["inspector.npc.skills"]),
        Living("RELATIONSHIPS", [typeof(RelationshipSystem), typeof(CourtshipSystem)],
            [WorldEventKind.Marriage, WorldEventKind.CourtshipStarted, WorldEventKind.CourtshipRejected, WorldEventKind.CourtshipSucceeded],
            ["inspector.npc.relationships"]),
        Living("BIRTH", [typeof(NatalitySystem)],
            [WorldEventKind.Birth, WorldEventKind.MaternalDeath, WorldEventKind.StillBirth], ["timeline.birth"]),
        Living("DEATH", [typeof(MortalitySystem)], [WorldEventKind.Death, WorldEventKind.Starvation], ["timeline.death"]),
        Living("ARCHIVE", systems: [typeof(ColdArchiveSystem)], consumers: ["inspector.history.archive"]),
        Living("CITY_GROWTH", systems: [typeof(CityGrowthSystem)], consumers: ["inspector.city.growth"]),
        Living("CONSTRUCTION", systems: [typeof(ConstructionDemandSystem), typeof(ConstructionSystem)], consumers: ["inspector.city.construction"]),
        Living("MIGRATION", systems: [typeof(MigrationSystem), typeof(RelocationArrivalSystem)], consumers: ["map.migration"]),
        Living("MATERIALIZATION", systems: [typeof(MaterializationSystem)], consumers: ["hud.materialization"]),
        Living("FOUNDING", [typeof(SettlementFoundingSystem), typeof(SpatialSettlementFoundingSystem)],
            [WorldEventKind.SettlementFounded, WorldEventKind.CityMerged], ["map.founding"]),
        Living("HISTORY_FACT", [typeof(FactToReportConversionScheduler)],
            [WorldEventKind.FactRecorded, WorldEventKind.ReportConverted, WorldEventKind.CompensatingCorrection],
            ["timeline.knowledge"]),
        Living("HISTORY_BOOK", [typeof(BookRediscoverySystem)],
            [WorldEventKind.BookLost, WorldEventKind.BookRediscovered], ["timeline.books"]),
        Living("NARRATIVE", systems: [typeof(ChronicleGenerationSystem)], consumers: ["inspector.narrative"]),
        Living("CONVERSATION", systems: [typeof(ConversationSessionStore)], consumers: ["interaction.conversation"]),
        Living("PERIOD", systems: [typeof(PeriodEvolutionSystem)], consumers: ["hud.period"]),
        Living("EXTRAORDINARY", systems: [
            typeof(ExtraordinaryStateSystem),
            typeof(ExtraordinaryPowerStageSystem),
            typeof(ExtraordinaryPassiveTickSystem),
            typeof(DimensionPortalSystem),
            typeof(FaunaDominateSystem),
            typeof(FloraGrowthSystem),
        ],
            events: [
                WorldEventKind.ExtraordinaryUseAttempted,
                WorldEventKind.ExtraordinaryCostPaid,
                WorldEventKind.ExtraordinaryEffectApplied,
                WorldEventKind.ExtraordinaryUseFailed,
                WorldEventKind.ExtraordinaryFailureApplied,
                WorldEventKind.ExtraordinaryAcquired,
                WorldEventKind.ExtraordinaryAcquisitionFailed,
                WorldEventKind.ExtraordinaryManifested,
                WorldEventKind.ExtraordinaryDormant,
                WorldEventKind.ExtraordinaryCulturalReaction,
                WorldEventKind.ExtraordinaryConstructCreated,
                WorldEventKind.ExtraordinaryConstructDamaged,
                WorldEventKind.ExtraordinaryConstructRemoved,
                WorldEventKind.ExtraordinaryRevoked,
                WorldEventKind.CombatResolved,
                WorldEventKind.CombatEncounterStarted,
                WorldEventKind.CombatRound,
                WorldEventKind.PossessionResisted,
                WorldEventKind.NpcInstantiated,
                WorldEventKind.IdentityChanged,
                WorldEventKind.PowerInherited,
                WorldEventKind.PowerInvoked,
            ],
            consumers: ["inspector.npc.extraordinary", "map.extraordinary.construct"]),
        Living("AUTHORING", events: [WorldEventKind.AuthoringCommandApplied, WorldEventKind.AuthoringCommandRejected], consumers: ["inspector.npc.authoring"]),
        new("EXAMPLE_COUNTER", CapabilityKind.DiagnosticOnly, [typeof(ExampleCounterSystem)], [], [],
            "Scheduler instrumentation; it neither describes nor changes the simulated world."),
    ];

    private static LivingWorldCapability Living(
        string id,
        IReadOnlyList<Type>? systems = null,
        IReadOnlyList<WorldEventKind>? events = null,
        IReadOnlyList<string>? consumers = null) =>
        new(id, CapabilityKind.LivingWorld, systems ?? [], events ?? [], consumers ?? []);
}
