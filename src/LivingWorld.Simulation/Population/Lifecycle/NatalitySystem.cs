using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Inheritance;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Lifecycle;

/// <summary>Concepção por ciclo entre cônjuges (AD-130): uma vez por mês cada mulher casada, fora da gravidez e do intervalo
/// pós-parto, concebe com a fecundabilidade da idade dela (zero depois de estéril) vezes a fertilidade do marido pela idade
/// dele, num dia qualquer do ciclo. Na concepção ficam decididos o sexo de cada feto (gêmeos raros), a duração da gestação
/// (a termo ou prematuro) ou a perda precoce. No fim, <see cref="HandleEvent"/> resolve perda, morte materna e natimorto
/// pelo risco da idade, saúde e paridade da mãe, e os nascimentos.</summary>
[LivingCapability("BIRTH", Events = [WorldEventKind.Birth, WorldEventKind.MaternalDeath, WorldEventKind.StillBirth], Consumers = ["timeline.birth"])]
[RunsAfter(typeof(CourtshipSystem))]
public sealed class NatalitySystem : ISimulationSystem
{
    public const string SystemName = "population-natality";

    /// <summary>Terceiro campo do payload de <see cref="WorldEventKind.StillBirth"/> quando a gestação terminou cedo.</summary>
    public const string MiscarriageMarker = "miscarriage";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Monthly;

    public void Tick(WorldState world, TickContext ctx)
    {
        var populationRules = world.PopulationRules;
        var fertility = populationRules.Fertility;
        var familyRules = world.FamilyRules;
        long now = ctx.CurrentTick;
        long hoursPerYear = world.Calendar.HoursPerYear;
        var alive = world.AliveNpcIndex.Alive; // em ordem de id; a concepção não muda a lista

        for (int i = 0; i < alive.Count; i++)
        {
            var mother = alive[i];
            if (mother.Sex != Sex.Female || mother.PregnantUntil is not null) continue;
            if (mother.Reproduction is { } history && history.InfecundUntilTick > now) continue;

            var father = FindLivingSpouse(world, mother);
            if (father is null) continue;

            // Mesmo teto de população viva usado por split-on-death (fase 16.4).
            if (populationRules.RemainingAliveSlots(alive.Count) <= 0) return;

            if (!MeetsConceptionFloors(world, mother, father, familyRules)) continue;

            double motherAge = (now - mother.BirthDate.TotalHours) / (double)hoursPerYear;
            double fatherAge = (now - father.BirthDate.TotalHours) / (double)hoursPerYear;
            double chance = fertility.FemaleCycleFecundability(motherAge, SterilityDraw(ctx, mother))
                * fertility.MaleFertility.At(fatherAge)
                * AttributeMechanic.FertilityMultiplier(world, mother)
                * AttributeMechanic.FertilityMultiplier(world, father);
            if (chance <= 0) continue;

            var rng = ctx.StreamFor("conception", unchecked(mother.Id.Value * 1_000_003L + now));
            if (rng.NextDouble() >= chance) continue;

            var household = mother.Household is { } householdId ? world.FindHousehold(householdId) : null;
            if (household is null) continue;

            Conceive(world, ctx, mother, UnionSystem.BiologicalFather(world, ctx, mother, father), household, motherAge, rng); // AD-136: filho do amante
        }
    }

    /// <summary>Sorteio fixo da mulher contra a fração estéril por idade: a mesma em toda chamada (inclui a menopausa).</summary>
    public static double SterilityDraw(TickContext ctx, Npc woman) => ctx.StreamFor("sterility", woman.Id.Value).NextDouble();

    private static void Conceive(
        WorldState world, TickContext ctx, Npc mother, Npc father, Household household, double motherAge, WorldRng rng)
    {
        var fertility = world.PopulationRules.Fertility;
        long now = ctx.CurrentTick;
        long hoursPerDay = world.Calendar.HoursPerDay;
        long conceived = now + (long)(rng.NextDouble() * world.Calendar.HoursPerMonth);
        string sexes = RollSex(rng, fertility).ToString();
        if (rng.NextDouble() < fertility.TwinRate)
            sexes += rng.NextDouble() < fertility.MonozygoticTwinShare ? sexes : RollSex(rng, fertility).ToString();

        double lossRisk = Math.Min(0.95, fertility.MiscarriageRiskByAge.At(motherAge) * ReproductionRules.HealthFactor(mother.Health));
        bool loss = rng.NextDouble() < lossRisk;
        int days = loss
            ? fertility.MiscarriageMinDays + (int)(rng.NextDouble() * (fertility.MiscarriageMaxDays - fertility.MiscarriageMinDays + 1))
            : fertility.RollGestation(rng).Days;
        long endTick = Math.Max(now + 1, conceived + days * hoursPerDay);

        long conceptionStock = household.Stock.Values.Sum();
        mother.BecomePregnant(world.CurrentDate.AddHours(endTick - now));
        mother.SetReproduction((mother.Reproduction ?? new ReproductiveState()) with
        {
            LastConceptionTick = conceived,
            Pregnancy = new PregnancyState(conceived, endTick, father.Id.Value, sexes, days, loss),
        });
        ctx.ScheduleEvent(endTick, SystemName, $"{mother.Id.Value}|{father.Id.Value}|{household.Id.Value}|{conceptionStock}");
    }

    private static char RollSex(WorldRng rng, ReproductionRules fertility) =>
        rng.NextDouble() < fertility.MaleBirthShare ? 'M' : 'F';

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
        var history = mother?.Reproduction ?? new ReproductiveState();
        var pregnancy = history.Pregnancy;
        history = history with { Pregnancy = null };
        if (mother is not null && pregnancy is not null) mother.SetReproduction(history);
        if (mother is not { IsAlive: true } || household is null)
            return;

        var father = world.FindNpc(fatherId);
        var familyRules = world.FamilyRules;
        var fertility = world.PopulationRules.Fertility;
        long now = ctx.CurrentTick;
        long lossInfecundUntil = now + fertility.PregnancyLossInfecundDays * world.Calendar.HoursPerDay;

        if (pregnancy is { EndsInLoss: true })
        {
            mother.SetReproduction(history with { InfecundUntilTick = lossInfecundUntil });
            ctx.LogEvent(
                WorldEventKind.StillBirth, $"{motherId.Value}|{fatherId.Value}|{MiscarriageMarker}", sourceSystem: "NatalitySystem");
            return;
        }

        // risco obstétrico pela idade, partos anteriores e saúde da mãe, sobre as taxas-base do cenário
        double risk = fertility.ObstetricRiskMultiplier(mother.AgeYears(world.CurrentDate), history.Parity, mother.Health);
        bool maternalDeath = ctx.OneShot($"natality-maternal-{motherId.Value}-{evt.Id}").NextDouble() < familyRules.MaternalDeathRisk * risk;
        var physiology = world.NeedsRules.Physiology;
        // AD-134: a mãe que morre no parto ainda dá à luz (o filho nasce vivo em parte desses partos); antes não havia bebê
        bool birthDespiteDeath = maternalDeath && physiology.Enabled && physiology.Lactation.Enabled;
        if (maternalDeath && !birthDespiteDeath)
        {
            NpcDeath.Apply(world, ctx, mother, WorldEventKind.MaternalDeath);
            return;
        }

        var delivered = history with { Parity = history.Parity + 1, LastBirthTick = now };
        double stillbirthRisk = birthDespiteDeath ? physiology.Lactation.StillbirthRiskWhenMotherDies : familyRules.InfantDeathRisk * risk;
        if (ctx.OneShot($"natality-infant-{motherId.Value}-{evt.Id}").NextDouble() < stillbirthRisk)
        {
            mother.SetReproduction(delivered with { InfecundUntilTick = lossInfecundUntil });
            ctx.LogEvent(
                WorldEventKind.StillBirth, $"{motherId.Value}|{fatherId.Value}",
                sourceSystem: "NatalitySystem");
            if (birthDespiteDeath) NpcDeath.Apply(world, ctx, mother, WorldEventKind.MaternalDeath);
            else ApplyDeliveryStrain(mother, physiology, risk);
            return;
        }

        int postpartumDays = fertility.RollPostpartumInfecundDays(
            ctx.StreamFor("postpartum", unchecked(motherId.Value * 1_000_003L + now)));
        mother.SetReproduction(delivered with { InfecundUntilTick = now + postpartumDays * world.Calendar.HoursPerDay });

        // Gravidez anterior ao AD-130 (snapshot antigo) não guardou sexo nem duração: sorteia o sexo no parto, a termo.
        string sexes = pregnancy?.Sexes
            ?? (ctx.OneShot($"natality-sex-{motherId.Value}-{evt.Id}").NextDouble() < fertility.MaleBirthShare ? "M" : "F");
        int gestationDays = pregnancy?.GestationDays ?? fertility.TermMeanDays;
        for (int i = 0; i < sexes.Length; i++)
        {
            // Teto de população viva (compartilhado com split-on-death).
            if (world.PopulationRules.RemainingAliveSlots(world.AliveNpcIndex.Alive.Count) <= 0)
                break;
            Deliver(world, ctx, evt, mother, father, fatherId, household, sexes[i] == 'M' ? Sex.Male : Sex.Female,
                gestationDays, conceptionStock, i == 0 ? "" : $"-{i + 1}");
        }

        if (birthDespiteDeath)
        {
            NpcDeath.Apply(world, ctx, mother, WorldEventKind.MaternalDeath);
            return;
        }

        ApplyDeliveryStrain(mother, physiology, risk);
        LivingWorld.Simulation.Health.Lactation.RefreshHousehold(world, mother, now);
    }

    /// <summary>O parto gasta reserva (hemorragia, anemia) na proporção do risco obstétrico; a fisiologia diária recupera.</summary>
    private static void ApplyDeliveryStrain(Npc mother, LivingWorld.Domain.Health.PhysiologyRules physiology, double risk)
    {
        if (!physiology.Enabled || physiology.DeliveryHealthLoss <= 0) return;
        mother.SetHealth(Math.Max(physiology.HealthFloor, mother.Health - (int)Math.Round(physiology.DeliveryHealthLoss * risk)));
    }

    private static void Deliver(
        WorldState world, TickContext ctx, ScheduledEvent evt, Npc mother, Npc? father, NpcId fatherId, Household household,
        Sex sex, int gestationDays, long conceptionStock, string nameSuffix)
    {
        var motherId = mother.Id;
        var familyRules = world.FamilyRules;
        var babyId = world.NextNpcIdAndAdvance();

        // a personalidade mistura a dos pais com sorteio próprio, no peso do cenário (personality-heredity)
        var personality = Personality.Inherit(
            mother.Personality, father?.Personality ?? mother.Personality, ctx.StreamFor("personality", babyId.Value),
            familyRules.PersonalityHeritability);
        // aparência herdada dos pais; só com AppearanceRules (sem ele o stream nem é aberto: hash igual ao de antes)
        var appearance = world.AppearanceRules is { } appearanceRules && mother.Appearance is { } motherLook
            ? Appearance.Inherit(motherLook, father?.Appearance ?? motherLook, ctx.StreamFor("appearance", babyId.Value),
                appearanceRules.Heritability)
            : null;
        var profession = ProfessionType.None; // ninguém nasce com ofício: escolhe na juventude (AD-136, VocationMechanic)
        var genetics = world.BodyRules.Genetics;
        var rateGene = RateGene.Inherit(
            mother.RateGene, father?.RateGene ?? mother.RateGene, ctx.StreamFor("rategene", babyId.Value), genetics);
        double vitality = HeredityService.InheritVitality(
            mother.Vitality, father?.Vitality ?? mother.Vitality, familyRules, genetics,
            ctx.StreamFor("vitality", babyId.Value));
        double upbringing = HeredityService.DeriveUpbringingFromConceptionStock(conceptionStock, familyRules);
        var body = world.BodyRules;
        double height = BodyGeneration.RollHeight(ctx.StreamFor("height", babyId.Value), body);
        double weight = BodyGeneration.RollWeight(ctx.StreamFor("weight", babyId.Value), body);
        double muscleMass = BodyGeneration.RollMuscleMass(ctx.StreamFor("musclemass", babyId.Value), body);

        var baby = new Npc(
            babyId, $"npc-{mother.Culture.Id}-child-{evt.Id}{nameSuffix}", sex, world.CurrentDate,
            mother.Culture, household.Location, motherId, father is { IsAlive: true } ? fatherId : null,
            household.Id, health: 100,
            personality: personality, profession: profession, currentLocation: household.Location,
            rateGene: rateGene, vitality: vitality, upbringing: upbringing,
            height: height, weight: weight, muscleMass: muscleMass, appearance: appearance);
        baby.SetGestationAtBirth(gestationDays);
        // AD-142: ganhos sinápticos herdados (só com o cérebro ligado; desligado o stream nem é aberto)
        if (world.BrainRules is { Enabled: true } brainRules)
            baby.SetBrain(LivingWorld.Simulation.Cognition.BrainMechanic.Inherit(
                brainRules, mother.Brain, father?.Brain, ctx.StreamFor("brain", babyId.Value), familyRules.PersonalityHeritability));
        // AD-134: tamanho de recém-nascido (menor se prematuro) e estatura herdada dos dois pais
        LivingWorld.Simulation.Population.Body.BodyGrowth.InitializeNewborn(world, baby, mother, father);

        baby.JoinCity(mother.City);
        world.AddNpc(baby);
        if (world.OntogenyRules.Enabled)
            baby.SetDevelopment(LivingWorld.Domain.Ontogeny.DevelopmentState.Newborn);
        baby.ConfigureNeedDecay(world.NeedsRules, world.CurrentDate.TotalHours);
        household.AddMember(baby.Id);
        LivingWorld.Simulation.Health.Lactation.TryNurse(world, baby, ctx.CurrentTick); // primeira mamada
        long birthEventId = ctx.LogEvent(
            WorldEventKind.Birth,
            $"{baby.Id.Value}|{motherId.Value}|{fatherId.Value}|{household.Id.Value}",
            sourceSystem: "NatalitySystem");
        TryApplyPowerInheritance(world, ctx, baby, motherId, fatherId, birthEventId);
        NpcInstantiationMechanic.ApplyPendingReincarnation(world, ctx, baby);
        MortalitySystem.CoverUntilNextSweep(world, ctx, baby);
        NpcWakeScheduler.ScheduleWake(world, ctx, baby.Id.Value, world.CurrentDate.TotalHours + 1);
    }

    /// <summary>Se ambos os pais são portadores, resolve herança e audita
    /// <see cref="WorldEventKind.PowerInherited"/>. Sem dois portadores — no-op O(1).</summary>
    internal static void TryApplyPowerInheritance(
        WorldState world, TickContext ctx, Npc baby, NpcId motherId, NpcId fatherId,
        long? causeEventId = null)
    {
        if (!PowerInheritanceResolver.IsPowerCarrier(world, motherId)
            || !PowerInheritanceResolver.IsPowerCarrier(world, fatherId))
            return;

        var decision = PowerInheritanceResolver.Decide(
            world, baby.Id, motherId, fatherId, world.Extraordinary.InheritanceRules);
        if (!decision.Occurred || decision.Outcome is null)
            return;

        var descriptors = PowerInheritanceResolver.ResolveDescriptors(
            world, baby.Id, motherId, fatherId, world.Extraordinary.InheritanceRules);
        foreach (var descriptor in descriptors)
            world.Extraordinary.EnsureDescriptor(descriptor);

        if (descriptors.Count > 0)
        {
            world.UpsertExtraordinaryCarrier(new ExtraordinaryCarrierState(
                baby.Id,
                descriptors.Select(d => d.Id).OrderBy(id => id, StringComparer.Ordinal).ToList(),
                IsManifested: false,
                ManifestationState: "dormant",
                Appearance: new ExtraordinaryAppearanceState(1, "", ""),
                NeedSubstitution: null,
                SenescenceRateMultiplier: 1));
        }

        string idsCsv = string.Join(",", descriptors.Select(d => d.Id));
        ctx.LogEvent(
            WorldEventKind.PowerInherited,
            $"{baby.Id.Value}|{motherId.Value}|{fatherId.Value}|{decision.Outcome}|{idsCsv}",
            sourceSystem: "NatalitySystem",
            causeEventId: causeEventId);
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
