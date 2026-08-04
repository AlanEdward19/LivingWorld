using LivingWorld.Domain;
using LivingWorld.Domain.Narrative;
using LivingWorld.Domain.Llm;

namespace LivingWorld.Simulation.Narrative;

/// <summary>Renderiza um <see cref="NarrativeDraft"/> em <see cref="NarrativeDocument"/> (Fase 12,
/// NARR-08/NARR-12) — template determinístico é o caminho padrão e o fallback; a LLM (opcional,
/// <c>rules/llm-boundary.md</c>) só reescreve a prosa dos claims já aprovados, nunca decide quais
/// claims existem. Mesmo espírito de <see cref="LivingWorld.Simulation.Llm.ConversationOrchestrator"/>:
/// provider indisponível, erro, ou saída que introduz numeral/nome órfão (<see
/// cref="ClaimAnchorValidator.ValidateProse"/>) sempre caem no mesmo template — a narrativa nunca
/// trava esperando a LLM nem publica prosa sem ancoragem.</summary>
public static class NarrativeRenderer
{
    public static async Task<NarrativeDocument> RenderAsync(
        NarrativeId id, NarrativeType type, NarrativeDraft draft,
        ILlmProvider? llmProvider = null, CancellationToken cancellationToken = default)
    {
        var outcome = ClaimAnchorValidator.ValidateClaims(draft.Claims);
        string templateProse = RenderTemplate(outcome.Approved);

        if (llmProvider is null)
            return new NarrativeDocument(id, type, templateProse, outcome.Approved);

        string? llmProse;
        try
        {
            var context = new LlmContext(
                NpcKnowledgeSummary: string.Join(" ", outcome.Approved.Select(c => c.Text)),
                PlayerUtterance: "narrar",
                AllowedIntents: []);
            var response = await llmProvider.GetResponseAsync(context, cancellationToken);
            llmProse = response.Dialogue;
        }
        catch (OperationCanceledException)
        {
            llmProse = null;
        }
        catch (Exception)
        {
            llmProse = null;
        }

        bool anchored = llmProse is not null
            && ClaimAnchorValidator.ValidateProse(llmProse, outcome.Approved).IsSuccess;

        return anchored
            ? new NarrativeDocument(id, type, llmProse!, outcome.Approved)
            : new NarrativeDocument(id, type, templateProse, outcome.Approved);
    }

    /// <summary>Concatena o texto dos claims aprovados, na ordem recebida — mesma entrada, mesma
    /// saída (NARR-08). Sem claims aprovados, usa um aviso fixo sem numeral/nome próprio, para
    /// nunca reprovar a própria ancoragem que ele mesmo deveria satisfazer.</summary>
    private static string RenderTemplate(IReadOnlyList<NarrativeClaim> approvedClaims) =>
        approvedClaims.Count == 0
            ? "sem registros ancorados para este período."
            : string.Join(" ", approvedClaims.Select(c => c.Text));
}
