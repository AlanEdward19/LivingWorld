using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Population;

/// <summary>Gerador de população inicial: pirâmide etária coerente — crianças, jovens,
/// adultos e idosos, sexo 50/50, casais pareados e crianças distribuídas em households. Nada de
/// N adultos da mesma idade. Puro: toda aleatoriedade vem do <see cref="WorldRng"/> recebido.</summary>
public static class PopulationGenerator
{
    public sealed record GeneratedPopulation(
        IReadOnlyList<Npc> Npcs, IReadOnlyList<Household> Households, long NextNpcId, long NextHouseholdId);

    private sealed record AgeBracket(int MinAge, int MaxAge, double Weight);

    // Pesos da pirâmide etária: algoritmo interno, não dado de cenário — a geração pede só
    // "coerente", não uma pirâmide configurável. Elder é limitado por MaxLongevityYears abaixo.
    private static readonly AgeBracket[] Pyramid =
    [
        new(0, 14, 0.28),
        new(15, 17, 0.07),
        new(18, 59, 0.50),
        new(60, 200, 0.15), // teto real vem de LifeTable.MaxLongevityYears - 1
    ];

    public static GeneratedPopulation GenerateInitial(
        WorldRng rng, WorldDate now, int count, CultureId culture, CellCoord villageLocation,
        LifeTable lifeTable, PopulationCatalog catalog, long startingNpcId = 0, long startingHouseholdId = 0,
        CityId city = default,
        Func<int, IReadOnlyList<CellCoord>>? householdLocationsFactory = null,
        BodyRules? bodyRules = null)
    {
        long nextNpcId = startingNpcId;
        long nextHouseholdId = startingHouseholdId;
        var body = BodyRules.Resolve(bodyRules);

        var npcs = new List<Npc>(count);
        var adults = new List<Npc>();
        var children = new List<Npc>();

        for (int i = 0; i < count; i++)
        {
            int ageYears = RollAge(rng, lifeTable.MaxLongevityYears);
            var sex = rng.NextDouble() < 0.5 ? Sex.Female : Sex.Male;
            var birthDate = now.AddYears(-ageYears);
            int health = ageYears >= 60 ? Math.Clamp(100 - (ageYears - 59) * 2, 40, 100) : 100;
            var npcId = new NpcId(nextNpcId++);

            // Streams próprios por NPC — derivados do stream "population-init"
            // já recebido em rng, nunca um novo WorldRng raiz. WorldRngRegistry.StableHash garante
            // a mesma chave string→long usada por TickContext.Rng, então nascimento em runtime
            // (NatalitySystem) e geração inicial produzem streams com a mesma convenção.
            var personality = Personality.RollFrom(rng.Derive(WorldRngRegistry.StableHash($"personality-{npcId.Value}")));
            var profession = catalog.RollProfession(rng.Derive(WorldRngRegistry.StableHash($"profession-{npcId.Value}")));
            // População seed não tem pais conhecidos — RollInitial em vez
            // de Inherit, mesmo stream próprio por NPC de personalidade/profissão acima.
            var rateGene = RateGene.RollInitial(rng.Derive(WorldRngRegistry.StableHash($"rategene-{npcId.Value}")));
            var vitality = HeredityService.RollInitialVitality(
                rng.Derive(WorldRngRegistry.StableHash($"vitality-{npcId.Value}")));
            var upbringing = HeredityService.RollInitialUpbringing(
                rng.Derive(WorldRngRegistry.StableHash($"upbringing-{npcId.Value}")));
            // Corpo mínimo — streams irmãos height-/weight-/musclemass-.
            var height = BodyGeneration.RollHeight(
                rng.Derive(WorldRngRegistry.StableHash($"height-{npcId.Value}")), body);
            var weight = BodyGeneration.RollWeight(
                rng.Derive(WorldRngRegistry.StableHash($"weight-{npcId.Value}")), body);
            var muscleMass = BodyGeneration.RollMuscleMass(
                rng.Derive(WorldRngRegistry.StableHash($"musclemass-{npcId.Value}")), body);

            var npc = new Npc(
                npcId, $"npc-{culture.Id}-{npcs.Count}", sex, birthDate, culture, villageLocation,
                motherId: null, fatherId: null, household: null, health,
                personality: personality, profession: profession, currentLocation: villageLocation,
                rateGene: rateGene, vitality: vitality, upbringing: upbringing, city: city,
                height: height, weight: weight, muscleMass: muscleMass);

            npcs.Add(npc);
            (ageYears >= 18 ? adults : children).Add(npc);
        }

        var households = PairIntoHouseholds(
            adults, children, villageLocation, ref nextHouseholdId, city, householdLocationsFactory);

        return new GeneratedPopulation(npcs, households, nextNpcId, nextHouseholdId);
    }

    /// <summary>População autorada (ADR-0024): nome, sexo, idade, profissão, personalidade e parentesco vêm do autor;
    /// genes, corpo, vitalidade e criação seguem sorteados pelos mesmos streams por NPC do gerador inicial, então
    /// mesma seed + mesmo bloco = mesmo mundo. Cada família vira uma <see cref="Household"/> com a casa dada por
    /// <paramref name="householdLocationsFactory"/>; a cabeça é o membro adulto mais velho.</summary>
    public static GeneratedPopulation GenerateAuthored(
        WorldRng rng, WorldDate now, IReadOnlyList<AuthoredHousehold> authored, CultureId culture,
        CellCoord villageLocation, long startingNpcId, long startingHouseholdId, CityId city,
        Func<int, IReadOnlyList<CellCoord>> householdLocationsFactory, BodyRules? bodyRules = null)
    {
        long nextNpcId = startingNpcId;
        long nextHouseholdId = startingHouseholdId;
        var body = BodyRules.Resolve(bodyRules);
        var npcs = new List<Npc>();
        var groups = new List<List<Npc>>();

        foreach (var family in authored)
        {
            long firstId = nextNpcId;
            var members = new List<Npc>(family.Members.Count);
            for (int i = 0; i < family.Members.Count; i++)
            {
                var person = family.Members[i];
                var npcId = new NpcId(nextNpcId++);
                int health = person.AgeYears >= 60 ? Math.Clamp(100 - (person.AgeYears - 59) * 2, 40, 100) : 100;
                var rolledPersonality = Personality.RollFrom(rng.Derive(WorldRngRegistry.StableHash($"personality-{npcId.Value}")));
                var rateGene = RateGene.RollInitial(rng.Derive(WorldRngRegistry.StableHash($"rategene-{npcId.Value}")));
                var vitality = HeredityService.RollInitialVitality(rng.Derive(WorldRngRegistry.StableHash($"vitality-{npcId.Value}")));
                var upbringing = HeredityService.RollInitialUpbringing(rng.Derive(WorldRngRegistry.StableHash($"upbringing-{npcId.Value}")));
                var height = BodyGeneration.RollHeight(rng.Derive(WorldRngRegistry.StableHash($"height-{npcId.Value}")), body);
                var weight = BodyGeneration.RollWeight(rng.Derive(WorldRngRegistry.StableHash($"weight-{npcId.Value}")), body);
                var muscleMass = BodyGeneration.RollMuscleMass(rng.Derive(WorldRngRegistry.StableHash($"musclemass-{npcId.Value}")), body);

                var npc = new Npc(
                    npcId, person.Name, person.Sex, now.AddYears(-person.AgeYears), culture, villageLocation,
                    motherId: person.Mother is int m ? new NpcId(firstId + m) : null,
                    fatherId: person.Father is int f ? new NpcId(firstId + f) : null,
                    household: null, health,
                    personality: person.Personality ?? rolledPersonality, profession: new ProfessionType(person.ProfessionId),
                    currentLocation: villageLocation, rateGene: rateGene, vitality: vitality, upbringing: upbringing,
                    city: city, height: height, weight: weight, muscleMass: muscleMass);
                members.Add(npc);
                npcs.Add(npc);
            }
            for (int i = 0; i < family.Members.Count; i++)
                if (family.Members[i].Spouse is int spouse && !members[i].Spouse.HasValue)
                {
                    members[i].Marry(members[spouse].Id);
                    members[spouse].Marry(members[i].Id);
                }
            groups.Add(members);
        }

        var locations = householdLocationsFactory(groups.Count);
        var households = new List<Household>(groups.Count);
        for (int g = 0; g < groups.Count; g++)
        {
            var head = groups[g].OrderByDescending(n => n.AgeYears(now)).ThenBy(n => n.Id.Value).First();
            var household = new Household(
                new HouseholdId(nextHouseholdId++), locations[g], head.Id, groups[g].Select(m => m.Id).ToList(), city: city);
            foreach (var member in groups[g])
            {
                member.JoinHousehold(household.Id);
                member.MoveTo(locations[g], member.ActionStartedAtTick);
            }
            households.Add(household);
        }
        return new GeneratedPopulation(npcs, households, nextNpcId, nextHouseholdId);
    }

    private static int RollAge(WorldRng rng, int maxLongevityYears)
    {
        double roll = rng.NextDouble();
        double cumulative = 0;
        foreach (var bracket in Pyramid)
        {
            cumulative += bracket.Weight;
            if (roll <= cumulative)
            {
                int maxAge = Math.Min(bracket.MaxAge, maxLongevityYears - 1);
                int minAge = Math.Min(bracket.MinAge, maxAge);
                double span = rng.NextDouble();
                return minAge + (int)(span * (maxAge - minAge + 1));
            }
        }
        return Math.Min(Pyramid[^1].MinAge, maxLongevityYears - 1);
    }

    private static List<Household> PairIntoHouseholds(
        List<Npc> adults, List<Npc> children, CellCoord location, ref long nextHouseholdId, CityId city,
        Func<int, IReadOnlyList<CellCoord>>? householdLocationsFactory)
    {
        var females = new Queue<Npc>(adults.Where(a => a.Sex == Sex.Female));
        var males = new Queue<Npc>(adults.Where(a => a.Sex == Sex.Male));

        var seeds = new List<List<Npc>>();
        while (females.Count > 0 && males.Count > 0)
        {
            var female = females.Dequeue();
            var male = males.Dequeue();
            female.Marry(male.Id);
            male.Marry(female.Id);
            seeds.Add([female, male]);
        }
        while (females.Count > 0)
            seeds.Add([females.Dequeue()]);
        while (males.Count > 0)
            seeds.Add([males.Dequeue()]);

        // Sem adulto nenhum (só crianças, caso extremo de contagem pequena): cada criança vira
        // a própria head — não há como formar um casal fundador nesse caso.
        if (seeds.Count == 0)
            foreach (var child in children)
                seeds.Add([child]);
        else
            for (int i = 0; i < children.Count; i++)
                seeds[i % seeds.Count].Add(children[i]);

        var householdLocations = householdLocationsFactory?.Invoke(seeds.Count)
            ?? Enumerable.Repeat(location, seeds.Count).ToArray();
        if (householdLocations.Count != seeds.Count)
            throw new ArgumentException(
                "Household location count must equal the generated household count.",
                nameof(householdLocationsFactory));

        var households = new List<Household>(seeds.Count);
        for (int householdIndex = 0; householdIndex < seeds.Count; householdIndex++)
        {
            var members = seeds[householdIndex];
            var householdLocation = householdLocations[householdIndex];
            var head = members.OrderBy(m => m.Id.Value).First();
            var household = new Household(new HouseholdId(nextHouseholdId++), householdLocation, head.Id, members.Select(m => m.Id).ToList(), city: city);
            foreach (var member in members)
            {
                member.JoinHousehold(household.Id);
                member.MoveTo(householdLocation, member.ActionStartedAtTick);
            }
            households.Add(household);
        }
        return households;
    }
}
