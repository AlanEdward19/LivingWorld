using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Objects;

/// <summary>Uma interação social como o cliente a lê para quem está perto: id, nome, categoria, se vale agora e, se não, o motivo.</summary>
public sealed record SocialOption(string Id, string Name, string Category, string CategoryName, bool Available, string? Reason, int Seconds);

/// <summary>A relação vista de quem pergunta: o que a outra pessoa sente (afeto e confiança, 0–100), o que quem pergunta sente, o estado do par, o humor dela
/// e a última coisa que aconteceu entre os dois (texto do catálogo, com o instante em ms de mundo).</summary>
public sealed record RelationView(
    string State, string StateLabel, int Affection, int Trust, int MineAffection, int MineTrust, string Mood, int MoodScore, string? Last, long? LastMs);

/// <summary>#259: o que a interação social fez ao ser pedida: <c>Accepted</c> falso é a recusa (com <c>Reason</c>); <c>Note</c> é a frase do catálogo.</summary>
public sealed record SocialStart(bool Accepted, string Note, string? Reason);

/// <summary>O que a pessoa vive agora, para o corpo e para quem a vê: o id e o nome da interação, a fase, o gesto, o outro e o papel (quem começou).</summary>
public sealed record SocialView(string Id, string Name, string Phase, string? Pose, long With, bool Initiator, string? PartnerPose = null);

/// <summary>#259 (S3 do sandbox): interações sociais entre duas pessoas, tudo pelo catálogo (<see cref="SocialCatalog"/>): o código só tem os mecanismos —
/// requisito, decisão de aceitar por termos pesados com o RNG semeado do mundo, os dois corpos ocupados (quem começou anda até ficar ao lado) e os efeitos nos
/// laços, nas necessidades e no humor, ao fim (interrompida, na fração vivida). Jogador→pessoa e pessoa→pessoa passam pelo mesmo <see cref="Start"/>. O
/// gesto e a virada de um para o outro são do cliente, que lê o <c>busy</c> da pose e o <c>social</c> do ocupante. Atividades conjuntas usam os usos de objeto
/// do catálogo com dois corpos (<see cref="SocialJoint"/>). Tudo em ms de mundo, sem estado por tick: eventos agendados (<see cref="BodyTaskSystem"/>).</summary>
public static partial class SocialInteractions
{
    /// <summary>O <c>Action</c> do <see cref="BodyTask"/> de uma interação social.</summary>
    public const string Action = "social";

    private const long SecondMs = 1000;
    private const long GiveUpMs = 20 * SecondMs;

    public static SocialCatalog? CatalogOf(WorldState world) => world.ObjectCatalog?.Social;

    public static bool Active(Npc npc) => npc.BodyTask is { Action: Action };

    // ---------------- leitura: relação, humor, estado ----------------

    /// <summary>A relação de <paramref name="from"/> por <paramref name="to"/>, se já existe.</summary>
    private static Relationship? Rel(WorldState world, Npc from, Npc to)
    {
        if (world.Relationships.TryGetValue(new RelationshipKey(from.Id, to.Id), out var r)) return r;
        return SpouseBaseline(world, from, to) is { } baseline ? Relationship.FromAxes(baseline.Trust, baseline.Affection, 0, 0, 0) : null; // leitura: nada é criado
    }

    /// <summary>O que um casal sente desde o começo (do catálogo) enquanto nenhum laço foi registrado entre eles; nulo para quem não é casal ou sem esse dado.</summary>
    private static (double Affection, double Trust)? SpouseBaseline(WorldState world, Npc from, Npc to) =>
        from.Spouse == to.Id && CatalogOf(world)?.Rules is { } rules && (rules.SpouseAffection > 0 || rules.SpouseTrust > 0) ? (rules.SpouseAffection, rules.SpouseTrust) : null;

    /// <summary>Ao começar o mundo: o casal ganha o laço que o catálogo diz que sentem (gravado, para não esfriar até zero sem convívio registrado). Sem catálogo social, nada.</summary>
    public static void SeedBond(WorldState world, Npc npc)
    {
        if (npc.Spouse is not { } id || world.FindNpc(id) is not { } spouse || npc.Id.Value > id.Value || SpouseBaseline(world, npc, spouse) is null) return;
        long hours = world.CurrentDate.TotalHours;
        Ensure(world, npc, spouse, hours);
        Ensure(world, spouse, npc, hours);
    }

    /// <summary>O laço de <paramref name="from"/> por <paramref name="to"/> para escrever: o que existe, ou um novo (de um casal, com o que o catálogo diz que sentem desde o começo).</summary>
    private static Relationship Ensure(WorldState world, Npc from, Npc to, long hours)
    {
        bool had = world.Relationships.ContainsKey(new RelationshipKey(from.Id, to.Id));
        var rel = world.GetOrCreateRelationship(new RelationshipKey(from.Id, to.Id), hours);
        if (!had && SpouseBaseline(world, from, to) is { } b)
        {
            rel.Shift(RelationshipAxis.Affection, b.Affection);
            rel.Shift(RelationshipAxis.Trust, b.Trust);
        }

        return rel;
    }

    /// <summary>A nota de humor (0–100) pelo catálogo: emoção líquida e as necessidades que pesam; a primeira faixa que a nota não passa dá o rótulo.</summary>
    public static (int Score, string Label) Mood(WorldState world, SocialCatalog catalog, Npc npc, long nowMs)
    {
        var mood = catalog.Mood;
        double valence = PsycheMechanic.AffectOf(world, npc, nowMs / WorldMillis.PerHour).Valence;
        double score = 50 + mood.ValenceWeight * valence;
        foreach (var term in mood.Needs)
            score += term.Weight * ((HomeLife.Value(world, npc, term.Need, nowMs) ?? 50) - 50) / 50.0;
        if (MoodMechanic.CatalogOf(world) is not null)
        {
            score += MoodMechanic.ScoreShift(world, npc, nowMs); // #262: os momentos pesam na nota (e na aceitação); #267: por curva suave e teto por emoção
            score = MoodMechanic.Band(world, npc, nowMs, score); // #267: a nota acompanha a emoção dominante
        }

        score = Math.Clamp(score, 0, 100);
        string label = mood.Labels[^1].Label;
        foreach (var l in mood.Labels)
            if (score < l.Below) { label = l.Label; break; }
        return ((int)Math.Round(score, MidpointRounding.AwayFromZero), label);
    }

    /// <summary>O estado do par visto por <paramref name="viewer"/> (o que ele sente pela outra pessoa e o casamento): o primeiro estado do catálogo que casa; vazio se nenhum.</summary>
    public static RelationStateDef? StateOf(WorldState world, SocialCatalog catalog, Npc viewer, Npc other)
    {
        var rel = Rel(world, viewer, other);
        double affection = rel?.Affection ?? 0, trust = rel?.Trust ?? 0;
        bool spouse = viewer.Spouse == other.Id;
        foreach (var state in catalog.States)
        {
            if (state.Spouse && !spouse) continue;
            if (affection < state.AffectionMin || trust < state.TrustMin) continue;
            if (state.AffectionMax is { } max && affection > max) continue;
            return state;
        }

        return null;
    }

    /// <summary>A relação como o jogador a vê ao mirar <paramref name="other"/>: o que ela sente por <paramref name="viewer"/>, o que ele sente, o estado, o humor dela e a última interação.</summary>
    public static RelationView? RelationOf(WorldState world, Npc viewer, Npc other)
    {
        if (CatalogOf(world) is not { } catalog) return null;
        long now = world.CurrentDate.TotalMilliseconds;
        var theirs = Rel(world, other, viewer);
        var mine = Rel(world, viewer, other);
        var state = StateOf(world, catalog, other, viewer);
        var (score, label) = Mood(world, catalog, other, now);
        var last = theirs?.LastSocial is not null ? theirs : mine?.LastSocial is not null ? mine : null;
        string? text = last is null ? null : LastText(world, catalog, viewer, other, last);
        return new RelationView(state?.Id ?? "", state?.Label ?? "", Round(theirs?.Affection), Round(theirs?.Trust), Round(mine?.Affection), Round(mine?.Trust), label, score,
            text, last?.LastSocialMs);
    }

    private static int Round(double? v) => (int)Math.Round(v ?? 0, MidpointRounding.AwayFromZero);

    /// <summary>A frase do catálogo para o que aconteceu por último no par. <paramref name="rel"/> é o registro do lado de <paramref name="viewer"/> ou do outro.</summary>
    private static string? LastText(WorldState world, SocialCatalog catalog, Npc viewer, Npc other, Relationship rel)
    {
        if (catalog.Find(rel.LastSocial!) is not { } def) return null;
        bool viewerActed = Rel(world, viewer, other) is { } mine && ReferenceEquals(mine, rel) ? rel.LastSocialActor : !rel.LastSocialActor;
        var (actor, target) = viewerActed ? (viewer, other) : (other, viewer);
        return Format(rel.LastSocialAccepted ? def.AcceptedText ?? def.Name : def.RefusedText ?? def.Name, actor.Name, target.Name, Delta(def, rel.LastSocialAccepted), null);
    }

    private static int Delta(SocialInteractionDef def, bool accepted) =>
        (int)Math.Round((accepted ? def.Effects : def.OnRefuse ?? def.Effects).Target.Affection, MidpointRounding.AwayFromZero);

    /// <summary>Troca <c>{actor}</c>, <c>{target}</c>, <c>{delta}</c> (com sinal) e <c>{reason}</c> no texto do catálogo.</summary>
    internal static string Format(string text, string actor, string target, int delta, string? reason) =>
        text.Replace("{actor}", actor).Replace("{target}", target).Replace("{delta}", delta >= 0 ? "+" + delta : delta.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("{reason}", reason ?? "");

    private static string Text(SocialCatalog catalog, string key, Npc actor, Npc target, string fallback) =>
        Format(catalog.Rules.Texts.TryGetValue(key, out var t) ? t : fallback, actor.Name, target.Name, 0, null);

    // ---------------- disponibilidade (sem sorteio) ----------------

    private static bool Adult(WorldState world, SocialCatalog catalog, Npc npc) => npc.AgeYears(world.CurrentDate) >= catalog.Rules.AdultYears;

    private static bool Busy(WorldState world, Npc npc, long nowMs) =>
        PlayerTasks.Of(world, npc) is not null || nowMs < npc.StepReadyAtMs || npc.CurrentAction == ActionType.Travel
        || (npc.Fine is { } f && (f.HeadingX != 0 || f.HeadingY != 0) && npc.ControllerId is not null);

    /// <summary>#261: o corpo está sentado, deitado ou usando um móvel (a ação do uso já começou): o uso não é uma ocupação que impeça conversar.</summary>
    public static bool Sitting(WorldState world, Npc npc, long nowMs) =>
        npc.CurrentAction != ActionType.Sleep && npc.BodyTask is { Action: ObjectUsage.Action, Doing: true } t && nowMs < t.UntilMs;

    /// <summary>Ocupado de verdade (anda, trabalha, outra interação): quem só usa um móvel não conta (<see cref="Sitting"/>).</summary>
    private static bool Occupied(WorldState world, Npc npc, long nowMs) => !Sitting(world, npc, nowMs) && Busy(world, npc, nowMs);

    /// <summary>#261: <paramref name="b"/> aparece para <paramref name="a"/> no menu social: mesma cidade, andar e prédio (ou os dois fora), a até
    /// <see cref="SocialRules.ApproachM"/> metros e sem parede no caminho em linha reta (a regra do chão do passo, <see cref="PlayerGround.TerrainRefusal"/>) ou com uma porta no caminho (rota curta).</summary>
    public static bool InSight(WorldState world, SocialCatalog catalog, Npc a, Npc b)
    {
        if (a.City != b.City || !PlayerGround.SameLevel(a, b) || PlayerGround.InsideBuilding(world, a) != PlayerGround.InsideBuilding(world, b)) return false;
        var (ax, ay) = FineOf(world, a);
        var (bx, by) = FineOf(world, b);
        double perM = FinePose.PerCell / Math.Max(0.1, world.Players.CellM);
        double dx = bx - ax, dy = by - ay, len = Math.Sqrt(dx * dx + dy * dy);
        if (len > catalog.Rules.ApproachM * perM) return false;
        int floor = PlayerGround.FloorOf(a);
        var own = a.Interior?.Building;
        var cache = new Dictionary<CellCoord, PlayerGround.Spot?>();
        PlayerGround.Spot? At(CellCoord c)
        {
            if (!cache.TryGetValue(c, out var spot)) cache[c] = spot = PlayerGround.At(world, c, floor);
            return spot;
        }

        var prev = new CellCoord((int)Math.Floor(ax / (double)FinePose.PerCell), (int)Math.Floor(ay / (double)FinePose.PerCell));
        int steps = Math.Max(1, (int)Math.Ceiling(len / (FinePose.PerCell / 4.0)));
        for (int i = 1; i <= steps; i++)
        {
            long x = ax + (long)Math.Round(dx * i / steps), y = ay + (long)Math.Round(dy * i / steps);
            var cell = new CellCoord((int)Math.Floor(x / (double)FinePose.PerCell), (int)Math.Floor(y / (double)FinePose.PerCell));
            if (cell == prev) continue;
            if (PlayerGround.TerrainRefusal(world, floor, own, prev, cell, At) is not null)
            {
                // parede no meio da reta: ainda aparece se a pessoa está ao alcance POR UMA PORTA (a rota do corpo até ela, em metros, cabe em 1,6× o alcance)
                var goal = new CellCoord((int)Math.Floor(bx / (double)FinePose.PerCell), (int)Math.Floor(by / (double)FinePose.PerCell));
                var start = new CellCoord((int)Math.Floor(ax / (double)FinePose.PerCell), (int)Math.Floor(ay / (double)FinePose.PerCell));
                var route = PlayerWalk.Route(world, a, floor, start, goal, At);
                return route is not null && route.Count * Math.Max(0.1, world.Players.CellM) <= catalog.Rules.ApproachM * 1.6;
            }

            prev = cell;
        }

        return true;
    }

    /// <summary>O motivo de a interação não valer agora entre <paramref name="actor"/> e <paramref name="target"/>; nulo se vale. Não sorteia nada.</summary>
    public static string? Unavailable(WorldState world, SocialCatalog catalog, SocialInteractionDef def, Npc actor, Npc target, long nowMs)
    {
        if (!actor.IsAlive || !target.IsAlive || actor.Id == target.Id) return Text(catalog, "far", actor, target, "indisponível");
        if (!InSight(world, catalog, actor, target)) return Text(catalog, "far", actor, target, "longe demais");
        if (Occupied(world, actor, nowMs)) return Text(catalog, "actorBusy", actor, target, "você está ocupado");
        if (target.CurrentAction == ActionType.Sleep && PlayerTasks.Of(world, target) is null) return Text(catalog, "targetAsleep", actor, target, "{target} está dormindo");
        if (Occupied(world, target, nowMs)) return Text(catalog, "targetBusy", actor, target, "{target} está ocupado");
        // #261: o alvo sentado ou usando um móvel só conversa o que o catálogo declara compatível com o uso (quem começa levanta sozinho, como no Sims)
        if (!def.CompatibleWithUse && Sitting(world, target, nowMs)) return Text(catalog, "targetUsing", actor, target, "{target} está usando um móvel agora");
        var req = def.Requires;
        if (req.AdultsOnly && !(Adult(world, catalog, actor) && Adult(world, catalog, target))) return req.Reason ?? Text(catalog, "adults", actor, target, "só entre adultos");
        var theirs = Rel(world, target, actor);
        if (req.States is { Count: > 0 } states && (StateOf(world, catalog, target, actor) is not { } state || !states.Contains(state.Id)))
            return req.Reason ?? Text(catalog, "state", actor, target, "a relação de vocês não chega a isso");
        if ((theirs?.Affection ?? 0) < req.AffectionMin) return req.Reason ?? Text(catalog, "affection", actor, target, "falta afeição");
        if ((theirs?.Trust ?? 0) < req.TrustMin) return req.Reason ?? Text(catalog, "trust", actor, target, "falta confiança");
        if (def.Joint is { } joint && FindJoint(world, catalog, joint, actor, target) is null) return Text(catalog, "noPlace", actor, target, "não há onde");
        return null;
    }

    /// <summary>As interações do catálogo para <paramref name="other"/> ao alcance: as que valem e as que não (com o motivo), na ordem do catálogo.</summary>
    public static IReadOnlyList<SocialOption>? OptionsFor(WorldState world, Npc actor, Npc other)
    {
        if (CatalogOf(world) is not { } catalog) return null;
        long now = world.CurrentDate.TotalMilliseconds;
        var list = new List<SocialOption>(catalog.Interactions.Count);
        foreach (var def in catalog.Interactions)
        {
            string? reason = Unavailable(world, catalog, def, actor, other, now);
            int seconds = def.Joint is { } j && JointMinutes(world, j.Use) is { } m ? m * 60 : def.DurationSec;
            list.Add(new SocialOption(def.Id, def.Name, def.Category, catalog.CategoryOf(def.Category)?.Name ?? def.Category, reason is null, reason, seconds));
        }

        return list;
    }

    private static int? JointMinutes(WorldState world, string use) =>
        world.ObjectCatalog?.Objects.SelectMany(o => o.Uses ?? []).FirstOrDefault(u => u.Id == use)?.DurationMin;

    // ---------------- aceitação (sorteio do mundo) ----------------

    private static double ValueOf(WorldState world, SocialCatalog catalog, Npc actor, Npc target, string of, long nowMs)
    {
        var parts = of.Split('.');
        switch (parts[0])
        {
            case "relation":
                var rel = Rel(world, target, actor);
                return parts[1] switch { "affection" => rel?.Affection ?? 0, "trust" => rel?.Trust ?? 0, _ => rel?.Respect ?? 0 };
            case "actor" or "target":
                var who = parts[0] == "actor" ? actor : target;
                if (parts[1] == "mood") return Mood(world, catalog, who, nowMs).Score;
                if (parts[1] == "need") return HomeLife.Value(world, who, parts[2], nowMs) ?? 50;
                return Trait(who.Personality, parts[2]);
            default: // recent.{categoria}: a última interação do par foi dessa categoria, dentro do histórico
                var last = Rel(world, target, actor);
                return last?.LastSocial is { } id && nowMs - last.LastSocialMs <= catalog.Rules.HistoryHours * WorldMillis.PerHour
                    && catalog.Find(id)?.Category == parts[1] ? 1 : 0;
        }
    }

    public static double TraitOf(Personality p, string name) => Trait(p, name);

    internal static double Trait(Personality p, string name) => name switch
    {
        "extroversion" => p.Extroversion,
        "agreeableness" => p.Agreeableness,
        "conscientiousness" => p.Conscientiousness,
        "emotionalstability" => p.EmotionalStability,
        "openness" => p.Openness,
        "ambition" => p.Ambition,
        "loyalty" => p.Loyalty,
        "altruism" => p.Altruism,
        "impulsivity" => p.Impulsivity,
        _ => p.RiskAversion,
    };

    /// <summary>A chance (0–100) de <paramref name="target"/> aceitar e, se algum termo pesa contra, o motivo do que mais pesa (frase do catálogo).</summary>
    public static (double Chance, string? Reason) Evaluate(WorldState world, SocialCatalog catalog, SocialInteractionDef def, Npc actor, Npc target, long nowMs)
    {
        double chance = def.Accept.Base, worst = 0;
        string? reason = null;
        foreach (var term in def.Accept.Terms)
        {
            double contribution = term.Weight * (ValueOf(world, catalog, actor, target, term.Of, nowMs) - term.From);
            chance += contribution;
            if (contribution < worst) { worst = contribution; reason = term.Reason ?? reason; }
        }

        return (chance, Format(reason ?? def.Accept.RefuseReason ?? "", actor.Name, target.Name, 0, null));
    }

    // ---------------- começar ----------------

    /// <summary>Pede a interação <paramref name="id"/> de <paramref name="actor"/> a <paramref name="target"/> (jogador ou NPC, o mesmo caminho): valida, o alvo aceita ou
    /// recusa pelo estado dele, e aceita ocupa os dois corpos. Recusa de validação é erro (409); recusa do alvo é sucesso com <see cref="SocialStart.Accepted"/> falso.</summary>
    public static Result<SocialStart> Start(WorldState world, TickContext ctx, Npc actor, Npc target, string id)
    {
        if (CatalogOf(world) is not { } catalog || catalog.Find(id) is not { } def) return Result<SocialStart>.Fail($"{PlayerCommands.NotFound} social: interação desconhecida");
        long now = world.CurrentDate.TotalMilliseconds;
        SettleDue(world, ctx, actor);
        SettleDue(world, ctx, target);
        if (Unavailable(world, catalog, def, actor, target, now) is { } why) return Result<SocialStart>.Fail($"{PlayerCommands.Conflict} social: {why}");
        bool actorSits = Sitting(world, actor, now), targetSits = Sitting(world, target, now);
        if (actorSits && !def.CompatibleWithUse && def.Joint is null) ObjectUsage.Stop(world, ctx, actor); // #261: levanta antes (como no Sims); a conversa leve não cancela o uso

        var (chance, reason) = Evaluate(world, catalog, def, actor, target, now);
        bool accepted = target.ControllerId is not null || chance >= 100
            || (chance > 0 && ctx.OneShot($"social-{actor.Id.Value}-{target.Id.Value}-{def.Id}-{now}").NextDouble() * 100.0 < chance);
        if (!accepted)
        {
            if (def.OnRefuse is { } refusal) ApplyEffects(world, refusal, actor, target, 1.0, now, def.Id);
            MarkPair(world, def, actor, target, now, accepted: false);
            MoodMechanic.OnSocial(world, def, actor, target, accepted: false, now); // #262
            string why2 = string.IsNullOrWhiteSpace(reason) ? Text(catalog, "refused", actor, target, "não quis") : reason;
            string refusedText = Format(def.RefusedText ?? "{target} recusou", actor.Name, target.Name, Delta(def, false), why2);
            return Result<SocialStart>.Ok(new SocialStart(false, refusedText.Contains(why2, StringComparison.Ordinal) ? refusedText : $"{refusedText}: {why2}", why2));
        }

        if (def.Joint is { } joint) return StartJoint(world, ctx, catalog, def, joint, actor, target, now);

        if (def.CompatibleWithUse && actorSits) // #261: sentado, a conversa leve vale na hora e o uso segue (sem andar, sem gesto que tire o corpo do móvel)
        {
            ApplyEffects(world, def.Effects, actor, target, 1.0, now, def.Id);
            MarkPair(world, def, actor, target, now, accepted: true);
            return Result<SocialStart>.Ok(new SocialStart(true, Format(def.Name, actor.Name, target.Name, 0, null), null));
        }

        bool light = def.CompatibleWithUse && targetSits; // o alvo segue no uso dele; só quem começou anda até ele
        var mine = new BodyTask(Action, now, now + GiveUpMs) { Use = def.Id, Partner = target.Id.Value, Initiator = true, LastMs = now };
        var theirs = new BodyTask(Action, now, now + GiveUpMs) { Use = def.Id, Partner = actor.Id.Value };
        if (!light)
        {
            if (target.ControllerId is not null) PlayerCommands.TakeTheWheel(world, target);
            if (target.Fine is { } f) target.SetFine(f with { HeadingX = 0, HeadingY = 0, AtMs = now });
            target.SetBodyTask(theirs);
            target.Step(now + GiveUpMs, 0, now);
        }
        if (actor.ControllerId is not null) PlayerCommands.TakeTheWheel(world, actor);
        actor.SetBodyTask(mine);
        if (actor.ControllerId is null || !world.FineTime)
        {
            PlaceBeside(world, actor, target);
            Arrive(world, ctx, actor, target, def, now);
        }
        else
        {
            actor.Step(now + GiveUpMs, 0, now);
            ctx.ScheduleEventAtMs(now + 1, BodyTaskSystem.SystemName, Payload(actor, now));
        }

        return Result<SocialStart>.Ok(new SocialStart(true, Format(def.Name, actor.Name, target.Name, 0, null), null));
    }

    internal static string Payload(Npc actor, long startedAtMs) => $"{actor.Id.Value}|{startedAtMs}|s";

    /// <summary>Onde o corpo de <paramref name="npc"/> fica, em milésimos de célula: o ponto fino se o tem na célula; senão o centro da célula.</summary>
    private static (long X, long Y) FineOf(WorldState world, Npc npc)
    {
        var cell = PlayerGround.PoseOf(world, npc);
        return npc.Fine is { } f && f.X / FinePose.PerCell == cell.X && f.Y / FinePose.PerCell == cell.Y && npc.ControllerId is not null
            ? (f.X, f.Y) : (cell.X * FinePose.PerCell + 500, cell.Y * FinePose.PerCell + 500);
    }

    /// <summary>O ponto de ficar ao lado de <paramref name="partner"/>, do lado de onde <paramref name="npc"/> vem, a 0,9 m (em milésimos de célula).</summary>
    private static (long X, long Y) StandPoint(WorldState world, Npc npc, Npc partner)
    {
        var (px, py) = FineOf(world, partner);
        var (ax, ay) = FineOf(world, npc);
        double dx = ax - px, dy = ay - py, len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1) (dx, dy, len) = (1, 0, 1);
        double stand = 0.9 / Math.Max(0.1, world.Players.CellM) * FinePose.PerCell;
        long sx = px + (long)Math.Round(dx / len * stand), sy = py + (long)Math.Round(dy / len * stand);
        var cell = PlayerGround.PoseOf(world, partner);
        return (Math.Clamp(sx, (cell.X - 1) * FinePose.PerCell + 40, (cell.X + 2) * FinePose.PerCell - 40),
            Math.Clamp(sy, (cell.Y - 1) * FinePose.PerCell + 40, (cell.Y + 2) * FinePose.PerCell - 40));
    }

    /// <summary>Põe <paramref name="npc"/> ao lado de <paramref name="partner"/> na hora (quem ninguém conduz, ou sem passo fino): na mesma célula, dentro do prédio ou na rua.</summary>
    private static void PlaceBeside(WorldState world, Npc npc, Npc partner)
    {
        var cell = PlayerGround.PoseOf(world, partner);
        long hours = world.CurrentDate.TotalHours;
        if (npc.ControllerId is not null)
        {
            var (sx, sy) = StandPoint(world, npc, partner);
            npc.SetFine(new FinePose(sx, sy, 0, 0, world.CurrentDate.TotalMilliseconds));
            if (PlayerGround.PoseOf(world, npc) != cell && (int)Math.Floor(sx / (double)FinePose.PerCell) is var cx && (int)Math.Floor(sy / (double)FinePose.PerCell) is var cy)
                npc.MoveTo(new CellCoord(cx, cy), hours);
            return;
        }

        if (partner.Interior is { } inside)
        {
            if (npc.Interior is { } own && own.Building == inside.Building) npc.MoveWithinBuilding(inside.LocalCell);
            else npc.EnterBuilding(inside.Building, inside.Floor, inside.LocalCell);
        }
        else
        {
            if (npc.Interior is not null) npc.ExitBuilding();
            if (npc.CurrentLocation != cell) npc.MoveTo(cell, hours);
        }
    }

    /// <summary>Os dois estão ao lado: a ação começa (<see cref="BodyTask.Doing"/>), os corpos ficam ocupados até o fim e o fim é agendado.</summary>
    private static void Arrive(WorldState world, TickContext ctx, Npc actor, Npc target, SocialInteractionDef def, long now)
    {
        long until = now + def.DurationSec * SecondMs;
        actor.SetBodyTask(new BodyTask(Action, now, until) { Use = def.Id, Partner = target.Id.Value, Initiator = true, Doing = true, LastMs = now });
        actor.Step(until, 0, now);
        if (def.CompatibleWithUse && Sitting(world, target, now)) // #261: o alvo segue no móvel dele
        {
            ctx.ScheduleEventAtMs(until, BodyTaskSystem.SystemName, Payload(actor, now));
            return;
        }

        target.SetBodyTask(new BodyTask(Action, now, until) { Use = def.Id, Partner = actor.Id.Value, Doing = true, LastMs = now });
        target.Step(until, 0, now);
        ctx.ScheduleEventAtMs(until, BodyTaskSystem.SystemName, Payload(actor, now));
    }

    // ---------------- o evento ----------------

    /// <summary>O evento da interação, no corpo de quem começou: a caminhada a cada tick fino ou o fim. Evento de uma tarefa que já acabou é velho e sai.</summary>
    public static void Step(WorldState world, TickContext ctx, Npc actor, long startedAtMs)
    {
        if (actor.BodyTask is not { Action: Action, Use: { } id } task || task.StartedAtMs != startedAtMs || !task.Initiator) return;
        long now = world.CurrentDate.TotalMilliseconds;
        if (CatalogOf(world)?.Find(id) is not { } def || world.FindNpc(new NpcId(task.Partner)) is not { IsAlive: true } partner
            || !(partner.BodyTask is { Action: Action, Partner: var back } && back == actor.Id.Value
                || def.CompatibleWithUse && Sitting(world, partner, now))) // #261: o alvo que usa um móvel segue nele
        {
            Release(world, ctx, actor, now);
            return;
        }

        if (task.Doing)
        {
            if (now >= task.UntilMs) Complete(world, ctx, def, actor, partner, 1.0, now);
            else ctx.ScheduleEventAtMs(task.UntilMs, BodyTaskSystem.SystemName, Payload(actor, task.StartedAtMs));
            return;
        }

        Walk(world, ctx, def, actor, partner, task, now);
    }

    /// <summary>Quem chega ao fim e a decisão da hora vê o evento ainda por correr: assenta agora, para nada ser perdido.</summary>
    public static void SettleDue(WorldState world, TickContext ctx, Npc npc)
    {
        if (npc.BodyTask is not { Action: Action, Doing: true } task || world.CurrentDate.TotalMilliseconds < task.UntilMs) return;
        var actor = task.Initiator ? npc : world.FindNpc(new NpcId(task.Partner));
        if (actor?.BodyTask is { Action: Action, Initiator: true } own) Step(world, ctx, actor, own.StartedAtMs);
    }

    private static void Walk(WorldState world, TickContext ctx, SocialInteractionDef def, Npc actor, Npc partner, BodyTask task, long now)
    {
        var cell = PlayerGround.PoseOf(world, actor);
        var (px, py) = FineOf(world, partner);
        var (ax, ay) = FineOf(world, actor);
        double dx = ax - px, dy = ay - py;
        double perM = FinePose.PerCell / Math.Max(0.1, world.Players.CellM);
        double reach = (CatalogOf(world)?.Rules.ReachM ?? 1.4) * perM, close = 0.55 * perM; // ao lado: nem longe demais, nem em cima do outro
        bool tooClose = dx * dx + dy * dy < close * close;
        if (!tooClose && dx * dx + dy * dy <= reach * reach)
        {
            actor.SetFine(new FinePose(ax, ay, 0, 0, now));
            Arrive(world, ctx, actor, partner, def, now);
            return;
        }

        var (sx, sy) = StandPoint(world, actor, partner);
        var pose = actor.Fine is { } f && f.X / FinePose.PerCell == cell.X && f.Y / FinePose.PerCell == cell.Y ? f : new FinePose(ax, ay, 0, 0, now);
        long elapsed = Math.Clamp(now - Math.Max(task.LastMs, pose.AtMs), 0, 4L * Math.Max(1, world.TickDeltaMs));
        var moved = PlayerWalk.Approach(world, ctx, actor, pose, sx, sy, elapsed);
        actor.SetFine(moved.Pose with { AtMs = now, HeadingX = moved.Arrived ? 0 : moved.Pose.HeadingX, HeadingY = moved.Arrived ? 0 : moved.Pose.HeadingY });
        if (moved.Arrived || (moved.Blocked && tooClose)) // sem lugar para se afastar (parede): faz ali mesmo
        {
            Arrive(world, ctx, actor, partner, def, now);
            return;
        }

        if (moved.Blocked && now - task.StartedAtMs > GiveUpMs / 4 || now - task.StartedAtMs > GiveUpMs)
        {
            actor.SetFine(moved.Pose with { HeadingX = 0, HeadingY = 0, AtMs = now });
            Release(world, ctx, actor, now);
            return;
        }

        actor.SetBodyTask(task with { LastMs = now });
        ctx.ScheduleEventAtMs(now + Math.Max(1, world.TickDeltaMs), BodyTaskSystem.SystemName, Payload(actor, task.StartedAtMs));
    }

    private static void Complete(WorldState world, TickContext ctx, SocialInteractionDef def, Npc actor, Npc partner, double fraction, long now)
    {
        ApplyEffects(world, def.Effects, actor, partner, fraction, now, def.Id);
        MarkPair(world, def, actor, partner, now, accepted: true);
        MoodMechanic.OnSocial(world, def, actor, partner, accepted: true, now); // #262
        Release(world, ctx, actor, now);
    }

    /// <summary>Solta os dois corpos (a tarefa some e o passo volta a andar); quem ninguém conduz acorda para decidir.</summary>
    private static void Release(WorldState world, TickContext ctx, Npc actor, long now)
    {
        long partnerId = actor.BodyTask?.Partner ?? 0;
        Free(world, actor, now);
        if (partnerId != 0 && world.FindNpc(new NpcId(partnerId)) is { } partner && partner.BodyTask is { Action: Action }) Free(world, partner, now);
    }

    private static void Free(WorldState world, Npc npc, long now)
    {
        npc.SetBodyTask(null);
        npc.Step(now, 0, now);
        if (npc.Fine is { } pose) npc.SetFine(pose with { HeadingX = 0, HeadingY = 0, AtMs = now });
        if (npc.ControllerId is null) PlayerCommands.WakeNow(world, npc);
    }

    /// <summary>Interrompe (o jogador mexeu, apertou Espaço) a interação de <paramref name="npc"/>, de qualquer dos dois lados: vale a fração vivida (menos de um quarto, nada).</summary>
    public static BodyTask? Stop(WorldState world, TickContext ctx, Npc npc)
    {
        if (npc.BodyTask is not { Action: Action, Use: { } id } task) return null;
        long now = world.CurrentDate.TotalMilliseconds;
        var actor = task.Initiator ? npc : world.FindNpc(new NpcId(task.Partner));
        var partner = task.Initiator ? world.FindNpc(new NpcId(task.Partner)) : npc;
        if (actor is not null && partner is not null && task.Doing && CatalogOf(world)?.Find(id) is { } def)
        {
            double fraction = Math.Clamp((now - task.StartedAtMs) / (double)Math.Max(1, task.UntilMs - task.StartedAtMs), 0, 1);
            if (fraction >= 0.25)
            {
                ApplyEffects(world, def.Effects, actor, partner, fraction, now, def.Id);
                MarkPair(world, def, actor, partner, now, accepted: true);
                MoodMechanic.OnSocial(world, def, actor, partner, accepted: true, now); // #262
            }
        }

        Release(world, ctx, actor ?? npc, now);
        if (npc.BodyTask is not null) Free(world, npc, now);
        return task;
    }

    /// <summary>A leitura do corpo ocupado para o cliente: a interação, a fase, o gesto do papel dele, e o outro. Nula se não é interação social.</summary>
    public static SocialView? ViewOf(WorldState world, BodyTask task)
    {
        if (task.Action != Action || task.Use is not { } id || CatalogOf(world)?.Find(id) is not { } def) return null;
        return new SocialView(id, def.Name, task.Doing ? "do" : "walk", task.Initiator ? def.Pose : def.TargetPose, task.Partner, task.Initiator, task.Initiator ? def.TargetPose : def.Pose);
    }

    // ---------------- efeitos ----------------

    /// <summary>Aplica os efeitos dos dois lados na fração <paramref name="fraction"/> (1 = por inteiro): laços (o que cada um sente pelo outro), necessidades, emoções e a marca na memória.</summary>
    public static void ApplyEffects(WorldState world, SocialEffects effects, Npc actor, Npc target, double fraction, long nowMs, string id)
    {
        ApplySide(world, effects.Actor, actor, target, fraction, nowMs);
        ApplySide(world, effects.Target, target, actor, fraction, nowMs);
        // #258: o estado de cada um (higiene baixa) pesa no laço de quem convive com ele, também entre NPCs
        HomeLife.WeighBond(world, actor, target, nowMs);
        HomeLife.WeighBond(world, target, actor, nowMs);
    }

    private static void ApplySide(WorldState world, SocialSide side, Npc who, Npc other, double fraction, long nowMs)
    {
        long hours = nowMs / WorldMillis.PerHour;
        if (side.Affection != 0 || side.Trust != 0 || side.Respect != 0)
        {
            var rel = Ensure(world, who, other, hours);
            if (side.Affection != 0) rel.Shift(RelationshipAxis.Affection, side.Affection * fraction);
            if (side.Trust != 0) rel.Shift(RelationshipAxis.Trust, side.Trust * fraction);
            if (side.Respect != 0) rel.Shift(RelationshipAxis.Respect, side.Respect * fraction);
        }

        if (side.Needs is { } needs)
            foreach (var shift in needs)
                HomeLife.Add(world, who, shift.Need, shift.Amount * fraction, nowMs);
        if (side.Mood is { Count: > 0 } || side.Salience > 0)
        {
            double Get(string key) => side.Mood is { } m && m.TryGetValue(key, out var v) ? v : 0;
            var impact = new LifeEventImpact(side.Salience, Grief: Get("grief"), Fear: Get("fear"), Anger: Get("anger"), Joy: Get("joy"), Shame: Get("shame"));
            PsycheMechanic.Experience(world, who, LifeEventKind.Social, other.Id.Value, hours, fraction, impact);
        }
    }

    /// <summary>Registra no par (os dois lados) a última interação, o contato e a quem coube começar.</summary>
    private static void MarkPair(WorldState world, SocialInteractionDef def, Npc actor, Npc target, long nowMs, bool accepted)
    {
        long hours = nowMs / WorldMillis.PerHour;
        var a = Ensure(world, actor, target, hours);
        var b = Ensure(world, target, actor, hours);
        a.NoteSocial(def.Id, nowMs, accepted, actor: true);
        b.NoteSocial(def.Id, nowMs, accepted, actor: false);
        if (!accepted) return;
        a.MarkContact(hours);
        b.MarkContact(hours);
    }

    // ---------------- atividade conjunta ----------------

    /// <summary>As duas peças da casa para a atividade: a do convite e a do convidado (índices da mobília); nulo se faltam ou estão ocupadas.</summary>
    private static (long Building, int PieceA, int PieceB)? FindJoint(WorldState world, SocialCatalog catalog, SocialJoint joint, Npc actor, Npc target)
    {
        if (world.ObjectCatalog is not { } objects || PlayerGround.InsideBuilding(world, actor) is not { } id || PlayerGround.InsideBuilding(world, target) != id) return null;
        if (world.FindBuilding(new BuildingId(id)) is not { Position: not null } building) return null;
        var pieces = HomeFurniture.Of(building, objects);
        int a = Pick(world, objects, pieces, id, joint.Use, -1, actor, target);
        if (a < 0) return null;
        int b = Pick(world, objects, pieces, id, joint.PartnerUse, a, actor, target);
        return b < 0 ? null : (id, a, b);
    }

    private static int Pick(WorldState world, ObjectCatalog objects, IReadOnlyList<HomePiece> pieces, long building, string use, int notPiece, Npc actor, Npc target)
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i].Spot is null || objects.Find(pieces[i].Id) is not { Uses: { } uses } || uses.FirstOrDefault(u => u.Id == use) is not { } def) continue;
            if (i == notPiece && def.Occupies) continue;
            if (def.Occupies && (ObjectUsage.OccupiedBy(world, building, i, actor) is { } who && who.Id != target.Id || ObjectUsage.OccupiedBy(world, building, i, target) is { } other && other.Id != actor.Id)) continue;
            return i;
        }

        return -1;
    }

    private static Result<SocialStart> StartJoint(WorldState world, TickContext ctx, SocialCatalog catalog, SocialInteractionDef def, SocialJoint joint, Npc actor, Npc target, long now)
    {
        if (FindJoint(world, catalog, joint, actor, target) is not { } place) return Result<SocialStart>.Fail($"{PlayerCommands.Conflict} social: {Text(catalog, "noPlace", actor, target, "não há onde")}");
        var first = ObjectUsage.Begin(world, ctx, actor, place.Building, place.PieceA, joint.Use);
        if (!first.IsSuccess) return Result<SocialStart>.Fail(first.Error!);
        var second = ObjectUsage.Begin(world, ctx, target, place.Building, place.PieceB, joint.PartnerUse);
        if (!second.IsSuccess)
        {
            ObjectUsage.Stop(world, ctx, actor);
            return Result<SocialStart>.Fail(second.Error!);
        }

        ApplyEffects(world, def.Effects, actor, target, 1.0, now, def.Id); // os laços e o humor da atividade valem ao começar; as necessidades do objeto sobem pelo uso, no tempo dele
        MarkPair(world, def, actor, target, now, accepted: true);
        MoodMechanic.OnSocial(world, def, actor, target, accepted: true, now); // #262
        return Result<SocialStart>.Ok(new SocialStart(true, Format(def.Name, actor.Name, target.Name, 0, null), null));
    }
}
