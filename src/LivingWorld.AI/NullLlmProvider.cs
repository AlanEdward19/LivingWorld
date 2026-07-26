namespace LivingWorld.AI;

/// <summary>Fallback quando não há provider real (ADR-0004): sempre o mesmo resultado neutro,
/// nunca falha, nunca chama rede. Degrada a experiência, nunca trava a simulação.</summary>
public sealed class NullLlmProvider : ILlmProvider
{
    public Task<LlmResponse> GetResponseAsync(LlmContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(new LlmResponse(
            Dialogue: "...",
            Emotion: "neutral",
            Intent: "none",
            ProposedActions: [],
            MemoryCandidates: []));
}
