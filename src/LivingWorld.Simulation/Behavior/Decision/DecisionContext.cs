using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Extraordinary.Opportunity;

namespace LivingWorld.Simulation.Behavior.Decision;

/// <summary>Snapshot efêmero por (NpcId, wake) para scoring de decisão — nunca persistido, nunca
/// <c>[Canonical]</c>, nunca carrega referência a
/// <see cref="WorldState"/>. Coleções são listas vazias quando sem fatores, nunca <c>null</c>.
/// <see cref="ForesightPreviews"/> é volátil por tick; omitido/
/// <c>null</c> ≡ <see cref="ForesightMechanic.EmptyPreviews"/> (sem alocação no caminho comum). <see cref="Inner"/> traz a vida
/// interior (AD-138); nulo = calmo, sem lembrança de fome e sem hormônio que pese (AD-139).</summary>
public sealed record DecisionContext(
    NpcId NpcId,
    long Tick,
    NeedsSnapshot Needs,
    BodySnapshot Body,
    HouseholdSnapshot? Household,
    IReadOnlyList<NpcMemory> RelevantMemories,
    IReadOnlyList<string> RelevantBeliefs,
    IReadOnlyList<RelationshipFact> KnownRelationships,
    IReadOnlyList<PowerOpportunity> PowerOpportunities,
    Personality Personality,
    ActionType? CurrentAction,
    IReadOnlyDictionary<ActionType, ResolutionResult>? ForesightPreviews = null,
    long WalletAmount = 0,
    double ProvisionTargetDays = 2.0,
    LivingWorld.Simulation.Objects.ObjectComfort Comfort = default,
    InnerLifeSnapshot? Inner = null,
    long FoodUnitPrice = 0,
    bool ShopperOut = false,
    bool FoodStallKnownEmpty = false)
{
    /// <summary>AD-148: ir comprar comida adianta: a carteira paga uma unidade no mercado mais perto (preço desconhecido = paga,
    /// como antes) e a casa não acabou de achar a banca vazia.</summary>
    public bool CanPayFood => (FoodUnitPrice <= 0 || WalletAmount >= FoodUnitPrice) && !FoodStallKnownEmpty;

    /// <summary>Emoções atuais; calmo sem vida interior.</summary>
    public LivingWorld.Domain.Cognition.Affect Mood => Inner?.Mood ?? default;

    /// <summary>Força (0..100) da lembrança de fome; 0 sem ela.</summary>
    public double FamineMemory => Inner?.FamineMemory ?? 0;

    /// <summary>Hormônios que pesam (AD-139): ciclo e adolescência; nenhum efeito sem vida interior.</summary>
    public LivingWorld.Simulation.Population.Body.HormonalSnapshot Hormones => Inner?.Hormones ?? default;
}

/// <summary>Vida interior que pesa na decisão (AD-138). Nulo no contexto quando o NPC está calmo e sem lembrança de fome — o
/// caso comum —, para não aumentar a alocação de cada decisão.</summary>
public sealed record InnerLifeSnapshot(
    LivingWorld.Domain.Cognition.Affect Mood, double FamineMemory,
    LivingWorld.Simulation.Population.Body.HormonalSnapshot Hormones = default);

public readonly record struct NeedsSnapshot(int Hunger, int Thirst, int Sleep, int Social);

public readonly record struct BodySnapshot(
    double Height,
    double Weight,
    double MuscleMass,
    double WorkCapacityMultiplier,
    double MovementCostMultiplier);

public sealed record HouseholdSnapshot(
    HouseholdId Id,
    IReadOnlyDictionary<ResourceType, long> Stock,
    IReadOnlyList<NpcId> Members);

/// <summary>4 eixos de <see cref="Relationship"/> expostos ao scoring — <c>Familiarity</c>
/// espelha o eixo Debt (4º eixo canônico) no shape do DecisionContext (design P1b).</summary>
public readonly record struct RelationshipFact(
    NpcId With,
    int Trust,
    int Affection,
    int Respect,
    int Familiarity);
