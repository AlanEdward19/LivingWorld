using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Llm;

/// <summary>Pipeline completo de um turno de conversa (Fase 11, LLM-09/10/11):
/// <see cref="ConversationSessionStore"/> (registra o turno) -> <see cref="LlmContextAssembler"/>
/// (crença) -> <see cref="ILlmProvider"/> -> <see cref="LlmResponseValidator"/> -> válido aplica
/// <see cref="ConversationEffectsApplier"/>, inválido usa <see cref="FallbackResponder"/>. Provider
/// indisponível, erro ou orçamento por interação excedido (<paramref name="budgetPerInteraction"/>
/// do construtor, via cancelamento do <see cref="CancellationTokenSource"/>) sempre caem no mesmo
/// fallback — o tick nunca trava esperando a LLM (rules/llm-boundary.md).</summary>
public sealed class ConversationOrchestrator
{
    private readonly ConversationSessionStore _sessions;
    private readonly ConversationEffectsApplier _effects;
    private readonly ILlmProvider _provider;
    private readonly IReadOnlyList<string> _knownEmotions;
    private readonly TimeSpan _budgetPerInteraction;

    public ConversationOrchestrator(
        ConversationSessionStore sessions, ConversationEffectsApplier effects, ILlmProvider provider,
        IReadOnlyList<string> knownEmotions, TimeSpan budgetPerInteraction)
    {
        _sessions = sessions;
        _effects = effects;
        _provider = provider;
        _knownEmotions = knownEmotions;
        _budgetPerInteraction = budgetPerInteraction;
    }

    public async Task<ValidatedLlmTurn> SendMessageAsync(
        WorldState world, Npc npc, ConversationSession session, string playerUtterance,
        IReadOnlyList<string> allowedIntents, IReadOnlyList<string> allowedActions, TickContext ctx)
    {
        if (_sessions.SendPlayerMessage(session.SessionId, playerUtterance, ctx) != SendMessageResult.Ok)
            return FallbackResponder.Respond(npc);

        var context = LlmContextAssembler.Assemble(world, npc, session, playerUtterance, allowedIntents, allowedActions);

        LlmResponse response;
        using (var budget = new CancellationTokenSource(_budgetPerInteraction))
        {
            try
            {
                response = await _provider.GetResponseAsync(context, budget.Token);
            }
            catch (OperationCanceledException)
            {
                // Orçamento por interação excedido (LLM-10 AC2): a chamada externa foi
                // interrompida, nunca esperada até o fim.
                return FallbackResponder.Respond(npc);
            }
            catch (Exception)
            {
                // Provider indisponível/erro (LLM-11 AC1) — degrada, nunca trava o tick.
                return FallbackResponder.Respond(npc);
            }
        }

        var validation = LlmResponseValidator.Validate(response, context, _knownEmotions);
        if (!validation.IsValid)
            return FallbackResponder.Respond(npc);

        _effects.Apply(world, npc, ctx.CurrentTick, validation.Turn!);
        return validation.Turn!;
    }
}
