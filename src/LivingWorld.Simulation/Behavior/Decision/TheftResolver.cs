using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Crime;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Behavior.Decision;

/// <summary>Furto (crime-and-theft). <see cref="Wants"/> decide se um NPC desesperado com o temperamento certo parte para o furto;
/// <see cref="Resolve"/> o executa ao fim da ação: sorteio semeado do ladrão e da hora, sucesso reduzido por cada guarda da
/// cidade, moeda que já existe mudando de carteira (nunca criada), e, se descoberto, <c>Betrayal</c> da vítima sobre o ladrão.
/// Tudo em ordem de id, sem relógio nem ordem de dicionário. Com <see cref="TheftIndex"/> a busca é por célula; sem ele, varre a população (testes).</summary>
internal static class TheftResolver
{
    public static bool Wants(WorldState world, Npc npc, long now, TheftIndex? index = null)
    {
        var rules = world.TheftRules;
        if (!rules.Enabled || npc.Wallet.Amount >= 1) return false;
        if (Math.Min(npc.HungerAt(now), npc.ThirstAt(now)) > rules.DesperateBelow) return false;
        if (PersonalityWeighting.WeightOf(npc.Personality, ActionType.Steal) < rules.PersonalityThreshold) return false;
        if (HasProvisions(world, npc)) return false;
        return (index is null ? FindVictim(world, npc) : index.FindVictim(npc)) is not null;
    }

    public static void Resolve(WorldState world, Npc thief, long tick, TickContext ctx, TheftIndex? index = null)
    {
        var rules = world.TheftRules;
        if (!rules.Enabled || (index is null ? FindVictim(world, thief) : index.FindVictim(thief)) is not { } victim) return;

        double success = rules.BaseSuccess * Math.Max(0.0, 1.0 - (index?.GuardsIn(thief.City) ?? GuardsIn(world, thief.City, rules)) * rules.GuardEffect);
        var rng = ctx.Rng($"theft-{thief.Id.Value}-{tick}");
        bool took = rng.NextDouble() < success;
        // quem falha é pego em flagrante; quem leva só é descoberto às vezes
        bool discovered = !took || rng.NextDouble() < rules.DiscoveryChance;

        long loot = 0;
        if (took)
        {
            loot = Math.Min(victim.Wallet.Amount, Math.Max(1, (long)(victim.Wallet.Amount * rules.MaxLootFraction)));
            if (victim.TryDebitWallet(new Money(loot)).IsSuccess) thief.CreditWallet(new Money(loot));
            else loot = 0;
        }

        if (!discovered) return;

        var relationship = world.GetOrCreateRelationship(new RelationshipKey(victim.Id, thief.Id), tick);
        relationship.ApplyEvent(RelationshipEventType.Betrayal, world.FamilyRules);
        relationship.MarkContact(tick);
        ctx.LogEvent(
            WorldEventKind.TheftDiscovered, $"{thief.Id.Value}|{victim.Id.Value}|{loot}|{(loot > 0 ? 1 : 0)}",
            BehaviorDecisionSystem.SystemName);
    }

    private static bool HasProvisions(WorldState world, Npc npc)
    {
        if (npc.Household is not { } id || world.FindHousehold(id) is not { } household) return false;
        var economy = world.EconomyRules;
        return household.Stock.GetValueOrDefault(new ResourceType(economy.FoodResourceId)) >= 1
            || household.Stock.GetValueOrDefault(new ResourceType(economy.WaterResourceId)) >= 1;
    }

    /// <summary>Quem tem mais moeda na mesma célula, fora da própria casa (empate: menor id).</summary>
    private static Npc? FindVictim(WorldState world, Npc thief)
    {
        Npc? best = null;
        foreach (var other in world.Npcs)
        {
            if (!other.IsAlive || other.Id == thief.Id || other.CurrentLocation != thief.CurrentLocation) continue;
            if (other.Wallet.Amount < 1) continue;
            if (thief.Household is { } home && other.Household == home) continue;
            if (best is null || other.Wallet.Amount > best.Wallet.Amount
                || (other.Wallet.Amount == best.Wallet.Amount && other.Id.Value < best.Id.Value))
                best = other;
        }
        return best;
    }

    private static int GuardsIn(WorldState world, CityId city, TheftRules rules)
    {
        if (rules.GuardProfessionId < 0) return 0;
        int guards = 0;
        foreach (var npc in world.Npcs)
            if (npc.IsAlive && npc.City == city && npc.Profession.Id == rules.GuardProfessionId)
                guards++;
        return guards;
    }
}
