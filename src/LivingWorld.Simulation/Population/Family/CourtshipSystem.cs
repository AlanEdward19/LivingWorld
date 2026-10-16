using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Family;

/// <summary>Score de atração, gates de elegibilidade e cortejo agendado.</summary>
[LivingCapability("RELATIONSHIPS")]
public sealed class CourtshipSystem : ISimulationSystem
{
    private static readonly AttractionFactor[] AttractionFactors = Enum.GetValues<AttractionFactor>();
    private static readonly RelationshipAxis[] RelationshipAxes = Enum.GetValues<RelationshipAxis>();
    public const string SystemName = "population-courtship";

    /// <summary>Escala compartilhada de normalização para eixos <c>[0,100]</c> (Health,
    /// Relationship, Skills no cap default do cenário).</summary>
    private const double HundredScale = 100.0;

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Yearly;

    public void Tick(WorldState world, TickContext ctx)
    {
        var familyRules = world.FamilyRules;
        var populationRules = world.PopulationRules;
        var lifeStageRules = world.LifeStageRules;
        var now = world.CurrentDate;
        Dictionary<NpcId, HashSet<NpcId>>? acquaintances = null;

        foreach (var seeker in world.Npcs
                     .Where(n => IsSeekerEligible(n, world, lifeStageRules, now))
                     .OrderBy(n => n.Id.Value))
        {
            // OrderBy materializa o filtro inicial; um pretendente anterior pode ter escolhido este NPC.
            if (!IsSeekerEligible(seeker, world, lifeStageRules, now)) continue;
            acquaintances ??= IndexAcquaintances(world);
            var candidates = CollectCandidates(world, seeker.Id, lifeStageRules, now, acquaintances);
            if (candidates.Count == 0)
                continue;

            if (familyRules.NeutralDriftEnabled)
            {
                TryNeutralDriftPairing(world, ctx, seeker, candidates, populationRules, now, familyRules);
                continue;
            }

            var best = PickBestByAttraction(world, seeker, candidates, familyRules, populationRules, now);
            if (best is null)
                continue;

            TryStartOrReject(world, ctx, seeker, best, populationRules, now, familyRules);
        }
    }

    public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt)
    {
        var parts = evt.Payload!.Split('|');
        var idA = new NpcId(long.Parse(parts[0]));
        var idB = new NpcId(long.Parse(parts[1]));

        var a = world.FindNpc(idA);
        var b = world.FindNpc(idB);
        if (a is null || b is null)
        {
            a?.EndCourtship();
            b?.EndCourtship();
            return;
        }

        if (a is not { IsAlive: true } || b is not { IsAlive: true })
        {
            if (a.IsAlive) a.EndCourtship();
            if (b.IsAlive) b.EndCourtship();
            return;
        }

        if (a.CourtingWith != b.Id || b.CourtingWith != a.Id)
        {
            a.EndCourtship();
            b.EndCourtship();
            return;
        }

        if (!IsSingle(a, world) || !IsSingle(b, world))
        {
            a.EndCourtship();
            b.EndCourtship();
            return;
        }

        ctx.LogEvent(WorldEventKind.CourtshipSucceeded, OrderedPairPayload(a.Id, b.Id), sourceSystem: "CourtshipSystem");
        MarriageSystem.Marry(world, ctx, a, b);
        a.EndCourtship();
        b.EndCourtship();
    }

    public static CourtshipRejectionReason? Reject(
        Npc a, Npc b, WorldDate now, PopulationRules populationRules)
    {
        if (IsFirstDegreeRelative(a, b))
            return CourtshipRejectionReason.Incesto;

        if (!IsFertilityCompatible(a, b, populationRules, now))
            return CourtshipRejectionReason.ForaDaFaixaEtaria;

        return null;
    }

    public static double AttractionScore(
        Npc a,
        Npc b,
        Relationship? aToB,
        Relationship? bToA,
        FamilyRules rules,
        PopulationRules populationRules,
        WorldDate now)
    {
        double totalWeight = 0;
        double weightedSum = 0;

        foreach (var factor in AttractionFactors)
        {
            double weight = rules.AttractionWeight(factor);
            if (weight == 0)
                continue;
            // sem aparência (mundo sem AppearanceRules) o fator não existe: não entra na média, o score é o de antes
            if (factor == AttractionFactor.Appearance && (a.Appearance is null || b.Appearance is null))
                continue;

            double normalized = NormalizeFactor(factor, a, b, aToB, bToA, populationRules, now);
            weightedSum += weight * normalized;
            totalWeight += weight;
        }

        return totalWeight == 0 ? 0 : weightedSum / totalWeight;
    }

    internal static bool IsFirstDegreeRelative(Npc a, Npc b)
    {
        if (a.Id == b.MotherId || a.Id == b.FatherId || b.Id == a.MotherId || b.Id == a.FatherId)
            return true;

        return a.MotherId == b.MotherId
               && a.FatherId == b.FatherId
               && a.MotherId is not null
               && a.FatherId is not null;
    }

    private static void TryNeutralDriftPairing(
        WorldState world,
        TickContext ctx,
        Npc seeker,
        List<Npc> candidates,
        PopulationRules populationRules,
        WorldDate now,
        FamilyRules familyRules)
    {
        var eligible = candidates
            .Where(c => Reject(seeker, c, now, populationRules) is null)
            .OrderBy(c => c.Id.Value)
            .ToList();
        if (eligible.Count == 0)
            return;

        int index = (int)(ctx.Rng($"courtship-drift-{seeker.Id.Value}-{now.TotalHours}").NextDouble() * eligible.Count);
        if (index >= eligible.Count)
            index = eligible.Count - 1;

        StartCourtship(world, ctx, seeker, eligible[index], familyRules);
    }

    private static void TryStartOrReject(
        WorldState world,
        TickContext ctx,
        Npc seeker,
        Npc candidate,
        PopulationRules populationRules,
        WorldDate now,
        FamilyRules familyRules)
    {
        var reject = Reject(seeker, candidate, now, populationRules);
        if (reject is { } reason)
        {
            ctx.LogEvent(
                WorldEventKind.CourtshipRejected,
                $"{reason}|{seeker.Id.Value}|{candidate.Id.Value}", sourceSystem: "CourtshipSystem");
            return;
        }

        var aToB = world.Relationships.GetValueOrDefault(new RelationshipKey(seeker.Id, candidate.Id));
        var bToA = world.Relationships.GetValueOrDefault(new RelationshipKey(candidate.Id, seeker.Id));
        double score = AttractionScore(seeker, candidate, aToB, bToA, familyRules, populationRules, now);
        if (score < familyRules.CourtshipThreshold)
        {
            ctx.LogEvent(
                WorldEventKind.CourtshipRejected,
                $"{CourtshipRejectionReason.SemAfinidade}|{seeker.Id.Value}|{candidate.Id.Value}", sourceSystem: "CourtshipSystem");
            return;
        }

        StartCourtship(world, ctx, seeker, candidate, familyRules);
    }

    private static void StartCourtship(WorldState world, TickContext ctx, Npc a, Npc b, FamilyRules familyRules)
    {
        a.StartCourtship(b.Id);
        b.StartCourtship(a.Id);
        ctx.LogEvent(WorldEventKind.CourtshipStarted, OrderedPairPayload(a.Id, b.Id), sourceSystem: "CourtshipSystem");

        var dueDate = world.CurrentDate.AddDays(familyRules.CourtshipDurationDays);
        ctx.ScheduleEvent(dueDate.TotalHours, SystemName, OrderedPairPayload(a.Id, b.Id));
    }

    private static Npc? PickBestByAttraction(
        WorldState world,
        Npc seeker,
        List<Npc> candidates,
        FamilyRules familyRules,
        PopulationRules populationRules,
        WorldDate now)
    {
        Npc? best = null;
        double bestScore = double.NegativeInfinity;
        bool hasCompatibleCandidate = candidates.Any(c => Reject(seeker, c, now, populationRules) is null);

        foreach (var candidate in candidates.OrderBy(c => c.Id.Value))
        {
            // Um irmão ou parceiro fora da janela não pode bloquear todos os vínculos válidos.
            // Sem alternativa válida, preserva a rejeição nomeada observável do melhor candidato.
            if (hasCompatibleCandidate && Reject(seeker, candidate, now, populationRules) is not null) continue;
            var aToB = world.Relationships.GetValueOrDefault(new RelationshipKey(seeker.Id, candidate.Id));
            var bToA = world.Relationships.GetValueOrDefault(new RelationshipKey(candidate.Id, seeker.Id));
            double score = AttractionScore(seeker, candidate, aToB, bToA, familyRules, populationRules, now);
            if (score > bestScore || (score == bestScore && best is not null && candidate.Id.Value < best.Id.Value))
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private static Dictionary<NpcId, HashSet<NpcId>> IndexAcquaintances(WorldState world)
    {
        var index = new Dictionary<NpcId, HashSet<NpcId>>();
        // Índice derivado, sem efeitos no mundo; a ordem é aplicada antes de escolher um candidato.
        foreach (var key in world.Relationships.Keys)
        {
            if (!index.TryGetValue(key.From, out var from)) index[key.From] = from = [];
            if (!index.TryGetValue(key.To, out var to)) index[key.To] = to = [];
            from.Add(key.To);
            to.Add(key.From);
        }
        return index;
    }

    private static List<Npc> CollectCandidates(
        WorldState world, NpcId seekerId, LifeStageRules lifeStageRules, WorldDate now,
        Dictionary<NpcId, HashSet<NpcId>> acquaintances)
    {
        var result = new List<Npc>();
        if (!acquaintances.TryGetValue(seekerId, out var known)) return result;
        foreach (var partnerId in known.OrderBy(id => id.Value))
        {
            var candidate = world.FindNpc(partnerId);
            if (candidate is not null && IsCandidateEligible(candidate, world, lifeStageRules, now)) result.Add(candidate);
        }
        return result;
    }

    private static bool IsSeekerEligible(Npc npc, WorldState world, LifeStageRules lifeStageRules, WorldDate now) =>
        npc.IsAlive
        && IsAdult(npc, lifeStageRules, now)
        && IsSingle(npc, world)
        && npc.CourtingWith is null;

    private static bool IsCandidateEligible(Npc npc, WorldState world, LifeStageRules lifeStageRules, WorldDate now) =>
        npc.IsAlive
        && IsAdult(npc, lifeStageRules, now)
        && IsSingle(npc, world)
        && npc.CourtingWith is null;

    private static bool IsAdult(Npc npc, LifeStageRules lifeStageRules, WorldDate now)
    {
        var stage = lifeStageRules.LifeStageOf(npc.AgeYears(now));
        return stage is LifeStage.Adult or LifeStage.Elder;
    }

    private static bool IsSingle(Npc npc, WorldState world)
    {
        if (npc.Spouse is not { } spouseId)
            return true;

        var spouse = world.FindNpc(spouseId);
        return spouse is not { IsAlive: true };
    }

    private static bool IsFertilityCompatible(Npc a, Npc b, PopulationRules rules, WorldDate now)
    {
        if (a.Sex == b.Sex)
            return false;

        return rules.IsFertileAge(a.AgeYears(now)) && rules.IsFertileAge(b.AgeYears(now));
    }

    private static double NormalizeFactor(
        AttractionFactor factor,
        Npc a,
        Npc b,
        Relationship? aToB,
        Relationship? bToA,
        PopulationRules populationRules,
        WorldDate now)
    {
        return factor switch
        {
            AttractionFactor.Age => PairAgeFactor(a, b, populationRules, now),
            AttractionFactor.Health => (a.Health / HundredScale + b.Health / HundredScale) / 2.0,
            AttractionFactor.Status => (StatusFactor(a) + StatusFactor(b)) / 2.0,
            AttractionFactor.Skill => (SkillFactor(a) + SkillFactor(b)) / 2.0,
            AttractionFactor.CulturalAffinity => CulturalAffinityOf(a, b),
            AttractionFactor.ExistingRelationship => ExistingRelationshipFactor(aToB, bToA),
            AttractionFactor.Appearance => Appearance.Similarity(a.Appearance!, b.Appearance!),
            _ => 0.0,
        };
    }

    /// <summary>Afinidade cultural pelo idioma que cada um fala (criação, Fase 21c), não pela etnia de nascimento.</summary>
    internal static double CulturalAffinityOf(Npc a, Npc b) =>
        LivingWorld.Simulation.Ontogeny.OntogenyMechanic.SpokenCulture(a) == LivingWorld.Simulation.Ontogeny.OntogenyMechanic.SpokenCulture(b) ? 1.0 : 0.0;

    private static double PairAgeFactor(Npc a, Npc b, PopulationRules rules, WorldDate now)
    {
        double fa = IndividualAgeFactor(a.AgeYears(now), rules);
        double fb = IndividualAgeFactor(b.AgeYears(now), rules);
        return (fa + fb) / 2.0;
    }

    private static double IndividualAgeFactor(int ageYears, PopulationRules rules)
    {
        if (!rules.IsFertileAge(ageYears))
            return 0.0;

        int min = rules.FertilityMinAge;
        int max = rules.FertilityMaxAge;
        int mid = (min + max) / 2;
        int halfSpan = Math.Max(1, (max - min) / 2);
        double distance = Math.Abs(ageYears - mid);
        return Math.Clamp(1.0 - distance / halfSpan, 0.0, 1.0);
    }

    private static double StatusFactor(Npc npc)
    {
        double wealth = Math.Min(1.0, npc.Wallet.Amount / HundredScale);
        double profession = npc.Profession != ProfessionType.None ? 1.0 : 0.5;
        return (wealth + profession) / 2.0;
    }

    // ponytail: normaliza sobre os mesmos 13 ids fixos (0..12) — teto histórico
    // preservado como constante de normalização, não decisão sobre quais habilidades existem
    // (SkillType/SkillSet aceitam id livre, mas nenhum período hoje declara catálogo
    // de habilidades maior/menor que esse). Se um período vier a declarar mais/menos
    // habilidades relevantes pro score de atração, virar contagem data-driven (ex.: via
    // SkillsRules) é o upgrade natural.
    private static readonly SkillType[] KnownSkillTypesForScoring =
        Enumerable.Range(0, 13).Select(id => new SkillType(id)).ToArray();

    private static double SkillFactor(Npc npc)
    {
        double sum = 0;
        foreach (var skill in KnownSkillTypesForScoring)
            sum += npc.Skills.Get(skill) / HundredScale;

        return sum / KnownSkillTypesForScoring.Length;
    }

    private static double ExistingRelationshipFactor(Relationship? aToB, Relationship? bToA)
    {
        static double SideAverage(Relationship? rel)
        {
            if (rel is null)
                return 0.0;

            double sum = 0;
            foreach (var axis in RelationshipAxes)
                sum += rel.Get(axis) / HundredScale;
            return sum / RelationshipAxes.Length;
        }

        return (SideAverage(aToB) + SideAverage(bToA)) / 2.0;
    }

    private static string OrderedPairPayload(NpcId a, NpcId b) =>
        a.Value <= b.Value ? $"{a.Value}|{b.Value}" : $"{b.Value}|{a.Value}";
}
