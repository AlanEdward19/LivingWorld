using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

/// <summary>A volta de quem estava sumido (AD-196, Fase 25): o jogador que volta a encarná-lo, o motor que o reassume (pool) ou a
/// procura que o acha. A reação é calculada, não roteirizada: quem tem vínculo com ele hoje guarda a lembrança do reencontro e
/// sente alegria na medida do vínculo, mais marcante quanto mais tempo ele ficou sumido, e o laço se reforça. O que correu na
/// ausência fica: a herança já dividida não volta e o cônjuge que se uniu de novo segue com quem está (AD-136); se o cônjuge
/// ainda é dele, ele volta para a casa do cônjuge. Chamado antes de limpar <see cref="Npc.DisappearedAtTick"/>.</summary>
public static class LateReturn
{
    public const string ByPlayer = "jogador";
    public const string ByPool = "pool";
    public const string BySearch = "busca";

    public static void Apply(WorldState world, TickContext ctx, Npc npc, string how)
    {
        var rules = world.Players;
        if (!rules.Enabled || npc.DisappearedAtTick is not { } since) return;
        long now = ctx.CurrentTick;
        int days = (int)((now - since) / world.Calendar.HoursPerDay);
        double time = 0.6 + 0.4 * Math.Min(1.0, days / (double)Math.Max(1, rules.ReunionFullSalienceDays));

        ComeHome(world, ctx, npc);
        var bonds = Disappearance.CloseBonds(world, npc, rules);
        foreach (var (id, strength) in bonds)
        {
            if (world.FindNpc(id) is not { IsAlive: true } other) continue;
            double scale = strength / 100.0 * time;
            world.AddNpcMemory(
                id, MemoryCategory.Episodic, $"{other.Name} reencontrou {npc.Name}, sumido havia {days} dias",
                (int)Math.Round(Math.Min(100, strength * time)), now, [npc.Id], other.CurrentLocation,
                LlmRules.Default.CanonicalMemoryImportanceThreshold);
            PsycheMechanic.Experience(world, other, LifeEventKind.Reunited, npc.Id.Value, now, scale, rules.Reunited);
            var tie = world.GetOrCreateRelationship(new RelationshipKey(id, npc.Id), now);
            tie.ApplyEvent(RelationshipEventType.Help, world.FamilyRules);
            tie.MarkContact(now);
        }

        ctx.LogEvent(WorldEventKind.Returned, $"{npc.Id.Value}|{days}|{bonds.Count}|{how}", sourceSystem: Disappearance.SourceSystem);
    }

    /// <summary>Cônjuge que ainda é dele: volta para a casa do cônjuge. Cônjuge que se uniu de novo (ou se separou dele na
    /// ausência): a união dele também se desfaz, nunca a do outro.</summary>
    private static void ComeHome(WorldState world, TickContext ctx, Npc npc)
    {
        if (npc.Spouse is not { } spouseId) return;
        if (world.FindNpc(spouseId) is not { IsAlive: true } spouse || spouse.Spouse != npc.Id)
        {
            if (world.FindNpc(spouseId) is { IsAlive: true }) npc.EndMarriage(); // viúvo de quem morreu segue lendo a viuvez
            return;
        }
        if (spouse.Household is not { } homeId || world.FindHousehold(homeId) is not { } home || npc.Household == homeId) return;

        var from = npc.Household is { } fromId ? world.FindHousehold(fromId) : null;
        from?.RemoveMember(npc.Id);
        home.AddMember(npc.Id);
        npc.JoinHousehold(home.Id);
        if (npc.City != spouse.City) npc.JoinCity(spouse.City);
        if (from is not null) HouseholdCleanup.DissolveIfEmpty(world, ctx, from);
    }
}
