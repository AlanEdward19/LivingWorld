using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Objects;

public static partial class SocialInteractions
{
    /// <summary>Quem está ao alcance de uma conversa de <paramref name="npc"/>: na mesma cidade, no mesmo andar e, dentro de um prédio, no mesmo prédio; a até 3 células.</summary>
    private static List<Npc> Around(WorldState world, Npc npc)
    {
        var at = PlayerGround.PoseOf(world, npc);
        var near = new List<Npc>();
        foreach (var o in world.AliveNpcIndex.Alive)
        {
            if (o.Id == npc.Id || o.City != npc.City || !PlayerGround.SameLevel(npc, o)) continue;
            if ((o.Interior is null) != (npc.Interior is null) || (npc.Interior is { } mine && o.Interior!.Building != mine.Building)) continue;
            var there = PlayerGround.PoseOf(world, o);
            if (Math.Max(Math.Abs(there.X - at.X), Math.Abs(there.Y - at.Y)) <= 3) near.Add(o);
        }

        near.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
        return near;
    }

    /// <summary>#259: quem ninguém conduz procura companhia: com a necessidade do catálogo (<see cref="SocialRules.InitiateNeed"/>) abaixo do limite, à toa, escolhe entre as
    /// pessoas ao alcance e as interações com <see cref="SocialInteractionDef.Initiative"/> a de maior utilidade (peso × afeto que sente × falta de companhia + um
    /// desempate do RNG do mundo) e a pede pelo mesmo <see cref="Start"/> do jogador. Pausa entre uma e outra no par (<see cref="SocialRules.CooldownMin"/>).
    /// Verdadeiro se começou uma (aceita ou recusada).</summary>
    public static bool TryInitiate(WorldState world, TickContext ctx, Npc npc)
    {
        if (CatalogOf(world) is not { } catalog || npc.ControllerId is not null || !npc.IsAlive) return false;
        long now = world.CurrentDate.TotalMilliseconds;
        SettleDue(world, ctx, npc);
        if (Busy(world, npc, now) || npc.CurrentAction == ActionType.Sleep || !Adult(world, catalog, npc)) return false;
        var rules = catalog.Rules;
        if ((HomeLife.Value(world, npc, rules.InitiateNeed, now) ?? 100) >= rules.InitiateBelow) return false;
        var candidates = Around(world, npc);
        if (candidates.Count == 0) return false;

        var rng = ctx.OneShot($"social-pick-{npc.Id.Value}-{now}");
        SocialInteractionDef? best = null;
        Npc? bestWith = null;
        double bestScore = 0;
        foreach (var other in candidates)
        {
            var mine = Rel(world, npc, other);
            if (mine?.LastSocial is not null && now - mine.LastSocialMs < rules.CooldownMin * 60_000L) continue;
            double affection = mine?.Affection ?? 0;
            foreach (var def in catalog.Interactions)
            {
                if (def.Initiative is not { } init || (other.ControllerId is not null && !init.ToPlayer) || affection < init.AffectionMin) continue;
                double below = init.Below ?? rules.InitiateBelow, level = HomeLife.Value(world, npc, init.Need ?? rules.InitiateNeed, now) ?? below;
                if (level >= below || Unavailable(world, catalog, def, npc, other, now) is not null) continue;
                double score = init.Weight * (1 + affection / 100.0) * (1 + (below - level) / Math.Max(1, below)) * MoodMechanic.SocialBias(world, npc, def.Category, now) + rng.NextDouble() * 0.25; // #262: o humor muda o que ele puxa
                if (score > bestScore) (bestScore, best, bestWith) = (score, def, other);
            }
        }

        return best is not null && bestWith is not null && Start(world, ctx, npc, bestWith, best.Id).IsSuccess;
    }

    /// <summary>A pausa até a próxima sondagem de iniciativa (ms), para o despertar de quem ninguém conduz; <see cref="long.MaxValue"/> sem catálogo social.</summary>
    public static long NextCheckMs(WorldState world, Npc npc, long nowMs) =>
        CatalogOf(world) is { } catalog && npc.ControllerId is null ? nowMs + catalog.Rules.CheckEveryMin * 60_000L : long.MaxValue;

    /// <summary>O que aconteceu por último a <paramref name="npc"/> que ele deva ver: a interação mais recente de um par com ele dentro de <paramref name="withinMs"/>, em texto
    /// do catálogo e o instante, para o aviso curto do jogador (a que ele mesmo pediu inclusive: o efeito só chega ao fim).</summary>
    public static SocialEvent? LastFor(WorldState world, Npc npc, long withinMs)
    {
        if (CatalogOf(world) is not { } catalog) return null;
        long now = world.CurrentDate.TotalMilliseconds;
        SocialEvent? best = null;
        var targets = world.RelationshipTargetsOf(npc.Id);
        for (int i = 0; i < targets.Count; i++)
        {
            if (world.Relationships.TryGetValue(new LivingWorld.Domain.Population.Family.RelationshipKey(npc.Id, targets[i]), out var mine) is false || mine.LastSocial is null
                || now - mine.LastSocialMs > withinMs || world.FindNpc(targets[i]) is not { IsAlive: true } other) continue;
            if (best is { } b && b.AtMs >= mine.LastSocialMs) continue;
            if (catalog.Find(mine.LastSocial) is not { } def || LastText(world, catalog, npc, other, mine) is not { } text) continue;
            best = new SocialEvent(text, mine.LastSocialMs, other.Id.Value, Delta(def, mine.LastSocialAccepted), mine.LastSocialAccepted);
        }

        return best;
    }
}

/// <summary>#259: a última interação social de um corpo, para o aviso do jogador: o texto do catálogo, quando (ms de mundo), com quem, o ganho de afeto e se foi aceita.</summary>
public sealed record SocialEvent(string Text, long AtMs, long With, int Delta, bool Accepted);
