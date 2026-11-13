using System.Globalization;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Invention;

/// <summary>Invenção por composição física (ADR-0045, AD-141), <c>Monthly</c>. Só olha quem já está com o problema (as mentes
/// frustradas pela água no caminho) e quem já sabe algo: nada de passo por NPC por tick. Quem passou do limiar tenta, com chance
/// de abertura, tempo livre e habilidade: copia um item visto na cidade ou concebe 8 variantes a partir do que conhece, julga com
/// a própria crença (propriedades com erro pela inabilidade) e constrói a melhor; a conclusão é agendada pelas horas que o design
/// pede ao tempo livre da pessoa. Na conclusão, o avaliador real decide sobre a peça como saiu das mãos dela. Quem sabe ensina a
/// casa e os amigos (com perda) e constrói a sua quando não tem. Itens gastam com o tempo.
/// AD-143: quem sabe também faz para vender quando a cidade procura mais do que há exposto (a encomenda anda na
/// <c>ProductionSystem</c>), o preço das peças expostas anda com oferta e procura, a cópia nasce de ter visto a peça em uso e a casa
/// que tem a peça pesca.</summary>
[LivingCapability("INVENTION", Events = [WorldEventKind.DesignPrototyped, WorldEventKind.PrototypeFailed, WorldEventKind.DesignLearned,
        WorldEventKind.DesignSold],
    Consumers = ["behavior.travel", "economy.purchase", "economy.production", "cognition.memory", "needs.food"])]
public sealed class InventionSystem : ISimulationSystem
{
    public const string SystemName = "invention";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Monthly;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.InventionOn) return;
        long now = ctx.CurrentTick;
        ExpireItems(world, now);
        InventionMarket.CloseMonth(world);
        Minds(world, ctx, now);
        Knowers(world, ctx, now);
        Fishing.Month(world, ctx);
    }

    public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt)
    {
        if (!world.InventionOn || evt.Payload is not { } payload) return;
        var f = payload.Split('|');
        if (f.Length < 2 || world.FindNpc(new NpcId(Long(f[1]))) is not { IsAlive: true } npc) return;
        if (f[0] == "P" && f.Length == 7 && Design.Decode(f[6]) is { } intended)
            CompletePrototype(world, ctx, npc, intended, (DesignOrigin)Int(f[2]), Int(f[3]), Int(f[4]));
        else if (f[0] == "B" && f.Length == 4 && world.Invention!.FindDesign(Int(f[2])) is { } design)
            CompleteBuild(world, ctx, npc, design, Int(f[3]));
    }

    private static void ExpireItems(WorldState world, long now)
    {
        var ledger = world.Invention!;
        var gone = ledger.Items.Where(i => i.ExpiresTick <= now).Select(i => i.DesignId).ToList();
        if (gone.Count == 0) return;
        ledger.Items.RemoveAll(i => i.ExpiresTick <= now);
        ledger.Touch();
        foreach (int design in gone.Distinct().Order()) InventionIntake.BuryIfLost(world, design, now);
    }

    private static void Minds(WorldState world, TickContext ctx, long now)
    {
        var rules = world.InventionRules!;
        var ledger = world.Invention!;
        var rumors = new HashSet<CityId>();
        foreach (var t in ledger.Tombstones) if (t.Accounts > 0) rumors.Add(t.City);
        foreach (var mind in ledger.Minds.ToList())
        {
            if (world.FindNpc(new NpcId(mind.NpcId)) is not { IsAlive: true } npc)
            {
                InventionIntake.Forget(world, mind.NpcId, now);
                continue;
            }

            int frustration = (int)((long)mind.FrustrationMinutes * rules.FrustrationKeepPm / 1000);
            var current = mind with { FrustrationMinutes = frustration };
            if (current != mind) ledger.PutMind(current);
            if (frustration < 10 && mind.BusyUntilTick <= now && mind.Attempts == 0)
            {
                ledger.Minds.RemoveAt(ledger.MindIndex(mind.NpcId)); // passou: quem nunca tentou não guarda nada
                continue;
            }
            if (mind.BusyUntilTick > now || frustration < rules.FrustrationThresholdMinutes || ledger.OrderIndex(mind.NpcId) >= 0) continue;
            if (npc.AgeYears(world.CurrentDate) < rules.MinInventorAgeYears || npc.Household is not { } home) continue;
            if (ledger.BestCraftSpeed(home.Value, rules) > 0) continue;

            var rng = ctx.OneShot($"invent-{npc.Id.Value}-{now}");
            double skill = Skill(npc, rules);
            double chance = rules.BaseAttemptChancePm / 1000.0
                * Math.Min(3.0, frustration / (double)rules.FrustrationThresholdMinutes)
                * (0.5 + npc.Personality.Openness / 100.0)
                * FreeHours(npc, rules) / 4.0
                * (0.5 + skill / 100.0);
            // relato vivo de que alguém da cidade já fez funcionar (e se perdeu): sabe-se que dá
            if (rumors.Contains(npc.City)) { chance *= 1 + rules.RumorAttemptBoostPm / 1000.0; world.InventionTally.RumorBoosts++; }
            if (rng.NextDouble() >= chance) continue;

            if (ledger.KnownBy(npc.Id.Value).FirstOrDefault() is { DesignId: > 0 } known)
                StartBuild(world, npc, known, current, now);
            else if (current.SeenDesignId > 0 && ledger.FindDesign(current.SeenDesignId) is { } seen
                     && rng.NextDouble() < rules.CopyPreferencePm / 1000.0)
                StartCopy(world, ctx, npc, seen, current, skill, rng, now);
            else
                StartInvention(world, ctx, npc, current, skill, rng, now);
        }
    }

    /// <summary>Concebe variantes a partir do que conhece, julga com a crença e manda construir a melhor que acredita funcionar.</summary>
    private static void StartInvention(WorldState world, TickContext ctx, Npc npc, InventionMind mind, double skill, WorldRng rng, long now)
    {
        var rules = world.InventionRules!;
        var ledger = world.Invention!;
        bool nearWater = NearWater(world, npc);
        var usable = rules.Materials.Where(m => (nearWater || !m.NeedsWaterNearby) && m.Id != mind.DistrustedMaterial).ToList();
        var known = ledger.KnownBy(npc.Id.Value).Select(k => ledger.FindDesign(k.DesignId)?.Parts).OfType<IReadOnlyList<DesignPart>>().ToList();
        var seeds = new List<IReadOnlyList<DesignPart>>();
        if (mind.LastTried is { } tried && Design.Decode(tried) is { } failed) seeds.Add(failed);
        seeds.AddRange(known);
        // o que viu boiar: a peça natural de cada material mais leve que a água
        foreach (var m in usable)
            if (!m.CoatingOnly && m.FloatsIn(rules.WaterDensityKgM3)) seeds.Add([DesignSearch.NaturalPiece(m)]);
        if (seeds.Count == 0) { ledger.PutMind(mind with { Attempts = mind.Attempts + 1 }); return; } // nada visto boiar: sem ideia

        var belief = Belief(rules, usable, skill, rng);
        // quem não tem ofício também erra o olho para o equilíbrio e para a borda: acha firme o que vira, seco o que alaga
        double eye = rules.JudgmentNoisePm / 1000.0 * (100 - skill) / 100.0;
        var judged = rules with
        {
            MinMetacentricHeightMm = (int)(rules.MinMetacentricHeightMm * (1 + 4 * eye * (rng.NextDouble() * 2 - 1))),
            MinFreeboardMm = (int)(rules.MinFreeboardMm * (1 + eye * (rng.NextDouble() * 2 - 1))),
        };
        List<DesignPart>? best = null;
        long bestScore = long.MinValue;
        for (int v = 0; v < rules.VariantsPerAttempt; v++)
        {
            var seed = seeds[v == 0 && mind.LastTried is not null ? 0 : (int)(rng.NextDouble() * seeds.Count)];
            var variant = DesignSearch.Variant(seed, known, usable, mind.LastFailure, rng);
            long score = Score(PhysicsEvaluator.Evaluate(variant, judged, belief, RiderKg(npc, rules)));
            if (score > bestScore) { bestScore = score; best = variant; }
        }

        var next = mind with { Attempts = mind.Attempts + 1 };
        if (best is null || bestScore < WorksScore) { world.InventionTally.IdeasDiscarded++; ledger.PutMind(next); return; } // nada parece funcionar: desiste por ora
        Schedule(world, ctx, npc, next, best, DesignOrigin.Invented, 0, 0, now);
    }

    /// <summary>Copia a peça que viu em uso (engenharia reversa se ninguém vivo sabe o design): com perda e mutação.</summary>
    private static void StartCopy(WorldState world, TickContext ctx, Npc npc, Design design, InventionMind mind, double skill, WorldRng rng, long now)
    {
        var rules = world.InventionRules!;
        var ledger = world.Invention!;
        world.InventionTally.CopiesFromSight++;
        bool anyoneKnows = ledger.Known.Any(k => k.DesignId == design.Id && world.FindNpc(new NpcId(k.NpcId)) is { IsAlive: true });
        int fidelity = (int)(rules.CopyFidelityPm * (0.6 + 0.4 * skill / 100.0));
        var parts = Mutated(design.Parts, fidelity, rules, rng);
        Schedule(world, ctx, npc, mind with { Attempts = mind.Attempts + 1, SeenDesignId = 0 }, parts,
            anyoneKnows ? DesignOrigin.Copied : DesignOrigin.ReverseEngineered, design.Id, 0, now);
    }

    /// <summary>Quem sabe faz a sua (ou uma para vender, com <paramref name="marketId"/>): encomenda na oficina com a receita do
    /// design; a pessoa fica ocupada até o fim estimado pelas horas no tempo livre.</summary>
    private static void StartBuild(WorldState world, Npc npc, KnownDesign known, InventionMind mind, long now, long? marketId = null)
    {
        if (world.Invention!.FindDesign(known.DesignId) is not { } design) return;
        long minutes = DesignWorkshop.Open(world, npc, design, known.FidelityPm, marketId, now);
        long until = now + DurationTicks(world, npc, Math.Max(1, minutes / 60));
        world.Invention.PutMind(mind with { BusyUntilTick = until });
    }

    private static void Schedule(WorldState world, TickContext ctx, Npc npc, InventionMind mind, IReadOnlyList<DesignPart> parts,
        DesignOrigin origin, int parent, int secondParent, long now)
    {
        var rules = world.InventionRules!;
        var perf = PhysicsEvaluator.Evaluate(parts, rules, RiderKg(npc, rules));
        long hours = Math.Max(1, perf.BuildMinutes / 60);
        long until = now + DurationTicks(world, npc, hours);
        world.Invention!.PutMind(mind with { BusyUntilTick = until });
        world.InventionTally.Attempts++;
        ctx.ScheduleEvent(until, SystemName, $"P|{npc.Id.Value}|{(int)origin}|{parent}|{secondParent}|0|{Design.Encode(parts)}");
    }

    private static void CompletePrototype(WorldState world, TickContext ctx, Npc npc, IReadOnlyList<DesignPart> intended,
        DesignOrigin origin, int parent, int secondParent)
    {
        var rules = world.InventionRules!;
        var ledger = world.Invention!;
        long now = ctx.CurrentTick;
        double skill = Skill(npc, rules);
        var rng = ctx.OneShot($"prototype-{npc.Id.Value}-{now}");
        var built = DesignSearch.AsBuilt(intended, rules.Materials, (int)(rules.ManufacturingNoisePm * (100 - skill) / 100), rng);
        var perf = PhysicsEvaluator.Evaluate(built, rules, RiderKg(npc, rules));
        int index = ledger.MindIndex(npc.Id.Value);
        var mind = index >= 0 ? ledger.Minds[index] : new InventionMind(npc.Id.Value, 0, 0, 0, 0, FailureMode.None, null, 0);
        if (!perf.Works) { Fail(world, ctx, npc, mind, perf.Failure, built, now, build: false); return; }

        var design = Design.Create(built, npc.Id.Value, now, parent, secondParent, origin);
        bool isNew = ledger.FindDesign(design.Id) is null;
        ledger.AddDesign(design);
        ledger.Learn(new KnownDesign(npc.Id.Value, design.Id, 1000, origin, now));
        AddItem(world, npc, design, skill, now);
        ledger.PutMind(mind with { FrustrationMinutes = 0, LastTried = null, LastFailure = FailureMode.None });
        if (isNew && origin == DesignOrigin.Invented) world.InventionTally.Inventions++;
        if (origin == DesignOrigin.Copied) world.InventionTally.Copied++;
        if (origin == DesignOrigin.ReverseEngineered) world.InventionTally.ReverseEngineered++;
        ctx.LogEvent(WorldEventKind.DesignPrototyped,
            string.Create(CultureInfo.InvariantCulture, $"{npc.Id.Value}|{design.Id}|{(int)origin}|{parent}|{secondParent}"), SystemName);
    }

    /// <summary>Construção agendada pela fatia 1 (evento "B" de snapshot antigo): conclui como antes.</summary>
    private static void CompleteBuild(WorldState world, TickContext ctx, Npc npc, Design design, int fidelity)
    {
        var rules = world.InventionRules!;
        long now = ctx.CurrentTick;
        double skill = Skill(npc, rules);
        var rng = ctx.OneShot($"build-{npc.Id.Value}-{now}");
        int noise = (int)(rules.ManufacturingNoisePm * (100 - skill) / 100 + (1000 - fidelity) / 4);
        var built = DesignSearch.AsBuilt(design.Parts, rules.Materials, noise, rng);
        var perf = PhysicsEvaluator.Evaluate(built, rules, RiderKg(npc, rules));
        var ledger = world.Invention!;
        int index = ledger.MindIndex(npc.Id.Value);
        var mind = index >= 0 ? ledger.Minds[index] : new InventionMind(npc.Id.Value, 0, 0, 0, 0, FailureMode.None, null, 0);
        if (!perf.Works)
        {
            world.InventionTally.BuildFailures++;
            Fail(world, ctx, npc, mind, perf.Failure, null, now, build: true);
            int i = ledger.MindIndex(npc.Id.Value); // desanima: só volta a tentar daqui a uns meses
            ledger.PutMind(ledger.Minds[i] with { BusyUntilTick = now + rules.RetryAfterFailureMonths * world.Calendar.HoursPerMonth });
            return;
        }
        AddItem(world, npc, design, skill, now);
        world.InventionTally.Built++;
        ledger.PutMind(mind with { FrustrationMinutes = 0 });
    }

    /// <summary>A falha vira lembrança (pela vida interior) e aprendizado: o protótipo fica de ponto de partida e, se quebrou ou
    /// afundou por causa do material, a pessoa passa a desconfiar dele.</summary>
    internal static void Fail(WorldState world, TickContext ctx, Npc npc, InventionMind mind, FailureMode mode, IReadOnlyList<DesignPart>? tried, long now, bool build)
    {
        var rules = world.InventionRules!;
        var tally = world.InventionTally;
        if (!build) tally.PrototypeFailures++;
        if (!build) switch (mode)
            {
                case FailureMode.Sinks: tally.Sank++; break;
                case FailureMode.Capsizes: tally.Capsized++; break;
                case FailureMode.Breaks: tally.Broke++; break;
                case FailureMode.TooSlow: tally.TooSlow++; break;
                default: tally.Invalid++; break;
            }
        int distrust = mind.DistrustedMaterial;
        if (tried is { Count: > 0 } && mode is FailureMode.Breaks or FailureMode.Sinks
            && rules.Material(tried[0].Material) is { } root
            && (mode == FailureMode.Breaks || (!tried[0].IsHollow && !root.FloatsIn(rules.WaterDensityKgM3))))
            distrust = root.Id;
        world.Invention!.PutMind(mind with
        {
            Failures = mind.Failures + 1,
            LastFailure = mode,
            DistrustedMaterial = distrust,
            LastTried = tried is null ? mind.LastTried : Design.Encode(tried),
        });
        ctx.LogEvent(WorldEventKind.PrototypeFailed,
            string.Create(CultureInfo.InvariantCulture, $"{npc.Id.Value}|{(int)mode}|{(build ? 1 : 0)}"), SystemName);
    }

    /// <summary>Quem sabe ensina (alguém da casa ou um amigo próximo; quem é ambicioso e pouco altruísta guarda segredo fora de
    /// casa) e, sem uma embarcação em casa, constrói a sua.</summary>
    private static void Knowers(WorldState world, TickContext ctx, long now)
    {
        var rules = world.InventionRules!;
        var ledger = world.Invention!;
        if (ledger.Known.Count == 0) return;
        var lessons = new List<(long Learner, long Teacher, KnownDesign From)>();
        var builders = new List<(Npc Npc, KnownDesign Known, long? Market)>();
        // procura por cidade que ainda não tem peça exposta nem encomenda para vender (o vendedor lê a demanda do mês passado)
        var open = new Dictionary<CityId, int>();
        foreach (var d in ledger.Demand) open[d.City] = d.Households;
        foreach (var item in ledger.Items) if (item.ForSale && open.ContainsKey(item.City)) open[item.City]--;
        foreach (var order in ledger.Orders)
            if (order.MarketId is not null && world.FindNpc(new NpcId(order.NpcId)) is { } maker && open.ContainsKey(maker.City)) open[maker.City]--;
        long last = -1;
        foreach (var known in ledger.Known.ToList())
        {
            if (known.NpcId == last) continue; // uma vez por pessoa por mês: ensina e constrói o primeiro que sabe
            last = known.NpcId;
            if (world.FindNpc(new NpcId(known.NpcId)) is not { IsAlive: true } teacher || teacher.Household is not { } home) continue;
            var rng = ctx.OneShot($"teach-{teacher.Id.Value}-{now}");
            if (rng.NextDouble() < rules.TeachChancePm / 1000.0 && PickLearner(world, teacher, known.DesignId, rng) is { } learner)
                lessons.Add((learner.Id.Value, teacher.Id.Value, known));
            int mindIndex = ledger.MindIndex(teacher.Id.Value);
            bool busy = (mindIndex >= 0 && ledger.Minds[mindIndex].BusyUntilTick > now) || ledger.OrderIndex(teacher.Id.Value) >= 0;
            bool needs = mindIndex >= 0 && ledger.Minds[mindIndex].FrustrationMinutes > 0;
            if (busy || teacher.AgeYears(world.CurrentDate) < rules.MinInventorAgeYears) continue;
            if (needs && ledger.BestCraftSpeed(home.Value, rules) == 0 && rng.NextDouble() < rules.BuildChancePm / 1000.0)
                builders.Add((teacher, known, null));
            else if (open.GetValueOrDefault(teacher.City) > 0 && rng.NextDouble() < rules.BuildChancePm / 1000.0
                     && InventionMarket.MarketFor(world, teacher) is { } market)
            {
                builders.Add((teacher, known, market.Id.Value)); // a cidade procura: faz para vender
                open[teacher.City]--;
            }
        }

        foreach (var (learnerId, teacherId, from) in lessons)
        {
            if (world.FindNpc(new NpcId(learnerId)) is not { } learner || ledger.FindDesign(from.DesignId) is not { } design) continue;
            // a perda fica na fidelidade: aparece como erro na hora de construir, não como design novo que ninguém provou
            int fidelity = (int)Math.Clamp((long)from.FidelityPm * rules.TeachFidelityPm / 1000 * (700 + 3 * (int)Skill(learner, rules)) / 1000, 100, 1000);
            int learnedId = design.Id;
            ledger.Learn(new KnownDesign(learnerId, learnedId, (short)fidelity, DesignOrigin.Taught, now));
            world.InventionTally.Taught++;
            ctx.LogEvent(WorldEventKind.DesignLearned,
                string.Create(CultureInfo.InvariantCulture, $"{learnerId}|{teacherId}|{learnedId}|{(int)DesignOrigin.Taught}|{fidelity}"), SystemName);
        }

        foreach (var (npc, known, market) in builders)
        {
            int index = ledger.MindIndex(npc.Id.Value);
            var mind = index >= 0 ? ledger.Minds[index] : new InventionMind(npc.Id.Value, 0, 0, 0, 0, FailureMode.None, null, 0);
            StartBuild(world, npc, known, mind, now, market);
        }
    }

    private static Npc? PickLearner(WorldState world, Npc teacher, int designId, WorldRng rng)
    {
        var rules = world.InventionRules!;
        var ledger = world.Invention!;
        var candidates = new List<Npc>();
        if (teacher.Household is { } h && world.FindHousehold(h) is { } home)
            foreach (var member in home.Members.OrderBy(m => m.Value))
                if (member != teacher.Id && world.FindNpc(member) is { IsAlive: true } other) candidates.Add(other);
        double openness = (teacher.Personality.Altruism + 100 - teacher.Personality.Ambition) / 200.0;
        if (rng.NextDouble() < openness)
        {
            var targets = world.RelationshipTargetsOf(teacher.Id);
            for (int i = 0; i < targets.Count; i++)
                if (world.Relationships.TryGetValue(new LivingWorld.Domain.Population.Family.RelationshipKey(teacher.Id, targets[i]), out var tie)
                    && tie.Affection >= rules.TeachAffection && world.FindNpc(targets[i]) is { IsAlive: true } friend
                    && friend.City == teacher.City && !candidates.Contains(friend))
                    candidates.Add(friend);
        }
        candidates.RemoveAll(c => c.AgeYears(world.CurrentDate) < rules.MinInventorAgeYears || ledger.KnownBy(c.Id.Value).Any(k => k.DesignId == designId));
        return candidates.Count == 0 ? null : candidates[(int)(rng.NextDouble() * candidates.Count)];
    }

    /// <summary>O que se perde ao aprender sem ter feito: com fidelidade abaixo de 1000, chance de uma medida sair errada.</summary>
    private static IReadOnlyList<DesignPart> Mutated(IReadOnlyList<DesignPart> parts, int fidelityPm, InventionRules rules, WorldRng rng)
    {
        if (rng.NextDouble() * 1000 >= 1000 - fidelityPm) return parts;
        return DesignSearch.AsBuilt(parts, rules.Materials, 1000 - fidelityPm, rng);
    }

    internal static void AddItem(WorldState world, Npc npc, Design design, double skill, long now)
    {
        if (npc.Household is null) return;
        world.Invention!.Items.Add(NewItem(world, npc, design, skill, now));
        world.Invention.Touch();
    }

    /// <summary>A peça feita por <paramref name="npc"/>: dura a vida do material mais curto × a qualidade (que sai da habilidade).</summary>
    internal static CraftedItem NewItem(WorldState world, Npc npc, Design design, double skill, long now)
    {
        var rules = world.InventionRules!;
        int months = int.MaxValue;
        foreach (var p in design.Parts) if (rules.Material(p.Material) is { } m) months = Math.Min(months, m.LifetimeMonths);
        short quality = (short)Math.Clamp(600 + 4 * (int)skill, 600, 1000);
        long expires = now + (long)months * world.Calendar.HoursPerMonth * quality / 1000;
        return new CraftedItem(world.Invention!.NextItemId++, design.Id, npc.Household?.Value ?? 0, npc.City, now, expires, quality);
    }

    /// <summary>Crença da pessoa sobre os materiais: densidade e resistência com erro que cai com a habilidade.</summary>
    private static List<MaterialSpec> Belief(InventionRules rules, IReadOnlyList<MaterialSpec> usable, double skill, WorldRng rng)
    {
        double noise = rules.JudgmentNoisePm / 1000.0 * (100 - skill) / 100.0;
        var belief = new List<MaterialSpec>(rules.Materials.Count);
        foreach (var m in rules.Materials)
        {
            if (!usable.Contains(m)) { belief.Add(m); continue; }
            belief.Add(m with
            {
                DensityKgM3 = Math.Max(1, (int)(m.DensityKgM3 * (1 + noise * (rng.NextDouble() * 2 - 1)))),
                TensileMPa = Math.Max(0, (int)(m.TensileMPa * (1 + noise * (rng.NextDouble() * 2 - 1)))),
            });
        }
        return belief;
    }

    private const long WorksScore = 1_000_000;

    private static long Score(Performance perf) => perf.Failure switch
    {
        // quem nunca fez tenta primeiro o mais simples que acha que serve: o esforço pesa mais que a sobra de carga
        FailureMode.None => WorksScore + perf.SpeedMmS * 10L + Math.Min(perf.PayloadG / 1000, 100) * 50 - perf.BuildMinutes * 20L,
        FailureMode.TooSlow or FailureMode.Breaks => 300_000 + perf.SpeedMmS,
        FailureMode.Capsizes => 200_000 + perf.MetacentricMm,
        FailureMode.Sinks => 100_000 + perf.PayloadG / 1000,
        _ => 0,
    };

    internal static double Skill(Npc npc, InventionRules rules)
    {
        double craft = npc.Skills.Get(new SkillType(rules.CraftSkillId));
        double best = 0;
        foreach (var value in npc.Skills.Values.Values) if (value > best) best = value;
        return Math.Clamp(Math.Max(craft, best / 2), 0, 100);
    }

    private static int FreeHours(Npc npc, InventionRules rules) => npc.Employer is null ? rules.FreeHoursIdle : rules.FreeHoursEmployed;

    internal static int RiderKg(Npc npc, InventionRules rules) => npc.Weight > 0 ? (int)Math.Round(npc.Weight) : rules.ReferenceRiderKg;

    /// <summary>As horas de trabalho espalhadas pelo tempo livre da pessoa, em dias de calendário.</summary>
    private static long DurationTicks(WorldState world, Npc npc, long hours)
    {
        int free = Math.Max(1, FreeHours(npc, world.InventionRules!));
        return (hours + free - 1) / free * world.Calendar.HoursPerDay;
    }

    private static bool NearWater(WorldState world, Npc npc)
    {
        var at = npc.Household is { } h && world.FindHousehold(h) is { } home ? home.Location : npc.CurrentLocation;
        int r = world.InventionRules!.WaterObservationRadius;
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
                if (world.Map.TryGetCell(new CellCoord(at.X + dx, at.Y + dy), out var cell) && cell.HasWater) return true;
        return false;
    }

    private static long Long(string s) => long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? v : 0;

    private static int Int(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
}
