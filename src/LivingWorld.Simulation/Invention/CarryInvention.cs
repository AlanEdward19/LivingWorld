using System.Globalization;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Invention;

/// <summary>O problema de carregar (AD-147), pelo mesmo caminho da travessia (AD-141/143): mensal, só olha quem já está com o
/// problema (compra cortada pelo que cabe nos braços, sede ou fome longe de casa) e quem já sabe fazer. Quem passa do limiar tenta:
/// copia o recipiente que viu em uso ou concebe variantes a partir do que precisava levar (um oco do tamanho do que faltou, de cada
/// material que conhece), julga com a própria crença pela sonda de conter e constrói a melhor; na conclusão o avaliador real decide.
/// Quem sabe ensina, faz o seu e faz para vender quando a cidade procura. Nenhum recipiente escrito: cesto, odre ou mochila é o nome
/// que alguém dá ao que o avaliador mostrou guardar e ser levado.</summary>
internal static class CarryInvention
{
    public const byte Function = 1;

    public static void Month(WorldState world, TickContext ctx, long now)
    {
        var ledger = world.Invention!;
        for (int i = ledger.Carried.Count - 1; i >= 0; i--) // quem saiu do mundo sem morrer (emigrou) leva o que tinha
            if (world.FindNpc(new NpcId(ledger.Carried[i].NpcId)) is not { IsAlive: true })
                ledger.Carry(ledger.Carried[i].NpcId, ledger.Carried[i].ResourceId, -ledger.Carried[i].Quantity);
        Minds(world, ctx, now);
        Knowers(world, ctx, now);
    }

    private static void Minds(WorldState world, TickContext ctx, long now)
    {
        var rules = world.InventionRules!;
        var ledger = world.Invention!;
        if (ledger.CarryMinds.Count == 0) return;
        foreach (var mind in ledger.CarryMinds.ToList())
        {
            if (world.FindNpc(new NpcId(mind.NpcId)) is not { IsAlive: true } npc)
            {
                PersonalCarry.Forget(world, mind.NpcId);
                continue;
            }

            int frustration = (int)((long)mind.FrustrationMinutes * rules.FrustrationKeepPm / 1000);
            var current = mind with { FrustrationMinutes = frustration };
            if (current != mind) ledger.PutCarryMind(current);
            if (frustration < 10 && mind.BusyUntilTick <= now && mind.Attempts == 0)
            {
                ledger.RemoveCarryMind(ledger.CarryMindIndex(mind.NpcId));
                continue;
            }
            if (mind.BusyUntilTick > now || frustration < rules.FrustrationThresholdMinutes || ledger.OrderIndex(mind.NpcId) >= 0) continue;
            if (npc.AgeYears(world.CurrentDate) < rules.MinInventorAgeYears || npc.Household is not { } home) continue;
            if (ledger.BestCarrier(home.Value, rules).Exists) continue;

            var rng = ctx.OneShot($"invent-carry-{npc.Id.Value}-{now}");
            double skill = InventionSystem.Skill(npc, rules);
            double chance = rules.BaseAttemptChancePm / 1000.0
                * Math.Min(3.0, frustration / (double)rules.FrustrationThresholdMinutes)
                * (0.5 + npc.Personality.Openness / 100.0)
                * InventionSystem.FreeHours(npc, rules) / 4.0
                * (0.5 + skill / 100.0);
            if (rng.NextDouble() >= chance) continue;

            if (FirstKnown(world, npc.Id.Value) is { } known)
                InventionSystem.StartBuild(world, npc, known, current, now, onCarryMind: true);
            else if (current.SeenDesignId > 0 && ledger.FindDesign(current.SeenDesignId) is { } seen
                     && rng.NextDouble() < rules.CopyPreferencePm / 1000.0)
                StartCopy(world, ctx, npc, seen, current, skill, rng, now);
            else
                StartInvention(world, ctx, npc, current, skill, rng, now);
        }
    }

    private static KnownDesign? FirstKnown(WorldState world, long npcId)
    {
        var ledger = world.Invention!;
        foreach (var k in ledger.KnownBy(npcId))
            if (ledger.FunctionOf(k.DesignId) == Function) return k;
        return null;
    }

    /// <summary>A ideia nasce do que faltou levar: um oco do tamanho disso, de cada material que a pessoa conhece (parede mínima
    /// que o material dá), mais o protótipo que falhou e os recipientes que ela já sabe; os mesmos operadores genéricos da busca.</summary>
    private static void StartInvention(WorldState world, TickContext ctx, Npc npc, InventionMind mind, double skill, WorldRng rng, long now)
    {
        var rules = world.InventionRules!;
        var ledger = world.Invention!;
        bool nearWater = InventionSystem.NearWater(world, npc);
        var usable = rules.Materials.Where(m => (nearWater || !m.NeedsWaterNearby) && m.Id != mind.DistrustedMaterial).ToList();
        var known = ledger.KnownBy(npc.Id.Value).Where(k => ledger.FunctionOf(k.DesignId) == Function)
            .Select(k => ledger.FindDesign(k.DesignId)?.Parts).OfType<IReadOnlyList<DesignPart>>().ToList();
        var seeds = new List<IReadOnlyList<DesignPart>>();
        if (mind.LastTried is { } tried && Design.Decode(tried) is { } failed) seeds.Add(failed);
        seeds.AddRange(known);
        int needMl = Math.Max(rules.SmallestUnitMl(), mind.NeedMl);
        foreach (var m in usable)
            if (!m.CoatingOnly && NeedSized(m, needMl) is { } piece) seeds.Add([piece]);
        if (seeds.Count == 0) { ledger.PutCarryMind(mind with { Attempts = mind.Attempts + 1 }); return; }

        var belief = InventionSystem.Belief(rules, usable, skill, rng);
        List<DesignPart>? best = null;
        long bestScore = long.MinValue;
        for (int v = 0; v < rules.VariantsPerAttempt; v++)
        {
            var seed = seeds[v == 0 && mind.LastTried is not null ? 0 : (int)(rng.NextDouble() * seeds.Count)];
            var variant = DesignSearch.Variant(seed, known, usable, mind.LastFailure, rng);
            long score = Score(PhysicsEvaluator.Contain(variant, rules, belief), mind, rules);
            if (score > bestScore) { bestScore = score; best = variant; }
        }

        var next = mind with { Attempts = mind.Attempts + 1 };
        if (best is null || bestScore < InventionSystem.WorksScore)
        {
            world.InventionTally.CarryIdeasDiscarded++;
            ledger.PutCarryMind(next);
            return;
        }
        Schedule(world, ctx, npc, next, best, DesignOrigin.Invented, 0, now);
    }

    /// <summary>Um oco aberto em cima, cúbico, com o volume que faltou e a parede mínima do material; nulo se a natureza não dá.</summary>
    internal static DesignPart? NeedSized(MaterialSpec m, int needMl)
    {
        int wall = Math.Max(1, m.MinWallMm);
        int side = (int)PhysicsEvaluator.ICbrt((Int128)needMl * 1000) + 2 * wall;
        side = (side + 9) / 10 * 10;
        if (side > short.MaxValue) return null;
        var p = new DesignPart(PartShape.Box, (short)side, (short)side, (short)(side - wall), (short)wall, (short)m.Id, 0,
            JointKind.Fixed, -1, 0, 0, 0);
        return PhysicsEvaluator.IsBuildable([p], [m]) ? p : null;
    }

    private static void StartCopy(WorldState world, TickContext ctx, Npc npc, Design design, InventionMind mind, double skill, WorldRng rng, long now)
    {
        var rules = world.InventionRules!;
        var ledger = world.Invention!;
        world.InventionTally.CarryCopies++;
        bool anyoneKnows = ledger.Known.Any(k => k.DesignId == design.Id && world.FindNpc(new NpcId(k.NpcId)) is { IsAlive: true });
        int fidelity = (int)(rules.CopyFidelityPm * (0.6 + 0.4 * skill / 100.0));
        var parts = InventionSystem.Mutated(design.Parts, fidelity, rules, rng);
        Schedule(world, ctx, npc, mind with { Attempts = mind.Attempts + 1, SeenDesignId = 0 }, parts,
            anyoneKnows ? DesignOrigin.Copied : DesignOrigin.ReverseEngineered, design.Id, now);
    }

    private static void Schedule(WorldState world, TickContext ctx, Npc npc, InventionMind mind, IReadOnlyList<DesignPart> parts,
        DesignOrigin origin, int parent, long now)
    {
        var perf = PhysicsEvaluator.Contain(parts, world.InventionRules!);
        long hours = Math.Max(1, perf.BuildMinutes / 60);
        long until = now + InventionSystem.DurationTicks(world, npc, hours);
        world.Invention!.PutCarryMind(mind with { BusyUntilTick = until });
        world.InventionTally.CarryAttempts++;
        ctx.ScheduleEvent(until, InventionSystem.SystemName, $"C|{npc.Id.Value}|{(int)origin}|{parent}|0|0|{Design.Encode(parts)}");
    }

    /// <summary>Conclusão do protótipo: a peça como saiu das mãos passa pela sonda real de conter.</summary>
    internal static void CompletePrototype(WorldState world, TickContext ctx, Npc npc, IReadOnlyList<DesignPart> intended, DesignOrigin origin, int parent)
    {
        var rules = world.InventionRules!;
        var ledger = world.Invention!;
        long now = ctx.CurrentTick;
        double skill = InventionSystem.Skill(npc, rules);
        var rng = ctx.OneShot($"prototype-carry-{npc.Id.Value}-{now}");
        var built = DesignSearch.AsBuilt(intended, rules.Materials, (int)(rules.ManufacturingNoisePm * (100 - skill) / 100), rng);
        var perf = PhysicsEvaluator.Contain(built, rules);
        int index = ledger.CarryMindIndex(npc.Id.Value);
        var mind = index >= 0 ? ledger.CarryMinds[index] : new InventionMind(npc.Id.Value, 0, 0, 0, 0, FailureMode.None, null, 0);
        if (!perf.Works)
        {
            Fail(world, ctx, npc, mind, perf.Failure, built);
            return;
        }

        var design = Design.Create(built, npc.Id.Value, now, parent, 0, origin);
        bool isNew = ledger.FindDesign(design.Id) is null;
        ledger.AddDesign(design);
        ledger.MarkCarryDesign(design.Id);
        ledger.Learn(new KnownDesign(npc.Id.Value, design.Id, 1000, origin, now));
        InventionSystem.AddItem(world, npc, design, skill, now);
        ledger.PutCarryMind(mind with { FrustrationMinutes = 0, LastTried = null, LastFailure = FailureMode.None });
        if (isNew && origin == DesignOrigin.Invented) world.InventionTally.CarryInventions++;
        ctx.LogEvent(WorldEventKind.DesignPrototyped,
            string.Create(CultureInfo.InvariantCulture, $"{npc.Id.Value}|{design.Id}|{(int)origin}|{parent}|0"), InventionSystem.SystemName);
    }

    /// <summary>Construção de quem sabe que falhou: desanima e só volta a tentar daqui a uns meses.</summary>
    internal static void BuildFailed(WorldState world, TickContext ctx, Npc npc, FailureMode mode, long now)
    {
        var ledger = world.Invention!;
        int index = ledger.CarryMindIndex(npc.Id.Value);
        var mind = index >= 0 ? ledger.CarryMinds[index] : new InventionMind(npc.Id.Value, 0, 0, 0, 0, FailureMode.None, null, 0);
        ledger.PutCarryMind(mind with
        {
            Failures = mind.Failures + 1,
            LastFailure = mode,
            BusyUntilTick = now + world.InventionRules!.RetryAfterFailureMonths * world.Calendar.HoursPerMonth,
        });
        ctx.LogEvent(WorldEventKind.PrototypeFailed,
            string.Create(CultureInfo.InvariantCulture, $"{npc.Id.Value}|{(int)mode}|1"), InventionSystem.SystemName);
    }

    private static void Fail(WorldState world, TickContext ctx, Npc npc, InventionMind mind, FailureMode mode, IReadOnlyList<DesignPart> tried)
    {
        world.InventionTally.CarryPrototypeFailures++;
        int distrust = mind.DistrustedMaterial;
        if (tried.Count > 0 && mode == FailureMode.Breaks) distrust = tried[0].Material;
        world.Invention!.PutCarryMind(mind with
        {
            Failures = mind.Failures + 1,
            LastFailure = mode,
            DistrustedMaterial = distrust,
            LastTried = Design.Encode(tried),
        });
        ctx.LogEvent(WorldEventKind.PrototypeFailed,
            string.Create(CultureInfo.InvariantCulture, $"{npc.Id.Value}|{(int)mode}|0"), InventionSystem.SystemName);
    }

    /// <summary>Quanto o que se imagina resolve o que faltou: carga útil até a necessidade (água só conta no estanque), mãos livres
    /// se se prende ao corpo, menos o peso próprio e o esforço de fazer. Quem nunca fez tenta primeiro o mais simples que serve.</summary>
    internal static long Score(Containment c, InventionMind mind, InventionRules rules)
    {
        long needG = Math.Max(rules.SmallestUnitMl(), mind.NeedMl) * (long)rules.WaterDensityKgM3 / 1000;
        return c.Failure switch
        {
            FailureMode.None => InventionSystem.WorksScore
                + Math.Min(c.CapacityG, needG) * 10
                + (mind.NeedsLiquid ? Math.Min(c.WatertightMl, needG) * 10 : 0)
                + (c.Worn ? rules.HandsVolumeMl * 10L : 0)
                - c.MassG * 5 - c.BuildMinutes * 20L,
            FailureMode.Breaks => 300_000 + c.CapacityG,
            FailureMode.TooHeavy => 250_000 - Math.Min(200_000, c.MassG / 10),
            FailureMode.TooSmall => 100_000 + c.InnerMl,
            _ => 0,
        };
    }

    /// <summary>Quem sabe fazer um recipiente: ensina (alguém da casa ou um amigo próximo), faz o seu se a casa precisa e não tem, ou
    /// faz para vender quando a cidade procurou no mês passado mais do que há exposto. Uma vez por pessoa por mês.</summary>
    private static void Knowers(WorldState world, TickContext ctx, long now)
    {
        var rules = world.InventionRules!;
        var ledger = world.Invention!;
        if (ledger.Known.Count == 0) return;
        var open = new Dictionary<CityId, int>();
        foreach (var d in ledger.Demand) if (d.Function == Function) open[d.City] = d.Households;
        foreach (var item in ledger.Items)
            if (item.ForSale && open.ContainsKey(item.City) && ledger.FunctionOf(item.DesignId) == Function) open[item.City]--;
        foreach (var order in ledger.Orders)
            if (order.MarketId is not null && ledger.FunctionOf(order.DesignId) == Function
                && world.FindNpc(new NpcId(order.NpcId)) is { } maker && open.ContainsKey(maker.City)) open[maker.City]--;

        var lessons = new List<(long Learner, long Teacher, KnownDesign From)>();
        var builders = new List<(Npc Npc, KnownDesign Known, long? Market)>();
        long last = -1;
        foreach (var known in ledger.Known.ToList())
        {
            if (known.NpcId == last || ledger.FunctionOf(known.DesignId) != Function) continue;
            last = known.NpcId;
            if (world.FindNpc(new NpcId(known.NpcId)) is not { IsAlive: true } teacher || teacher.Household is not { } home) continue;
            var rng = ctx.OneShot($"teach-carry-{teacher.Id.Value}-{now}");
            if (rng.NextDouble() < rules.TeachChancePm / 1000.0 && InventionSystem.PickLearner(world, teacher, known.DesignId, rng) is { } learner)
                lessons.Add((learner.Id.Value, teacher.Id.Value, known));
            int mindIndex = ledger.CarryMindIndex(teacher.Id.Value);
            int boatMind = ledger.MindIndex(teacher.Id.Value);
            bool busy = (mindIndex >= 0 && ledger.CarryMinds[mindIndex].BusyUntilTick > now)
                || (boatMind >= 0 && ledger.Minds[boatMind].BusyUntilTick > now) || ledger.OrderIndex(teacher.Id.Value) >= 0;
            bool needs = mindIndex >= 0 && ledger.CarryMinds[mindIndex].FrustrationMinutes > 0;
            if (busy || teacher.AgeYears(world.CurrentDate) < rules.MinInventorAgeYears) continue;
            if (needs && !ledger.BestCarrier(home.Value, rules).Exists && rng.NextDouble() < rules.BuildChancePm / 1000.0)
                builders.Add((teacher, known, null));
            else if (open.GetValueOrDefault(teacher.City) > 0 && rng.NextDouble() < rules.BuildChancePm / 1000.0
                     && InventionMarket.MarketFor(world, teacher) is { } market)
            {
                builders.Add((teacher, known, market.Id.Value));
                open[teacher.City]--;
            }
        }

        foreach (var (learnerId, teacherId, from) in lessons)
        {
            if (world.FindNpc(new NpcId(learnerId)) is not { } learner || ledger.FindDesign(from.DesignId) is not { } design) continue;
            int fidelity = (int)Math.Clamp((long)from.FidelityPm * rules.TeachFidelityPm / 1000 * (700 + 3 * (int)InventionSystem.Skill(learner, rules)) / 1000, 100, 1000);
            ledger.Learn(new KnownDesign(learnerId, design.Id, (short)fidelity, DesignOrigin.Taught, now));
            world.InventionTally.CarryTaught++;
            ctx.LogEvent(WorldEventKind.DesignLearned,
                string.Create(CultureInfo.InvariantCulture, $"{learnerId}|{teacherId}|{design.Id}|{(int)DesignOrigin.Taught}|{fidelity}"), InventionSystem.SystemName);
        }

        foreach (var (npc, known, market) in builders)
        {
            int index = ledger.CarryMindIndex(npc.Id.Value);
            var mind = index >= 0 ? ledger.CarryMinds[index] : new InventionMind(npc.Id.Value, 0, 0, 0, 0, FailureMode.None, null, 0);
            InventionSystem.StartBuild(world, npc, known, mind, now, market, onCarryMind: true);
            world.InventionTally.CarryBuilt++;
        }
    }
}
