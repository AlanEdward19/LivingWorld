using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Crime;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Behavior.Decision;

/// <summary>Furto por fome (AD-150), decidido na hora pela ação Furtar. <see cref="Wants"/>: quem passa do limite do desespero
/// (fome ou sede), sem comida nem água em casa e sem como comprar (sem moeda ou a banca vazia), e cujo temperamento passa a
/// barreira moral, que a fome rebaixa e o medo da punição sobe, e tem algo a levar (comida de banca ou casa, ou a moeda de quem
/// está ao lado). <see cref="Resolve"/> executa ao fim da ação pelo mesmo caminho do furto planejado (<see cref="Theft"/>).
/// As checagens baratas vêm primeiro: o caminho comum (alguém com comida ou com moeda) não procura alvo nem aloca.</summary>
internal static class TheftResolver
{
    public static bool Wants(WorldState world, Npc npc, long now)
    {
        var rules = world.TheftRules;
        if (!rules.Enabled) return false;
        double need = Theft.NeedOf(rules, npc, now);
        if (need < 0 || npc.AgeYears(world.CurrentDate) < rules.MinAgeYears) return false;
        if (Theft.HasFoodAtHome(world, npc) || Theft.CanBuyFood(world, npc)) return false;
        double barrier = rules.NeedBarrier(need, Theft.Deterrence(world, npc, now));
        if (PersonalityWeighting.WeightOf(npc.Personality, ActionType.Steal) < barrier) return false;
        return Theft.FindTarget(world, npc, TheftMotive.Hunger) is not null;
    }

    public static void Resolve(WorldState world, Npc thief, long tick, TickContext ctx)
    {
        if (!world.TheftRules.Enabled || Theft.FindTarget(world, thief, TheftMotive.Hunger) is not { } target) return;
        Theft.Commit(world, ctx, thief, TheftMotive.Hunger, target, ctx.OneShot($"theft-{thief.Id.Value}-{tick}"));
    }
}
