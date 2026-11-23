using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Players;

/// <summary>O que a pessoa presa vê e sente agora (AD-197e), só para quem a possui: cópia de valores calculados pelo motor (psique,
/// vínculos, necessidades). É o que a fronteira da LLM pode pôr em palavras (fala e pensamento), nunca o contrário.</summary>
public sealed record CaptiveView(
    long NpcId, long SinceTick, int Objections, int Distress, long LastObjectionTarget, string? LastObjectionTargetName,
    double LastObjectionCare, double Grief, double Fear, double Anger, double Joy, double Shame, string Wants);

/// <summary>A vontade da pessoa possuída (AD-197e). Uma regra só para os dois modos e para quem for o alvo da posse, inclusive um
/// personagem de jogador no futuro: o quanto ela se importa com alguém é o vínculo (parente: média entre o parentesco e o laço
/// vivido, como no sumiço da AD-196; fora da família, o laço) vezes a lealdade dela. A partir de
/// <see cref="PlayerRules.RefuseHarmCare"/> ela não fere essa pessoa: na posse comum recusa a ordem; na plena obedece presa e sofre.
/// Sem sorteio: a recusa é causal e se repete igual no replay.</summary>
public static class PossessionWill
{
    /// <summary>Cuidado (0–100) de <paramref name="npc"/> por <paramref name="other"/>.</summary>
    public static double CareFor(WorldState world, Npc npc, Npc other)
    {
        bool kin = npc.Spouse == other.Id || other.Spouse == npc.Id || npc.MotherId == other.Id || npc.FatherId == other.Id
            || other.MotherId == npc.Id || other.FatherId == npc.Id
            || (npc.Household is { } home && other.Household == home);
        double? tie = world.Relationships.TryGetValue(new RelationshipKey(npc.Id, other.Id), out var bond)
            ? Math.Clamp((bond.Trust + bond.Affection) / 2, 0, 100)
            : null;
        double strength = kin ? (tie is { } t ? (100 + t) / 2 : 100) : tie ?? 0;
        return Math.Clamp(strength * (0.5 + npc.Personality.Loyalty / 200.0), 0, 100);
    }

    /// <summary>Fim da posse plena: a pessoa volta a si e vive o que passou presa (lembrança e emoção na medida do sofrimento, com
    /// piso: ter o corpo tomado já marca). O luto e o medo recolhem a rotina depois (AD-138).</summary>
    public static void Release(WorldState world, Npc npc, long tick)
    {
        if (npc.Captive is not { } mind) return;
        if (world.Players.Enabled)
            PsycheMechanic.Experience(world, npc, LifeEventKind.Possessed, mind.LastObjectionTarget >= 0 ? mind.LastObjectionTarget : 0,
                tick, 0.3 + 0.7 * mind.Distress / 100.0, world.Players.Possessed);
        npc.SetPossession(PossessionMode.Common, tick);
    }

    /// <summary>O estado da pessoa presa, ou nulo fora da posse plena.</summary>
    public static CaptiveView? View(WorldState world, Npc npc, long tick)
    {
        if (npc.Captive is not { } mind) return null;
        var affect = PsycheMechanic.AffectOf(world, npc, tick);
        var target = mind.LastObjectionTarget >= 0 ? world.FindNpc(new LivingWorld.Domain.Shared.NpcId(mind.LastObjectionTarget)) : null;
        var needs = new (string Need, int Level)[]
        {
            ("comer", npc.HungerAt(tick)), ("beber", npc.ThirstAt(tick)), ("dormir", npc.SleepAt(tick)), ("companhia", npc.SocialAt(tick)),
        };
        var lowest = needs.OrderBy(n => n.Level).First();
        string wants = lowest.Level < 50 ? lowest.Need : target is not null && mind.Distress > 0 ? "parar" : "voltar a si";
        return new CaptiveView(npc.Id.Value, mind.SinceTick, mind.Objections, mind.Distress, mind.LastObjectionTarget, target?.Name,
            target is null ? 0 : CareFor(world, npc, target), affect.Grief, affect.Fear, affect.Anger, affect.Joy, affect.Shame, wants);
    }
}
