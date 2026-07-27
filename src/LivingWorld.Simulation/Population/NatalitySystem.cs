using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Nascimento como regra demográfica do cenário (fora de escopo: atração/casamento da
/// Fase 7). Uma vez por ano, cada household elegível rola concepção; sucesso agenda o parto
/// (task 4) em vez de virar filho na hora — gestação é gasto de tempo, não instantâneo.</summary>
public sealed class NatalitySystem : ISimulationSystem
{
    public const string SystemName = "population-natality";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Yearly;

    public void Tick(WorldState world, TickContext ctx)
    {
        var rules = world.PopulationRules;

        // Snapshot da lista: a concepção pode adicionar households não-nascidos ainda, mas
        // nunca deveria iterar a própria coleção sendo alterada por outro household do mesmo
        // laço (não altera; só a mãe grávida muda), então iterar direto é seguro e determinístico.
        foreach (var household in world.Households)
        {
            var mother = FindEligibleMother(world, household, rules);
            if (mother is null) continue;
            var father = FindPartner(world, household, mother.Id, rules);
            if (father is null) continue;

            double roll = ctx.Rng($"natality-{household.Id.Value}").NextDouble();
            if (roll >= rules.AnnualConceptionChance) continue;

            var dueDate = world.CurrentDate.AddDays(rules.GestationDays);
            mother.BecomePregnant(dueDate);
            ctx.ScheduleEvent(dueDate.TotalHours, SystemName, $"{mother.Id.Value}|{father.Id.Value}|{household.Id.Value}");
        }
    }

    public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt)
    {
        var parts = evt.Payload!.Split('|');
        var motherId = new NpcId(long.Parse(parts[0]));
        var fatherId = new NpcId(long.Parse(parts[1]));
        var householdId = new HouseholdId(long.Parse(parts[2]));

        var mother = world.FindNpc(motherId);
        var household = world.FindHousehold(householdId);
        mother?.ClearPregnancy();
        if (mother is not { IsAlive: true } || household is null) return; // mãe ou lar não sobreviveram à gestação

        var sex = ctx.Rng($"natality-sex-{motherId.Value}-{evt.Id}").NextDouble() < 0.5 ? Sex.Female : Sex.Male;
        var father = world.FindNpc(fatherId);
        var babyId = world.NextNpcIdAndAdvance();

        // Streams próprios por NPC (task 7, ADR-0005), mesma convenção de chave de
        // MortalitySystem.SchedulePlannedDeath ($"mortality-{npc.Id.Value}").
        var personality = Personality.RollFrom(ctx.Rng($"personality-{babyId.Value}"));
        var profession = world.PopulationCatalog.RollProfession(ctx.Rng($"profession-{babyId.Value}"));
        // Fase 6 (SKILL-09): pais conhecidos — Inherit em vez de RollInitial, mesmo stream
        // próprio do NPC recém-nascido; pai biológico usado mesmo que não tenha sobrevivido à
        // gestação (genética não muda com a morte dele), só o vínculo de household (fatherId
        // acima) reflete quem está vivo.
        var rateGene = RateGene.Inherit(mother.RateGene, father?.RateGene ?? mother.RateGene, ctx.Rng($"rategene-{babyId.Value}"));

        var baby = new Npc(
            babyId, $"npc-{mother.Culture.Id}-child-{evt.Id}", sex, world.CurrentDate,
            mother.Culture, household.Location, motherId, father is { IsAlive: true } ? fatherId : null,
            household.Id, health: 100,
            personality: personality, profession: profession, currentLocation: household.Location,
            rateGene: rateGene);

        world.AddNpc(baby);
        household.AddMember(baby.Id);
        ctx.LogEvent(WorldEventKind.Birth, $"{baby.Id.Value}|{motherId.Value}|{fatherId.Value}|{household.Id.Value}");
        MortalitySystem.SchedulePlannedDeath(world, ctx, baby);
    }

    private static Npc? FindEligibleMother(WorldState world, Household household, PopulationRules rules) =>
        household.Members
            .Select(world.FindNpc)
            .Where(n => n is { IsAlive: true, Sex: Sex.Female, PregnantUntil: null })
            .Where(n => rules.IsFertileAge(n!.AgeYears(world.CurrentDate)))
            .OrderBy(n => n!.Id.Value)
            .FirstOrDefault();

    private static Npc? FindPartner(WorldState world, Household household, NpcId excludeMother, PopulationRules rules) =>
        household.Members
            .Where(id => id != excludeMother)
            .Select(world.FindNpc)
            .Where(n => n is { IsAlive: true, Sex: Sex.Male })
            .Where(n => n!.AgeYears(world.CurrentDate) >= rules.FertilityMinAge)
            .OrderBy(n => n!.Id.Value)
            .FirstOrDefault();
}
