using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.History;

/// <summary>Limites de proveniência causal (COH-02) — cenário-driven, mesmo padrão de
/// <see cref="NeedsRules.MaxActionSelectionSteps"/>.</summary>
public sealed record CausalRules(int MaxCauseChainDepth)
{
    public static Result<CausalRules> Create(int maxCauseChainDepth)
    {
        if (maxCauseChainDepth <= 0)
            return Result<CausalRules>.Fail("MaxCauseChainDepth: deve ser positivo");

        return Result<CausalRules>.Ok(new CausalRules(maxCauseChainDepth));
    }

    /// <summary>Profundidade máxima padrão da cadeia CauseEventId antes de abortar
    /// (doc#81 / COH-02).</summary>
    public static CausalRules Default { get; } = Create(64).Value!;
}
