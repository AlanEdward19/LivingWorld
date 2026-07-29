using LivingWorld.Domain;
using LivingWorld.Simulation.Behavior;

namespace LivingWorld.Simulation;

/// <summary>Concepção anual entre cônjuges casados (Fase 7, T17) — agenda parto com riqueza
/// capturada na concepção; risco de parto e hereditariedade no <see cref="HandleEvent"/>.</summary>
public sealed class NatalitySystem : ISimulationSystem
{
    public const string SystemName = "population-natality";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Yearly;

    public void Tick(WorldState world, TickContext ctx)
    {
        var populationRules = world.PopulationRules;
        var familyRules = world.FamilyRules;
        var now = world.CurrentDate;

        foreach (var mother in world.Npcs
                     .Where(n => n is { IsAlive: true, Sex: Sex.Female, PregnantUntil: null })
                     .OrderBy(n => n.Id.Value))
        {
            if (!populationRules.IsFertileAge(mother.AgeYears(now)))
                continue;

            var father = FindLivingSpouse(world, mother);
            if (father is null)
                continue;

            if (!MeetsConceptionFloors(world, mother, father, familyRules))
                continue;

            double roll = ctx.Rng($"natality-{mother.Id.Value}-{now.TotalHours}").NextDouble();
            if (roll >= populationRules.AnnualConceptionChance)
                continue;

            var household = mother.Household is { } householdId ? world.FindHousehold(householdId) : null;
            if (household is null)
                continue;

            long conceptionStock = household.Stock.Values.Sum();
            var dueDate = now.AddDays(populationRules.GestationDays);
            mother.BecomePregnant(dueDate);
            ctx.ScheduleEvent(
                dueDate.TotalHours,
                SystemName,
                $"{mother.Id.Value}|{father.Id.Value}|{household.Id.Value}|{conceptionStock}");
        }
    }

    public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt)
    {
        var parts = evt.Payload!.Split('|');
        var motherId = new NpcId(long.Parse(parts[0]));
        var fatherId = new NpcId(long.Parse(parts[1]));
        var householdId = new HouseholdId(long.Parse(parts[2]));
        long conceptionStock = long.Parse(parts[3]);

        var mother = world.FindNpc(motherId);
        var household = world.FindHousehold(householdId);
        mother?.ClearPregnancy();
        if (mother is not { IsAlive: true } || household is null)
            return;

        var father = world.FindNpc(fatherId);
        var familyRules = world.FamilyRules;

        if (ctx.Rng($"natality-maternal-{motherId.Value}-{evt.Id}").NextDouble() < familyRules.MaternalDeathRisk)
        {
            NpcDeath.Apply(world, ctx, mother, WorldEventKind.MaternalDeath);
            return;
        }

        if (ctx.Rng($"natality-infant-{motherId.Value}-{evt.Id}").NextDouble() < familyRules.InfantDeathRisk)
        {
            ctx.LogEvent(WorldEventKind.StillBirth, $"{motherId.Value}|{fatherId.Value}");
            return;
        }

        var sex = ctx.Rng($"natality-sex-{motherId.Value}-{evt.Id}").NextDouble() < 0.5 ? Sex.Female : Sex.Male;
        var babyId = world.NextNpcIdAndAdvance();

        var personality = Personality.RollFrom(ctx.StreamFor("personality", babyId.Value));
        var profession = world.PopulationCatalog.RollProfession(ctx.StreamFor("profession", babyId.Value));
        var rateGene = RateGene.Inherit(
            mother.RateGene, father?.RateGene ?? mother.RateGene, ctx.StreamFor("rategene", babyId.Value));
        double vitality = HeredityService.InheritVitality(
            mother.Vitality, father?.Vitality ?? mother.Vitality, familyRules,
            ctx.StreamFor("vitality", babyId.Value));
        double upbringing = HeredityService.DeriveUpbringingFromConceptionStock(conceptionStock, familyRules);

        var baby = new Npc(
            babyId, $"npc-{mother.Culture.Id}-child-{evt.Id}", sex, world.CurrentDate,
            mother.Culture, household.Location, motherId, father is { IsAlive: true } ? fatherId : null,
            household.Id, health: 100,
            personality: personality, profession: profession, currentLocation: household.Location,
            rateGene: rateGene, vitality: vitality, upbringing: upbringing);

        world.AddNpc(baby);
        baby.ConfigureNeedDecay(world.NeedsRules, world.CurrentDate.TotalHours);
        household.AddMember(baby.Id);
        ctx.LogEvent(WorldEventKind.Birth, $"{baby.Id.Value}|{motherId.Value}|{fatherId.Value}|{household.Id.Value}");
        MortalitySystem.SchedulePlannedDeath(world, ctx, baby);
        NpcWakeScheduler.ScheduleWake(world, ctx, baby.Id.Value, world.CurrentDate.TotalHours + 1);
    }

    internal static bool MeetsConceptionFloors(
        WorldState world, Npc mother, Npc father, FamilyRules familyRules)
    {
        if (mother.Health < familyRules.ConceptionHealthFloor
            || father.Health < familyRules.ConceptionHealthFloor)
            return false;

        if (!MeetsRelationshipFloor(world, mother, father, familyRules))
            return false;

        if (mother.Household is not { } householdId)
            return false;

        var household = world.FindHousehold(householdId);
        if (household is null)
            return false;

        foreach (var (resourceId, floor) in familyRules.ConceptionResourceFloor)
        {
            long stock = household.Stock.GetValueOrDefault(new ResourceType(resourceId));
            if (stock < floor)
                return false;
        }

        return true;
    }

    private static bool MeetsRelationshipFloor(WorldState world, Npc a, Npc b, FamilyRules familyRules)
    {
        var aToB = world.Relationships.GetValueOrDefault(new RelationshipKey(a.Id, b.Id));
        var bToA = world.Relationships.GetValueOrDefault(new RelationshipKey(b.Id, a.Id));
        double quality = (RelationshipQuality(aToB) + RelationshipQuality(bToA)) / 2.0;
        return quality >= familyRules.ConceptionRelationshipFloor;
    }

    private static double RelationshipQuality(Relationship? relationship)
    {
        if (relationship is null)
            return 0.0;

        double sum = 0;
        foreach (var axis in Enum.GetValues<RelationshipAxis>())
            sum += relationship.Get(axis);
        return sum / Enum.GetValues<RelationshipAxis>().Length;
    }

    private static Npc? FindLivingSpouse(WorldState world, Npc npc)
    {
        if (npc.Spouse is not { } spouseId)
            return null;

        var spouse = world.FindNpc(spouseId);
        if (spouse is not { IsAlive: true } || spouse.Spouse != npc.Id)
            return null;

        return spouse;
    }
}
