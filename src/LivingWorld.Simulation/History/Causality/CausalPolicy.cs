using System.Collections.Frozen;
using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.History.Causality;

/// <summary>Papel causal de um <see cref="WorldEventKind"/> (AD-121, issue #113).</summary>
public enum CausalRole
{
    /// <summary>Pode abrir uma cadeia: nascimento natural, comando de autoria, acidente, contabilidade.</summary>
    Root,

    /// <summary>Transforma o mundo: precisa de <see cref="WorldEvent.CauseEventId"/> (o evento registrado
    /// mais próximo que tornou a transição possível) e de <c>SourceSystem</c> real.</summary>
    Transformation,
}

/// <summary>Tabela única, no código, de qual kind é transformação e qual é raiz. Todo kind novo
/// do enum entra numa das duas listas — o teste reprova se faltar. Esta classe só classifica:
/// não muda estado do mundo nem emite evento.</summary>
public static class CausalPolicy
{
    private static readonly WorldEventKind[] Transformations =
    [
        // Mortes e perdas de vida
        WorldEventKind.Death, WorldEventKind.Starvation, WorldEventKind.Dehydration,
        WorldEventKind.Exhaustion, WorldEventKind.Disease, WorldEventKind.MaternalDeath,
        // Trabalho, renda e moradia
        WorldEventKind.Hired, WorldEventKind.Fired, WorldEventKind.WageUnpaid,
        WorldEventKind.RentUnpaid, WorldEventKind.Evicted, WorldEventKind.EstateSettled,
        WorldEventKind.AidGiven, WorldEventKind.TheftDiscovered,
        // Família
        WorldEventKind.CourtshipSucceeded, WorldEventKind.Marriage, WorldEventKind.OrphanTakenIn,
        WorldEventKind.PowerInherited, WorldEventKind.MilestoneRegressed,
        WorldEventKind.UnionEnded, WorldEventKind.AffairDiscovered,
        // Cidade, saúde e tecnologia
        WorldEventKind.SettlementFounded, WorldEventKind.CityMerged, WorldEventKind.OutbreakStarted,
        WorldEventKind.TechnologyDiscovered, WorldEventKind.TechnologyAdopted,
        // Extraordinário e combate (cadeia pela invocação)
        WorldEventKind.ExtraordinaryCostPaid, WorldEventKind.ExtraordinaryEffectApplied,
        WorldEventKind.ExtraordinaryUseFailed, WorldEventKind.ExtraordinaryFailureApplied,
        WorldEventKind.ExtraordinaryManifested, WorldEventKind.ExtraordinaryDormant,
        WorldEventKind.ExtraordinaryCulturalReaction, WorldEventKind.ExtraordinaryConstructCreated,
        WorldEventKind.ExtraordinaryConstructDamaged, WorldEventKind.ExtraordinaryConstructRemoved,
        WorldEventKind.CombatRound, WorldEventKind.CombatResolved, WorldEventKind.NpcInstantiated,
        WorldEventKind.IdentityChanged, WorldEventKind.PossessionResisted, WorldEventKind.PowerInvoked,
        // Jogador e histórico (derivados de um evento anterior)
        WorldEventKind.DisappearedFound, WorldEventKind.SearchGaveUp, WorldEventKind.FactRecorded,
        WorldEventKind.ReportConverted, WorldEventKind.CompensatingCorrection,
        WorldEventKind.BookRediscovered,
    ];

    private static readonly WorldEventKind[] Roots =
    [
        WorldEventKind.Birth, WorldEventKind.StillBirth, WorldEventKind.PlantMatured,
        WorldEventKind.CourtshipStarted, WorldEventKind.CourtshipRejected,
        WorldEventKind.AffairStarted, WorldEventKind.VocationChosen, WorldEventKind.UnwedPregnancy,
        WorldEventKind.ResourceLost, WorldEventKind.Minted, WorldEventKind.Destroyed,
        WorldEventKind.Trade, WorldEventKind.BookLost,
        WorldEventKind.ExtraordinaryUseAttempted, WorldEventKind.ExtraordinaryAcquired,
        WorldEventKind.ExtraordinaryAcquisitionFailed, WorldEventKind.ExtraordinaryRevoked,
        WorldEventKind.CombatEncounterStarted,
        // Autoria e jogador: comando externo ao mundo
        WorldEventKind.AuthoringCommandApplied, WorldEventKind.AuthoringCommandRejected,
        WorldEventKind.AuthoredImmigration, WorldEventKind.AuthoredWorkplaceFounded,
        WorldEventKind.PlayerIncarnated, WorldEventKind.PlayerDisappeared,
    ];

    private static readonly FrozenDictionary<WorldEventKind, CausalRole> Roles = Build();

    public static IReadOnlyCollection<WorldEventKind> TransformationKinds => Transformations;

    public static IReadOnlyCollection<WorldEventKind> RootKinds => Roots;

    /// <summary>Papel do kind, ou <c>null</c> se ninguém o classificou (o teste de cobertura reprova).</summary>
    public static CausalRole? RoleOf(WorldEventKind kind) =>
        Roles.TryGetValue(kind, out var role) ? role : null;

    public static bool IsTransformation(WorldEventKind kind) =>
        Roles.TryGetValue(kind, out var role) && role == CausalRole.Transformation;

    private static FrozenDictionary<WorldEventKind, CausalRole> Build()
    {
        var map = new Dictionary<WorldEventKind, CausalRole>();
        foreach (var kind in Transformations) map.Add(kind, CausalRole.Transformation);
        foreach (var kind in Roots) map.Add(kind, CausalRole.Root); // duplicata aqui lança: um kind, um papel
        return map.ToFrozenDictionary();
    }
}
