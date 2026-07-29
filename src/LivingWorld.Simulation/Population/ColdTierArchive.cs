using LivingWorld.Domain;

namespace LivingWorld.Simulation.Population;

/// <summary>Arquivo frio (tier-2) de NPCs mortos há muito tempo (Fase 9, PERF-10).</summary>
public sealed class ColdTierArchive
{
    private readonly Dictionary<long, NpcSummary> _byId = new();

    public bool TryArchive(WorldState world, TickContext ctx, Npc deadNpc, long nowTick, PerfRules rules)
    {
        if (deadNpc.IsAlive) return false;
        if (deadNpc.DeathDate is not { } deathDate) return false;

        long ageYears = (nowTick - deathDate.TotalHours) / deadNpc.BirthDate.Calendar.HoursPerYear;
        if (ageYears < rules.ColdArchiveAfterYears) return false;

        if (IsReferencedByLivingNpc(world, deadNpc.Id))
            return false;

        Money wallet = deadNpc.Wallet;
        if (wallet.Amount > 0)
        {
            if (!deadNpc.TryDebitWallet(wallet).IsSuccess)
                return false;
            world.BurnCirculatingMoney(ctx, wallet, "cold-archive");
        }

        _byId[deadNpc.Id.Value] = NpcSummary.From(deadNpc);
        world.RemoveNpc(deadNpc.Id);
        return true;
    }

    public NpcSummary? Lookup(long npcId) => _byId.GetValueOrDefault(npcId);

    private static bool IsReferencedByLivingNpc(WorldState world, NpcId id)
    {
        foreach (var npc in world.AliveNpcIndex.Alive)
        {
            if (npc.MotherId == id || npc.FatherId == id || npc.Spouse == id || npc.Mentor == id || npc.CourtingWith == id)
                return true;
        }

        foreach (var workplace in world.Workplaces)
        {
            if (workplace.Employees.Contains(id))
                return true;
        }

        foreach (var household in world.Households)
        {
            if (household.Members.Contains(id))
                return true;
        }

        return false;
    }

    public sealed record NpcSummary(
        NpcId Id, string Name, Sex Sex, WorldDate BirthDate, WorldDate? DeathDate, CultureId Culture, ProfessionType Profession)
    {
        public static NpcSummary From(Npc npc) => new(
            npc.Id, npc.Name, npc.Sex, npc.BirthDate, npc.DeathDate, npc.Culture, npc.Profession);
    }
}
