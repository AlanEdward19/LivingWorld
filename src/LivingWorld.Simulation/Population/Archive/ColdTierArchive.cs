using LivingWorld.Domain.Fauna;
using LivingWorld.Domain.Flora;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Performance;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Archive;

/// <summary>Arquivo frio (tier-2) de NPCs/fauna/flora mortos há muito tempo (Fase 9 PERF-10,
/// REALISM-21).</summary>
public sealed class ColdTierArchive
{
    private readonly Dictionary<long, NpcSummary> _byId = new();
    private readonly Dictionary<long, AnimalSummary> _animalsById = new();
    private readonly Dictionary<long, PlantSummary> _plantsById = new();

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

    public bool TryArchiveAnimal(WorldState world, Animal dead, long nowTick, PerfRules rules)
    {
        if (dead.IsAlive || dead.DeathTick is not { } deathTick) return false;

        long ageYears = (nowTick - deathTick) / world.Calendar.HoursPerYear;
        if (ageYears < rules.ColdArchiveAfterYears) return false;

        _animalsById[dead.Id.Value] = AnimalSummary.From(dead);
        world.RemoveAnimal(dead.Id);
        return true;
    }

    /// <summary>REALISM-21: flora sai do hot na morte e entra no arquivo frio na hora
    /// (sem DeathTick no record Plant — contrato T7).</summary>
    public void ArchivePlantOnDeath(Plant plant, long deathTick)
    {
        _plantsById[plant.Id.Value] = new PlantSummary(
            plant.Id, plant.Species, plant.Position, plant.GrowthStage, deathTick);
    }

    public NpcSummary? Lookup(long npcId) => _byId.GetValueOrDefault(npcId);

    public AnimalSummary? LookupAnimal(long animalId) => _animalsById.GetValueOrDefault(animalId);

    public PlantSummary? LookupPlant(long plantId) => _plantsById.GetValueOrDefault(plantId);

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

    public sealed record PlantSummary(PlantId Id, string Species, CellCoord Position, int GrowthStage, long DeathTick);

    public sealed record AnimalSummary(AnimalId Id, string Species, CellCoord Position, long DeathTick)
    {
        public static AnimalSummary From(Animal animal) => new(
            animal.Id, animal.Species, animal.Position, animal.DeathTick!.Value);
    }
}
