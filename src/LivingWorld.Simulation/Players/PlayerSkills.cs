using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Skills;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Simulation.Players;

/// <summary>Uma habilidade do corpo como o jogador a vê: pontos, nível, o que falta para o próximo (<c>ToNext</c>, em pontos; 0 no teto),
/// a fração já andada no nível, se ela sobe agora (<c>Practicing</c>) e quanto cada prática rende (<c>GainPerPractice</c>).</summary>
public sealed record SkillProgress(int SkillId, double Points, double Cap, int Level, int MaxLevel, double ToNext, double Fraction,
    bool Practicing, double GainPerPractice);

/// <summary>Progresso das habilidades (#235). O ganho é o do motor (<see cref="SkillPracticeSystem"/>): quem trabalha no próprio
/// posto, seja NPC ou jogador, sobe pelo mesmo caminho; aqui só se lê. Níveis são faixas iguais de <c>Cap / MaxLevel</c> pontos.</summary>
public static class PlayerSkills
{
    public const int MaxLevel = 10;

    public static IReadOnlyList<SkillProgress> Of(WorldState world, Npc npc, SkillsRules? rules = null)
    {
        rules ??= ScenarioRunner.DefaultSkillsRules;
        SkillType? own = rules.SkillByProfession.TryGetValue(npc.Profession.Id, out var mine) ? mine : null;
        var ids = new SortedSet<int>(npc.Skills.Values.Where(kv => kv.Value > 0).Select(kv => kv.Key));
        if (own is { } skill) ids.Add(skill.Id);
        return ids.Select(id => One(world, npc, rules, new SkillType(id), own)).ToList();
    }

    /// <summary>A habilidade do ofício do corpo (a que o trabalho dele pratica), ou nula sem ofício mapeado.</summary>
    public static SkillProgress? Practiced(WorldState world, Npc npc, SkillsRules? rules = null)
    {
        rules ??= ScenarioRunner.DefaultSkillsRules;
        return rules.SkillByProfession.TryGetValue(npc.Profession.Id, out var s) ? One(world, npc, rules, s, s) : null;
    }

    private static SkillProgress One(WorldState world, Npc npc, SkillsRules rules, SkillType skill, SkillType? own)
    {
        double points = npc.Skills.Get(skill), width = rules.Cap / MaxLevel;
        int level = Math.Min(MaxLevel, (int)(points / width));
        bool atCap = points >= rules.Cap;
        double into = points - level * width;
        bool practicing = own == skill && npc.CurrentAction == ActionType.Work && npc.Employer is { } e
            && world.FindWorkplace(e) is { } w && npc.CurrentLocation == w.Location; // a mesma condição do SkillPracticeSystem
        return new SkillProgress(skill.Id, Math.Round(points, 2), rules.Cap, level, MaxLevel,
            atCap ? 0 : Math.Round(width - into, 2), atCap ? 1 : Math.Round(into / width, 3), practicing,
            own == skill ? SkillPracticeSystem.PracticeGain(world, rules, npc, skill) : 0);
    }
}
