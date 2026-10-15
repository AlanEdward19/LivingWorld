using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Behavior.Decision;

/// <summary>Estímulos sintéticos que o sandbox sobrepõe ao contexto real do NPC. Tudo opcional: o que
/// vier nulo fica como o NPC tem agora. Necessidades usam a mesma escala da ficha (0–100, alto = satisfeito).</summary>
public sealed record NpcSandboxOverrides(
    int? Hunger = null, int? Thirst = null, int? Sleep = null, int? Social = null,
    IReadOnlyDictionary<string, int>? Personality = null,
    ActionType? ContinuityAction = null);

/// <summary>"E se…?" de um NPC real (Fase 28 task 12): monta o <see cref="DecisionContext"/> verdadeiro do NPC,
/// aplica os overrides e roda o mesmo <see cref="DecisionSandbox"/>. Leitura pura: nenhuma escrita no mundo.</summary>
public static class NpcDecisionSandbox
{
    private static readonly string[] Traits =
    [
        "extroversion", "agreeableness", "conscientiousness", "emotionalStability", "openness",
        "ambition", "loyalty", "altruism", "impulsivity", "riskAversion",
    ];

    public static Result<DecisionSandboxResult> Run(WorldState world, NpcId id, NpcSandboxOverrides overrides)
    {
        var npc = world.FindNpc(id);
        if (npc is null || !npc.IsAlive)
            return Result<DecisionSandboxResult>.Fail("NpcId: NPC ausente, morto ou ainda não materializado");

        var personality = ApplyPersonality(npc.Personality, overrides.Personality);
        if (!personality.IsSuccess)
            return Result<DecisionSandboxResult>.Fail(personality.Error!);
        foreach (var (name, value) in new[] { ("hunger", overrides.Hunger), ("thirst", overrides.Thirst), ("sleep", overrides.Sleep), ("social", overrides.Social) })
            if (value is < 0 or > 100)
                return Result<DecisionSandboxResult>.Fail($"needs.{name}: fora de [0,100]");

        var tick = world.CurrentDate.TotalHours;
        var context = DecisionContextBuilder.Build(world, npc, tick);
        context = context with
        {
            Needs = new NeedsSnapshot(
                overrides.Hunger ?? context.Needs.Hunger, overrides.Thirst ?? context.Needs.Thirst,
                overrides.Sleep ?? context.Needs.Sleep, overrides.Social ?? context.Needs.Social),
            Personality = personality.Value!,
        };

        var request = new DecisionSandboxRequest(
            ContinuityAction: overrides.ContinuityAction ?? npc.CurrentAction, PreviousIntent: npc.CurrentIntent);
        return Result<DecisionSandboxResult>.Ok(
            DecisionSandbox.Decide(context, world.NeedsRules, world.EconomyRules, request));
    }

    private static Result<Personality> ApplyPersonality(Personality current, IReadOnlyDictionary<string, int>? values)
    {
        if (values is null || values.Count == 0)
            return Result<Personality>.Ok(current);
        foreach (var key in values.Keys)
            if (!Traits.Contains(key))
                return Result<Personality>.Fail($"personality.{key}: traço desconhecido");

        int Pick(string trait, int fallback) => values.TryGetValue(trait, out var v) ? v : fallback;
        return Personality.Create(
            Pick("extroversion", current.Extroversion), Pick("agreeableness", current.Agreeableness),
            Pick("conscientiousness", current.Conscientiousness), Pick("emotionalStability", current.EmotionalStability),
            Pick("openness", current.Openness), Pick("ambition", current.Ambition), Pick("loyalty", current.Loyalty),
            Pick("altruism", current.Altruism), Pick("impulsivity", current.Impulsivity), Pick("riskAversion", current.RiskAversion));
    }
}
