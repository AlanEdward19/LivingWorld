using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;

namespace LivingWorld.Simulation.Behavior.Decision;

// Parte de BehaviorDecisionSystem — só diagnóstico (AD-182, sonda person-trace): refaz a conta da decisão de uma pessoa sem
// escrever nada no mundo. Nenhum sistema chama isto.
public sealed partial class BehaviorDecisionSystem
{
    /// <summary>Uma linha com o que a decisão de agora veria: urgência, rotina, candidato fora da urgência, compra, elegibilidade e,
    /// na urgência, o rastro da utility (vencedora, fatores e bloqueios). Lê o mundo; não escreve.</summary>
    internal static string ExplainDecision(WorldState world, Npc npc)
    {
        long now = world.CurrentDate.TotalHours;
        var rules = world.NeedsRules;
        var stage = world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate));
        var index = MarketIndex.BuildForTick(world);
        bool urgent = HasUrgentNeedForDecision(npc, rules, now);
        var routine = RoutineWithCommuteLead(world, npc, world.ActionCatalog, stage);
        bool restock = NeedsRestocking(world, npc, index);
        var eligibility = LivingWorld.Simulation.Ontogeny.OntogenyMechanic.EligibilityFor(world.OntogenyRules, npc.Development);
        string elig = string.Join(",", new[] { ActionType.Buy, ActionType.Work, ActionType.Eat, ActionType.Travel, ActionType.Socialize }
            .Select(a => $"{a}:{(ConstrainByDevelopment(world, npc, a) == a ? 1 : 0)}"));
        string line = $"DEC urgent={urgent} routine={routine} restock={restock} elig={elig} social={npc.SocialAt(now)}";
        if (!urgent) return line + $" nonUrgent={NonUrgentCandidate(world, npc, routine, stage, index)}";
        var provision = ProvisionViewFor(world, npc, index);
        var decision = SelectByUtility(
            DecisionContextBuilder.Build(world, npc, now, provisionTargetDays: provision.TargetDays * PsycheMechanic.HoardFactor(world, npc, now),
                foodUnitPrice: provision.FoodPrice, shopperOut: provision.ShopperOut, foodStallKnownEmpty: provision.FoodStallEmpty),
            rules, world.EconomyRules, npc.CurrentAction, captureTrace: true, eligibility: eligibility,
            brainGains: world.BrainOn ? BrainMechanic.GainsFor(world.BrainRules!, npc, now) : null);
        var t = decision.Trace;
        // AD-191: o que acontece com a vencedora depois da utility (percepção, refino de local) e para onde a viagem iria
        int ti = world.Invention?.WaterTrips is { Count: > 0 } ? world.Invention.WaterTripIndex(npc.Id.Value) : -1;
        string trip = ti >= 0 ? $"{world.Invention!.WaterTrips![ti]}" : "-";
        var room = LivingWorld.Simulation.Invention.PersonalCarry.RoomOf(world, npc);
        line += $" trip=[{trip}] room={room.WeightG}g/{room.WetMl}ml started={npc.ActionStartedAtTick}";
        var perceived = ApplyPerception(world, npc, t.Winner);
        var refined = RefineForLocation(world, npc, perceived, index);
        return line + $" winner={t.Winner}:{t.WinningUtility:F2} perceived={perceived} refined={refined} sleepAt={SleepDestinationOf(world, npc)?.ToString() ?? "-"} travelTo={TravelDestinationOf(world, npc, index)?.ToString() ?? "-"} plus=[{string.Join(";", t.TopPositiveFactors)}] minus=[{string.Join(";", t.TopNegativeFactors)}] block=[{string.Join(";", t.BlockingFactors)}] alt=[{string.Join(",", t.KnownAlternatives)}]";
    }
}
