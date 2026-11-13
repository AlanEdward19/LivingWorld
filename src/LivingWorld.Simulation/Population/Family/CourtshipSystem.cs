using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Body;
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

    /// <summary>Ascendência por pessoa, só durante um tick (derivada): o tabu de parentesco a consulta para cada par.</summary>
    private readonly Dictionary<long, HashSet<long>> _lineage = [];

    public void Tick(WorldState world, TickContext ctx)
    {
        var familyRules = world.FamilyRules;
        var populationRules = world.PopulationRules;
        var lifeStageRules = world.LifeStageRules;
        var now = world.CurrentDate;
        Dictionary<NpcId, HashSet<NpcId>>? acquaintances = null;
        _lineage.Clear();

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
                TryNeutralDriftPairing(world, ctx, seeker, candidates, populationRules, now, familyRules, _lineage);
                continue;
            }

            var best = PickBestByAttraction(world, seeker, candidates, familyRules, populationRules, now, _lineage);
            if (best is null)
                continue;

            TryStartOrReject(world, ctx, seeker, best, populationRules, now, familyRules, _lineage);
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

    /// <summary>Recusa no mundo (AD-136): além do 1º grau e da idade, o tabu de parentesco do cenário pela genealogia.</summary>
    internal static CourtshipRejectionReason? Reject(
        WorldState world, Npc a, Npc b, WorldDate now, PopulationRules populationRules, Dictionary<long, HashSet<long>>? lineage)
    {
        if (Reject(a, b, now, populationRules) is { } reason) return reason;
        return Kinship.IsTaboo(world, a, b, lineage) ? CourtshipRejectionReason.Incesto : null;
    }

    /// <summary>O quanto <paramref name="evaluator"/> quer <paramref name="target"/> (AD-136, achado 24): cada um avalia o
    /// que o outro tem — saúde, posição, habilidade, a fertilidade da idade, a afinidade de língua, o próprio vínculo com ele e
    /// a aparência — e quem é ambicioso pesa mais a posição. Assimétrico: os dois só namoram se cada um quiser o outro.</summary>
    public static double Preference(
        Npc evaluator, Npc target, Relationship? evaluatorToTarget, FamilyRules rules, PopulationRules populationRules, WorldDate now)
    {
        double totalWeight = 0, weightedSum = 0;
        foreach (var factor in AttractionFactors)
        {
            double weight = rules.AttractionWeight(factor);
            if (weight == 0) continue;
            if (factor == AttractionFactor.Appearance && (evaluator.Appearance is null || target.Appearance is null)) continue;
            if (factor == AttractionFactor.Status) weight *= 0.5 + evaluator.Personality.Ambition / HundredScale;
            double value = factor switch
            {
                AttractionFactor.Age => IndividualAgeFactor(target, populationRules, now) * AgeFit(evaluator, target, rules.Unions, now),
                AttractionFactor.Health => target.Health / HundredScale,
                AttractionFactor.Status => StatusFactor(target),
                AttractionFactor.Skill => SkillFactor(target),
                AttractionFactor.CulturalAffinity => CulturalAffinityOf(evaluator, target),
                AttractionFactor.ExistingRelationship => SideOf(evaluatorToTarget, rules.Bonds is not null),
                AttractionFactor.Appearance => Appearance.Similarity(evaluator.Appearance!, target.Appearance!),
                _ => 0.0,
            };
            weightedSum += weight * value;
            totalWeight += weight;
        }
        return totalWeight == 0 ? 0 : weightedSum / totalWeight;
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

            double normalized = NormalizeFactor(factor, a, b, aToB, bToA, populationRules, now, rules.Bonds is not null);
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
        FamilyRules familyRules,
        Dictionary<long, HashSet<long>> lineage)
    {
        var eligible = candidates
            .Where(c => Reject(world, seeker, c, now, populationRules, lineage) is null)
            .OrderBy(c => c.Id.Value)
            .ToList();
        if (eligible.Count == 0)
            return;

        int index = (int)(ctx.OneShot($"courtship-drift-{seeker.Id.Value}-{now.TotalHours}").NextDouble() * eligible.Count);
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
        FamilyRules familyRules,
        Dictionary<long, HashSet<long>> lineage)
    {
        var reject = Reject(world, seeker, candidate, now, populationRules, lineage);
        if (reject is { } reason)
        {
            ctx.LogEvent(
                WorldEventKind.CourtshipRejected,
                $"{reason}|{seeker.Id.Value}|{candidate.Id.Value}", sourceSystem: "CourtshipSystem");
            return;
        }

        var aToB = world.Relationships.GetValueOrDefault(new RelationshipKey(seeker.Id, candidate.Id));
        var bToA = world.Relationships.GetValueOrDefault(new RelationshipKey(candidate.Id, seeker.Id));
        // cada um tem de querer o outro (AD-136)
        double wants = Preference(seeker, candidate, aToB, familyRules, populationRules, now);
        double wanted = Preference(candidate, seeker, bToA, familyRules, populationRules, now);
        double seekerThreshold = ThresholdOf(world, seeker), candidateThreshold = ThresholdOf(world, candidate);
        if (wants < seekerThreshold || wanted < candidateThreshold)
        {
            ctx.LogEvent(
                WorldEventKind.CourtshipRejected,
                $"{CourtshipRejectionReason.SemAfinidade}|{seeker.Id.Value}|{candidate.Id.Value}", sourceSystem: "CourtshipSystem");
            return;
        }

        // AD-145: os dois se querem, mas cada um pesa se o casal tem com o que montar casa (trabalho, economias)
        if (wants * HouseholdMeans.MarriageReadiness(world, seeker, candidate) < seekerThreshold
            || wanted * HouseholdMeans.MarriageReadiness(world, candidate, seeker) < candidateThreshold)
        {
            ctx.LogEvent(
                WorldEventKind.CourtshipRejected,
                $"{CourtshipRejectionReason.SemMeios}|{seeker.Id.Value}|{candidate.Id.Value}", sourceSystem: "CourtshipSystem");
            return;
        }

        StartCourtship(world, ctx, seeker, candidate, familyRules);
    }

    /// <summary>Exigência para aceitar um par (AD-139): a do cenário, menor no jovem no pico do impulso da adolescência
    /// (escolha precipitada).</summary>
    internal static double ThresholdOf(WorldState world, Npc npc)
    {
        double threshold = world.FamilyRules.CourtshipThreshold;
        var rules = Endocrine.Rules(world);
        if (!rules.Enabled || rules.PrecipitousCourtship <= 0) return threshold;
        return threshold * (1 - rules.PrecipitousCourtship * Endocrine.AdolescentDrive(world, npc, world.CurrentDate.TotalHours));
    }

    private static void StartCourtship(WorldState world, TickContext ctx, Npc a, Npc b, FamilyRules familyRules)
    {
        a.StartCourtship(b.Id);
        b.StartCourtship(a.Id);
        ctx.LogEvent(WorldEventKind.CourtshipStarted, OrderedPairPayload(a.Id, b.Id), sourceSystem: "CourtshipSystem");

        var dueDate = world.CurrentDate.AddDays(familyRules.CourtshipDurationDays);
        ctx.ScheduleEvent(dueDate.TotalHours, SystemName, OrderedPairPayload(a.Id, b.Id));
    }

    /// <summary>O melhor para o pretendente (pela preferência dele) entre os que o aceitariam; sem ninguém que o aceite, o
    /// que ele mais quer (a recusa fica registrada). Candidato vetado (parentesco, idade) não bloqueia os válidos.</summary>
    private static Npc? PickBestByAttraction(
        WorldState world,
        Npc seeker,
        List<Npc> candidates,
        FamilyRules familyRules,
        PopulationRules populationRules,
        WorldDate now,
        Dictionary<long, HashSet<long>> lineage)
    {
        Npc? best = null, bestAccepting = null;
        double bestScore = double.NegativeInfinity, bestAcceptingScore = double.NegativeInfinity;
        Npc? firstRejected = null;

        foreach (var candidate in candidates.OrderBy(c => c.Id.Value))
        {
            if (Reject(world, seeker, candidate, now, populationRules, lineage) is not null)
            {
                firstRejected ??= candidate;
                continue;
            }
            var aToB = world.Relationships.GetValueOrDefault(new RelationshipKey(seeker.Id, candidate.Id));
            var bToA = world.Relationships.GetValueOrDefault(new RelationshipKey(candidate.Id, seeker.Id));
            double score = Preference(seeker, candidate, aToB, familyRules, populationRules, now);
            if (score > bestScore) (bestScore, best) = (score, candidate);
            // AD-145: entre os que o aceitariam, o melhor com os meios do casal pesados pelos dois
            double ready = score * HouseholdMeans.MarriageReadiness(world, seeker, candidate);
            if (Preference(candidate, seeker, bToA, familyRules, populationRules, now) * HouseholdMeans.MarriageReadiness(world, candidate, seeker)
                    >= ThresholdOf(world, candidate)
                && ready > bestAcceptingScore)
                (bestAcceptingScore, bestAccepting) = (ready, candidate);
        }

        // Sem alternativa válida, preserva a rejeição nomeada observável do primeiro candidato vetado.
        return bestAccepting ?? best ?? firstRejected;
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
        if (world.FamilyRules.Bonds is { } bonds)
        {
            foreach (var partnerId in ProspectsOf(world, seekerId, bonds, acquaintances).OrderBy(id => id.Value))
                if (world.FindNpc(partnerId) is { } prospect && IsCandidateEligible(prospect, world, lifeStageRules, now)) result.Add(prospect);
            return result;
        }

        if (!acquaintances.TryGetValue(seekerId, out var known)) return result;
        foreach (var partnerId in known.OrderBy(id => id.Value))
        {
            var candidate = world.FindNpc(partnerId);
            if (candidate is not null && IsCandidateEligible(candidate, world, lifeStageRules, now)) result.Add(candidate);
        }
        return result;
    }

    /// <summary>De quem um solteiro sabe (AD-128): quem ele conhece, quem os parentes e os amigos dele conhecem (é assim que
    /// se é apresentado) e, numa comunidade em que todo mundo se conhece de vista, todos os moradores.</summary>
    private static HashSet<NpcId> ProspectsOf(
        WorldState world, NpcId seekerId, SocialBondRules bonds, Dictionary<NpcId, HashSet<NpcId>> acquaintances)
    {
        var prospects = new HashSet<NpcId>();
        if (world.FindNpc(seekerId) is not { } seeker) return prospects;

        acquaintances.TryGetValue(seekerId, out var known);
        if (known is not null) prospects.UnionWith(known);

        void Introduce(NpcId? through)
        {
            if (through is { } id && acquaintances.TryGetValue(id, out var theirs)) prospects.UnionWith(theirs);
        }

        Introduce(seeker.MotherId);
        Introduce(seeker.FatherId);
        if (known is not null)
            foreach (var friendId in known)
                if (world.Relationships.TryGetValue(new RelationshipKey(seekerId, friendId), out var tie) && tie.Affection >= bonds.IntroductionAffection)
                    Introduce(friendId);

        var neighbors = CommunityOf(world, seeker.City, bonds.CommunitySize);
        if (neighbors is not null) prospects.UnionWith(neighbors);
        prospects.Remove(seekerId);
        return prospects;
    }

    /// <summary>Vivos da cidade, ou <c>null</c> se ela passa do tamanho em que todos se conhecem. Memo do último pedido.</summary>
    private static List<NpcId>? CommunityOf(WorldState world, CityId city, int communitySize)
    {
        var members = new List<NpcId>();
        foreach (var npc in world.AliveNpcIndex.Alive)
        {
            if (npc.City != city) continue;
            if (members.Count >= communitySize) return null;
            members.Add(npc.Id);
        }
        return members;
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

    /// <summary>Livre para cortejar (AD-136): sem cônjuge, ou viúvo depois do luto do cenário; e, depois de uma separação,
    /// passado o tempo que a norma pede para unir-se de novo.</summary>
    private static bool IsSingle(Npc npc, WorldState world)
    {
        var rules = world.FamilyRules.Unions;
        var now = world.CurrentDate;
        if (npc.Union is { } union && union.CourtFromTick > now.TotalHours) return false;
        if (npc.Spouse is not { } spouseId)
            return true;

        var spouse = world.FindNpc(spouseId);
        if (spouse is { IsAlive: true }) return false;
        if (spouse?.DeathDate is not { } widowed) return true;
        int mourning = npc.Sex == Sex.Female ? rules.WidowMourningMonths : rules.WidowerMourningMonths;
        return now.TotalHours - widowed.TotalHours >= (long)mourning * now.Calendar.HoursPerMonth;
    }

    private static bool IsFertilityCompatible(Npc a, Npc b, PopulationRules rules, WorldDate now)
    {
        if (a.Sex == b.Sex)
            return false;

        // idade de casar por sexo (AD-130): a mulher na janela fértil; o homem sem teto
        return rules.IsMarriageableAge(a.Sex, a.AgeYears(now)) && rules.IsMarriageableAge(b.Sex, b.AgeYears(now));
    }

    private static double NormalizeFactor(
        AttractionFactor factor,
        Npc a,
        Npc b,
        Relationship? aToB,
        Relationship? bToA,
        PopulationRules populationRules,
        WorldDate now,
        bool bondsByFamiliarity)
    {
        return factor switch
        {
            AttractionFactor.Age => PairAgeFactor(a, b, populationRules, now),
            AttractionFactor.Health => (a.Health / HundredScale + b.Health / HundredScale) / 2.0,
            AttractionFactor.Status => (StatusFactor(a) + StatusFactor(b)) / 2.0,
            AttractionFactor.Skill => (SkillFactor(a) + SkillFactor(b)) / 2.0,
            AttractionFactor.CulturalAffinity => CulturalAffinityOf(a, b),
            AttractionFactor.ExistingRelationship => ExistingRelationshipFactor(aToB, bToA, bondsByFamiliarity),
            AttractionFactor.Appearance => Appearance.Similarity(a.Appearance!, b.Appearance!),
            _ => 0.0,
        };
    }

    /// <summary>Afinidade cultural pelo idioma que cada um fala (criação, Fase 21c), não pela etnia de nascimento.</summary>
    internal static double CulturalAffinityOf(Npc a, Npc b) =>
        LivingWorld.Simulation.Ontogeny.OntogenyMechanic.SpokenCulture(a) == LivingWorld.Simulation.Ontogeny.OntogenyMechanic.SpokenCulture(b) ? 1.0 : 0.0;

    private static double PairAgeFactor(Npc a, Npc b, PopulationRules rules, WorldDate now)
    {
        double fa = IndividualAgeFactor(a, rules, now);
        double fb = IndividualAgeFactor(b, rules, now);
        return (fa + fb) / 2.0;
    }

    /// <summary>Atração pela idade = fertilidade relativa da idade, por sexo (AD-130): a da mulher cai depois dos 30 e
    /// zera na menopausa; a do homem fica alta até os 40 e cai devagar na velhice.</summary>
    private static double IndividualAgeFactor(Npc npc, PopulationRules rules, WorldDate now)
    {
        int ageYears = npc.AgeYears(now);
        if (!rules.IsMarriageableAge(npc.Sex, ageYears))
            return 0.0;

        double exactAge = (now.TotalHours - npc.BirthDate.TotalHours) / (double)now.Calendar.HoursPerYear;
        return rules.Fertility.RelativeFertility(npc.Sex, exactAge);
    }

    /// <summary>Quão perto da idade que o avaliador procura o outro está (AD-136): o homem procura alguém uns anos mais
    /// nova, a mulher alguém uns anos mais velho, e o interesse cai em linha até a tolerância do cenário.</summary>
    private static double AgeFit(Npc evaluator, Npc target, UnionRules rules, WorldDate now)
    {
        double hoursPerYear = now.Calendar.HoursPerYear;
        double gap = (evaluator.BirthDate.TotalHours - target.BirthDate.TotalHours) / hoursPerYear; // > 0: o outro é mais velho
        double wanted = evaluator.Sex == Sex.Male ? -rules.PreferredAgeGapYears : rules.PreferredAgeGapYears;
        return Math.Clamp(1 - Math.Abs(gap - wanted) / rules.AgeToleranceYears, 0, 1);
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

    /// <summary>Com <see cref="FamilyRules.Bonds"/> (AD-128) confiança e afeto deixam de saturar em 100 para qualquer colega.
    /// Quem é da mesma comunidade ou foi apresentado vale o que o par saturado valia antes (0,5: conhecido de vista e de
    /// fama), e a amizade de verdade soma até o dobro. O cortejo começa pela atração; o vínculo se faz durante ele.</summary>
    private static double SideOf(Relationship? rel, bool bondsByFamiliarity)
    {
        if (rel is null)
            return bondsByFamiliarity ? 0.5 : 0.0;
        if (bondsByFamiliarity)
            return 0.5 + 0.5 * Math.Min(1.0, (rel.Trust + rel.Affection) / (2 * HundredScale));
        double sum = 0;
        foreach (var axis in RelationshipAxes)
            sum += rel.Get(axis) / HundredScale;
        return sum / RelationshipAxes.Length;
    }

    private static double ExistingRelationshipFactor(Relationship? aToB, Relationship? bToA, bool bondsByFamiliarity)
    {
        double SideAverage(Relationship? rel)
        {
            if (rel is null)
                return bondsByFamiliarity ? 0.5 : 0.0;
            if (bondsByFamiliarity)
                return 0.5 + 0.5 * Math.Min(1.0, (rel.Trust + rel.Affection) / (2 * HundredScale));

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
