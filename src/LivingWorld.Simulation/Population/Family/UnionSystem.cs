using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Family;

/// <summary>A vida do casal, uma vez por mês (AD-136). A mágoa de cada um sobe com o choque de temperamento, a penúria e a
/// distância (e, fora daqui, com a morte de um filho e a traição) e é perdoada aos poucos; ela baixa o teto do afeto e da
/// confiança que a convivência refaz (<see cref="RelationshipSystem"/>), de modo que o amor esfria de verdade. Quem tem
/// propensão (impulsivo, pouco leal, pouco amor em casa) e oportunidade (alguém de quem gosta e que gosta dele) pode ter um
/// caso; o caso pode gerar filho do amante (<c>NatalitySystem</c>) e ser descoberto, pelo cônjuge ou pelo boato que corre
/// pelos vínculos dele. Quem deixou de amar pode sair (<see cref="UnionDissolution"/>), no ritmo da norma do cenário.</summary>
[LivingCapability("FAMILY", Events = [WorldEventKind.UnionEnded, WorldEventKind.AffairStarted, WorldEventKind.AffairDiscovered],
    Consumers = ["inspector.npc.household", "timeline.family"])]
[RunsAfter(typeof(CourtshipSystem))]
public sealed class UnionSystem : ISimulationSystem
{
    public const string SystemName = "population-union";

    private const double Scale = 100.0;
    private readonly List<(Npc, Npc)> _couples = [];

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Monthly;

    public void Tick(WorldState world, TickContext ctx)
    {
        var rules = world.FamilyRules.Unions;
        if (!rules.Enabled) return;

        _couples.Clear();
        foreach (var npc in world.AliveNpcIndex.Alive)
            if (npc.Spouse is { } spouseId && npc.Id.Value < spouseId.Value
                && world.FindNpc(spouseId) is { IsAlive: true } spouse && spouse.Spouse == npc.Id)
                _couples.Add((npc, spouse));

        long month = ctx.CurrentTick / world.Calendar.HoursPerMonth;
        foreach (var (a, b) in _couples)
        {
            if (!a.IsAlive || !b.IsAlive || a.Spouse != b.Id || b.Spouse != a.Id) continue;
            Review(world, ctx, rules, a, b, month);
        }
    }

    private static void Review(WorldState world, TickContext ctx, UnionRules rules, Npc a, Npc b, long month)
    {
        Ensure(world, a);
        Ensure(world, b);
        bool poor = IsPoor(world, a);
        Strain(world, rules, a, b, poor);
        Strain(world, rules, b, a, poor);
        Cool(world, a, b);
        Cool(world, b, a);

        foreach (var (self, spouse) in new[] { (a, b), (b, a) })
            if (ReviewAffair(world, ctx, rules, self, spouse, month)) return;
        foreach (var (self, spouse) in new[] { (a, b), (b, a) })
            if (TryLeave(world, ctx, rules, self, spouse, month)) return;
    }

    /// <summary>Teto do afeto e da confiança pelo cônjuge com a mágoa <paramref name="grievance"/> (0..100).</summary>
    public static (double Affection, double Trust) MaritalCeilings(double affection, double trust, double grievance) =>
        (affection * (1 - grievance / Scale), trust * (1 - grievance / (2 * Scale)));

    /// <summary>Casal formado antes do AD-136 (ou da população inicial) ganha o estado conjugal na primeira revisão.</summary>
    private static void Ensure(WorldState world, Npc npc)
    {
        if (npc.Union is null) npc.SetUnion(new UnionState(SinceTick: world.CurrentDate.TotalHours, Unions: 1));
    }

    private static void AddGrievance(Npc npc, double amount)
    {
        if (npc.Union is not { } union || amount == 0) return;
        npc.SetUnion(union with { Grievance = Math.Clamp(union.Grievance + amount, 0, Scale) });
    }

    /// <summary>Um mês de convivência: briga pelo choque de temperamento, penúria, distância e o perdão do mês.</summary>
    private static void Strain(WorldState world, UnionRules rules, Npc self, Npc spouse, bool poor)
    {
        var me = self.Personality;
        double conflict = (1 - SocialBondRules.Likability(me, spouse.Personality))
            * Math.Clamp(0.5 + (50.0 - me.EmotionalStability) / Scale + (50.0 - spouse.Personality.Agreeableness) / Scale, 0, 1.5);
        double g = self.Union!.Grievance + rules.QuarrelPerMonth * conflict;
        if (poor) g += rules.PovertyPerMonth * (1.5 - me.EmotionalStability / Scale);
        if (self.Household != spouse.Household) g += rules.AbsencePerMonth;
        g -= g * rules.ForgivenessPerMonth * (0.5 + me.Agreeableness / Scale);
        double clamped = Math.Clamp(g, 0, Scale);
        if (clamped != self.Union.Grievance) self.SetUnion(self.Union with { Grievance = clamped });
    }

    private static bool IsPoor(WorldState world, Npc member)
    {
        if (member.Household is not { } id || world.FindHousehold(id) is not { } home) return true;
        long floor = world.FamilyRules.ConceptionResourceFloor.GetValueOrDefault(world.EconomyRules.FoodResourceId);
        if (home.Stock.GetValueOrDefault(new ResourceType(world.EconomyRules.FoodResourceId)) < floor) return true;
        foreach (var memberId in home.Members)
            if (world.FindNpc(memberId) is { IsAlive: true, HungerZeroSinceTick: not null }) return true;
        return false;
    }

    /// <summary>O afeto e a confiança pelo cônjuge descem ao que a relação hoje sustenta.</summary>
    private static void Cool(WorldState world, Npc self, Npc spouse)
    {
        if (self.Union is not { Grievance: > 0 } union) return;
        if (!world.Relationships.TryGetValue(new RelationshipKey(self.Id, spouse.Id), out var rel)) return;
        var bonds = world.FamilyRules.Bonds;
        double affection = bonds?.AffectionCeiling(self.Personality, spouse.Personality, sameHousehold: true) ?? Scale;
        double trust = bonds?.TrustCeiling(spouse.Personality) ?? Scale;
        var (a, t) = MaritalCeilings(affection, trust, union.Grievance);
        rel.CoolTo(a, t);
    }

    private static double Love(WorldState world, Npc self, Npc spouse) =>
        world.Relationships.TryGetValue(new RelationshipKey(self.Id, spouse.Id), out var rel) ? rel.Affection : 0;

    /// <summary>Propensão a trair, em torno de 1 no temperamento médio: impulsividade e pouca lealdade.</summary>
    private static double Propensity(Personality p) => (p.Impulsivity / 50.0) * ((Scale - p.Loyalty) / 50.0);

    /// <summary>Caso em curso: descoberta ou fim; sem caso: talvez comece um. Devolve verdadeiro se a união acabou.</summary>
    private static bool ReviewAffair(WorldState world, TickContext ctx, UnionRules rules, Npc self, Npc spouse, long month)
    {
        var rng = ctx.StreamFor("union-affair", unchecked(self.Id.Value * 1_000_003L + month));
        if (self.Union!.AffairWith is { } loverId)
        {
            var lover = world.FindNpc(new NpcId(loverId));
            if (lover is not { IsAlive: true } || lover.Union?.AffairWith != self.Id.Value)
            {
                self.SetUnion(self.Union with { AffairWith = null });
                return false;
            }

            double discovery = rules.DiscoveryPerMonth + rules.GossipPerConfidant * Confidants(world, rules, spouse, self, lover);
            if (rng.NextDouble() < discovery) return Discover(world, ctx, rules, spouse, self, lover, rng);
            if (rng.NextDouble() < rules.AffairEndPerMonth) EndAffair(self, lover);
            return false;
        }

        double lovelessness = Math.Clamp(2 * (1 - Love(world, self, spouse) / Scale), 0, 2);
        double chance = rules.AffairPerYear / world.Calendar.MonthsPerYear * Propensity(self.Personality) * lovelessness;
        if (rng.NextDouble() >= chance) return false;
        if (FindLover(world, rules, self, spouse, rng) is not { } partner) return false;

        self.SetUnion(self.Union with { AffairWith = partner.Id.Value, AffairSinceTick = ctx.CurrentTick });
        partner.SetUnion((partner.Union ?? new UnionState()) with { AffairWith = self.Id.Value, AffairSinceTick = ctx.CurrentTick });
        ctx.LogEvent(WorldEventKind.AffairStarted, $"{self.Id.Value}|{partner.Id.Value}", sourceSystem: SystemName);
        return false;
    }

    /// <summary>Oportunidade: entre quem a pessoa conhece, do outro sexo, na idade de casar, sem tabu de parentesco, sem
    /// outro caso, de quem ela gosta e que gosta dela, o mais querido; e ele precisa aceitar (o casado pela própria
    /// propensão, o solteiro pelo quanto é impulsivo e pouco leal).</summary>
    private static Npc? FindLover(WorldState world, UnionRules rules, Npc self, Npc spouse, WorldRng rng)
    {
        var targets = world.RelationshipTargetsOf(self.Id);
        var relations = world.RelationshipsFrom(self.Id);
        if (relations is null) return null;
        var now = world.CurrentDate;
        Npc? best = null;
        double bestAffection = rules.AffairAffectionMin;
        for (int i = 0; i < targets.Count && i < relations.Count; i++)
        {
            if (relations[i].Affection < bestAffection) continue;
            if (world.FindNpc(targets[i]) is not { IsAlive: true } other || other.Id == spouse.Id || other.Sex == self.Sex) continue;
            if (InAffair(world, other) || other.City != self.City) continue;
            if (!world.PopulationRules.IsMarriageableAge(other.Sex, other.AgeYears(now))) continue;
            if (!world.Relationships.TryGetValue(new RelationshipKey(other.Id, self.Id), out var back) || back.Affection < rules.AffairAffectionMin) continue;
            if (Kinship.IsTaboo(world, self, other)) continue;
            (best, bestAffection) = (other, relations[i].Affection);
        }
        if (best is null) return null;

        var p = best.Personality;
        double accept = best.Spouse is { } theirs && world.FindNpc(theirs) is { IsAlive: true }
            ? Math.Clamp(0.5 * Propensity(p), 0, 1)
            : Math.Clamp(0.5 + (p.Impulsivity - p.Loyalty) / (2 * Scale), 0, 1);
        return rng.NextDouble() < accept ? best : null;
    }

    /// <summary>Confidentes do cônjuge que conhecem alguém do caso: por eles o boato chega (até o teto do cenário).</summary>
    private static int Confidants(WorldState world, UnionRules rules, Npc spouse, Npc unfaithful, Npc lover)
    {
        var targets = world.RelationshipTargetsOf(spouse.Id);
        var relations = world.RelationshipsFrom(spouse.Id);
        if (relations is null) return 0;
        int count = 0;
        for (int i = 0; i < targets.Count && i < relations.Count && count < rules.MaxConfidants; i++)
        {
            var id = targets[i];
            if (id == unfaithful.Id || id == lover.Id || relations[i].Affection < rules.ConfidantAffection) continue;
            if (world.Relationships.ContainsKey(new RelationshipKey(id, unfaithful.Id))
                || world.Relationships.ContainsKey(new RelationshipKey(id, lover.Id)))
                count++;
        }
        return count;
    }

    /// <summary>Caso em curso de verdade: o amante vivo e com o caso do lado dele também.</summary>
    private static bool InAffair(WorldState world, Npc npc) =>
        npc.Union?.AffairWith is { } id && world.FindNpc(new NpcId(id)) is { IsAlive: true } lover
        && lover.Union?.AffairWith == npc.Id.Value;

    private static void EndAffair(Npc a, Npc b)
    {
        if (a.Union is { } ua) a.SetUnion(ua with { AffairWith = null });
        if (b.Union is { } ub && ub.AffairWith == a.Id.Value) b.SetUnion(ub with { AffairWith = null });
    }

    /// <summary>O traído soube: traição no vínculo com o infiel e com o amante, mágoa grande, fim do caso, e então violência,
    /// separação ou perdão conforme o temperamento dos dois e a norma do lugar.</summary>
    private static bool Discover(WorldState world, TickContext ctx, UnionRules rules, Npc betrayed, Npc unfaithful, Npc lover, WorldRng rng)
    {
        long now = ctx.CurrentTick;
        foreach (var target in new[] { unfaithful, lover })
        {
            var rel = world.GetOrCreateRelationship(new RelationshipKey(betrayed.Id, target.Id), now);
            rel.ApplyEvent(RelationshipEventType.Betrayal, world.FamilyRules);
            rel.MarkContact(now);
        }
        Ensure(world, betrayed);
        AddGrievance(betrayed, rules.BetrayalGrievance);
        EndAffair(unfaithful, lover);
        Cool(world, betrayed, unfaithful);

        var b = betrayed.Personality;
        double temper = (b.Impulsivity / 50.0) * ((Scale - b.Agreeableness) / 50.0) * (0.5 + (Scale - b.EmotionalStability) / Scale);
        bool violence = rng.NextDouble() < Math.Clamp(rules.ViolenceChance * temper, 0, 1);
        if (violence) unfaithful.SetHealth(Math.Max(1, unfaithful.Health - rules.ViolenceHarm));

        var u = unfaithful.Personality;
        double leave = rules.AdulterySeparation * (1.5 - b.Agreeableness / Scale) * (1.5 - b.Loyalty / Scale)
            * (1.5 - u.Agreeableness / Scale);
        bool separate = rng.NextDouble() < Math.Clamp(leave, 0, 1);
        string outcome = (violence, separate) switch
        {
            (true, true) => "violencia_separacao",
            (true, false) => "violencia",
            (false, true) => "separacao",
            _ => "perdao",
        };
        ctx.LogEvent(WorldEventKind.AffairDiscovered,
            $"{betrayed.Id.Value}|{unfaithful.Id.Value}|{lover.Id.Value}|{outcome}", sourceSystem: SystemName);
        if (!separate) return false;

        // quem traiu é posto para fora (ou foge, se a norma sorteia abandono)
        UnionDissolution.Separate(world, ctx, unfaithful, betrayed, rules.ModeFor(rng.NextDouble()));
        return true;
    }

    /// <summary>Quem deixou de amar pode sair, na chance da norma, pesada pelo quanto o amor se foi, pelo temperamento
    /// (impulsivo, pouco leal, que arrisca) e pelas amarras (filho pequeno, não ter como se manter).</summary>
    private static bool TryLeave(WorldState world, TickContext ctx, UnionRules rules, Npc self, Npc spouse, long month)
    {
        double love = Love(world, self, spouse);
        if (love >= rules.LoveLostBelow) return false;

        var p = self.Personality;
        double discontent = (rules.LoveLostBelow - love) / rules.LoveLostBelow;
        double temper = (0.5 + p.Impulsivity / Scale) * (1.5 - p.Loyalty / Scale) * (1.5 - p.RiskAversion / Scale);
        double barrier = HasYoungChild(world, rules, self, spouse) ? rules.YoungChildBarrier : 1.0;
        if (self.Employer is null && self.Wallet.Amount <= 0) barrier *= rules.DependencyBarrier;
        double chance = rules.SeparationPerYear / world.Calendar.MonthsPerYear * discontent * temper * barrier;
        var rng = ctx.StreamFor("union-leave", unchecked(self.Id.Value * 1_000_003L + month));
        if (rng.NextDouble() >= chance) return false;

        var mode = rules.ModeFor(rng.NextDouble());
        // quem tem a terra ou o negócio fica com a casa; no abandono, quem decide é quem foge
        bool selfOwns = OwnsWorkplace(world, self), spouseOwns = OwnsWorkplace(world, spouse);
        var (leaver, stayer) = mode != UnionEndMode.Abandonment && selfOwns && !spouseOwns ? (spouse, self) : (self, spouse);
        UnionDissolution.Separate(world, ctx, leaver, stayer, mode);
        return true;
    }

    private static bool OwnsWorkplace(WorldState world, Npc npc) => world.Workplaces.Any(w => w.Owner == npc.Id);

    private static bool HasYoungChild(WorldState world, UnionRules rules, Npc a, Npc b)
    {
        if (a.Household is not { } id || world.FindHousehold(id) is not { } home) return false;
        foreach (var memberId in home.Members)
            if (world.FindNpc(memberId) is { IsAlive: true } kid && kid.AgeYears(world.CurrentDate) < rules.TenderYearsAge
                && (kid.MotherId == a.Id || kid.FatherId == a.Id || kid.MotherId == b.Id || kid.FatherId == b.Id))
                return true;
        return false;
    }

    /// <summary>Luto que afasta (chamado no espólio de quem morreu): a morte de um filho criança fere os dois pais casados.</summary>
    public static void OnDeath(WorldState world, Npc dead)
    {
        var rules = world.FamilyRules.Unions;
        if (!rules.Enabled || world.LifeStageRules.LifeStageOf(dead.AgeYears(world.CurrentDate)) != LifeStage.Child) return;
        if (dead.MotherId is not { } m || dead.FatherId is not { } f) return;
        if (world.FindNpc(m) is not { IsAlive: true } mother || world.FindNpc(f) is not { IsAlive: true } father) return;
        if (mother.Spouse != father.Id || father.Spouse != mother.Id) return;
        Ensure(world, mother);
        Ensure(world, father);
        AddGrievance(mother, rules.ChildDeathGrievance);
        AddGrievance(father, rules.ChildDeathGrievance);
    }

    /// <summary>O pai biológico de uma concepção (AD-136): o marido, ou, se a mulher tem caso em curso com um homem vivo,
    /// o amante na fração <see cref="UnionRules.AffairPaternityShare"/>. Sorteio próprio, só aberto quando há caso.</summary>
    public static Npc BiologicalFather(WorldState world, TickContext ctx, Npc mother, Npc husband)
    {
        var rules = world.FamilyRules.Unions;
        if (!rules.Enabled || mother.Union?.AffairWith is not { } loverId) return husband;
        if (world.FindNpc(new NpcId(loverId)) is not { IsAlive: true, Sex: Sex.Male } lover
            || lover.Union?.AffairWith != mother.Id.Value) return husband;
        var rng = ctx.StreamFor("paternity", unchecked(mother.Id.Value * 1_000_003L + ctx.CurrentTick));
        return rng.NextDouble() < rules.AffairPaternityShare ? lover : husband;
    }
}
