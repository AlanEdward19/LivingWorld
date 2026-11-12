using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Family;

namespace LivingWorld.Simulation.Cognition;

/// <summary>Para quem a vida interior inclina a conversa (AD-138): enlutado procura os seus, quem tem medo evita estranhos, e quem
/// guarda mágoa de alguém (traição, furto, violência, abandono) o evita. Lido na escolha de com quem conversar
/// (<c>Socialize</c>) e nos encontros do dia (<see cref="RelationshipSystem"/>). Sem psique: inerte e sem custo.</summary>
public readonly struct SocialLeaning
{
    private readonly double _grief;
    private readonly double _fear;
    private readonly bool _remembers;
    private readonly PsycheRules? _rules;

    private SocialLeaning(PsycheRules rules, double grief, double fear, bool remembers)
    {
        _rules = rules;
        _grief = grief;
        _fear = fear;
        _remembers = remembers;
    }

    public bool IsInert => _rules is null;

    public static SocialLeaning Of(WorldState world, Npc npc, long tick)
    {
        var rules = world.NeedsRules.Psyche;
        if (!rules.Enabled || npc.Psyche is not { } psyche) return default;
        bool feeling = tick < psyche.CalmFromTick;
        double grief = feeling && psyche.Grief > 0 ? PsycheMechanic.EmotionOf(world, npc, Emotion.Grief, tick) : 0;
        double fear = feeling && psyche.Fear > 0 ? PsycheMechanic.EmotionOf(world, npc, Emotion.Fear, tick) : 0;
        bool remembers = psyche.Memories.Count > 0;
        if (grief <= 0 && fear <= 0 && !remembers) return default;
        return new SocialLeaning(rules, grief, fear, remembers);
    }

    /// <summary>Apelo a somar ao de conversar com <paramref name="other"/> (pode ser negativo).</summary>
    public double AppealToward(WorldState world, Npc self, Npc other, double affection, long tick)
    {
        if (_rules is null) return 0;
        double appeal = 0;
        if (_grief > 0 && RelationshipSystem.IsKin(self, other)) appeal += _rules.KinSeekingAppeal * _grief;
        if (_fear > 0 && affection <= 0) appeal -= _rules.FearStrangerPenalty * _fear;
        if (_remembers) appeal -= _rules.GrudgeAppealPenalty * PsycheMechanic.GrudgeAgainst(world, self, other.Id.Value, tick);
        return appeal;
    }
}
