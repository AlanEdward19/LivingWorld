using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Narrative;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Narrative;

namespace LivingWorld.Tests.Unit.Narrative;

/// <summary>Fase 12, T4: <see cref="NarrativeRenderer"/> (NARR-08, NARR-12) — template
/// determinístico é o caminho padrão/fallback; LLM opcional só reescreve a prosa dos claims já
/// aprovados, nunca altera a estrutura publicada.</summary>
public class NarrativeRendererTests
{
    /// <summary>Provider controlável para os cenários do gate — mesmo padrão de
    /// <c>ConversationOrchestratorTests.ScriptedProvider</c>, nunca chama rede de verdade.</summary>
    private sealed class ScriptedProvider : ILlmProvider
    {
        private readonly Func<LlmContext, CancellationToken, Task<LlmResponse>> _behavior;
        public ScriptedProvider(Func<LlmContext, CancellationToken, Task<LlmResponse>> behavior) => _behavior = behavior;
        public Task<LlmResponse> GetResponseAsync(LlmContext context, CancellationToken cancellationToken = default) =>
            _behavior(context, cancellationToken);
    }

    private static NarrativeDraft MakeDraft() => new(
        Location: new CityId(Guid.NewGuid()), PeriodStartTick: 0, PeriodEndTick: 100,
        Claims: [new NarrativeClaim("A vila sofreu uma seca de 3 anos.", [1, 2])]);

    [Fact]
    public async Task Render_without_llm_provider_uses_deterministic_template_from_approved_claims()
    {
        var draft = MakeDraft();

        var doc = await NarrativeRenderer.RenderAsync(new NarrativeId(1), NarrativeType.Chronicle, draft, llmProvider: null);

        Assert.Equal("A vila sofreu uma seca de 3 anos.", doc.Prose);
        Assert.Single(doc.Claims);
        Assert.Equal(new long[] { 1, 2 }, doc.Claims[0].EventIds);
    }

    [Fact]
    public async Task Render_without_llm_provider_is_deterministic_across_calls()
    {
        var draft = MakeDraft();

        var doc1 = await NarrativeRenderer.RenderAsync(new NarrativeId(1), NarrativeType.Chronicle, draft, llmProvider: null);
        var doc2 = await NarrativeRenderer.RenderAsync(new NarrativeId(1), NarrativeType.Chronicle, draft, llmProvider: null);

        Assert.Equal(doc1.Prose, doc2.Prose);
    }

    [Fact]
    public async Task Render_drops_unanchored_claims_from_both_prose_and_document_structure()
    {
        var draft = new NarrativeDraft(new CityId(Guid.NewGuid()), 0, 100, Claims:
        [
            new NarrativeClaim("evento ancorado", [1]),
            new NarrativeClaim("nada digno de nota", []),
        ]);

        var doc = await NarrativeRenderer.RenderAsync(new NarrativeId(2), NarrativeType.Chronicle, draft, llmProvider: null);

        Assert.Single(doc.Claims);
        Assert.Equal("evento ancorado", doc.Claims[0].Text);
        Assert.DoesNotContain("nada digno de nota", doc.Prose);
    }

    [Fact]
    public async Task Render_with_no_approved_claims_falls_back_to_a_fixed_notice_without_throwing()
    {
        var draft = new NarrativeDraft(null, 0, 100, Claims: [new NarrativeClaim("sem lastro", [])]);

        var doc = await NarrativeRenderer.RenderAsync(new NarrativeId(3), NarrativeType.Chronicle, draft, llmProvider: null);

        Assert.Empty(doc.Claims);
        Assert.Equal("sem registros ancorados para este período.", doc.Prose);
    }

    [Fact]
    public async Task Render_uses_llm_prose_when_llm_output_stays_anchored_to_approved_claims()
    {
        var draft = MakeDraft();
        var provider = new ScriptedProvider((_, _) =>
            Task.FromResult(new LlmResponse("A vila sofreu uma seca de 3 anos, e chorou.", "neutral", "none", [], [])));

        var doc = await NarrativeRenderer.RenderAsync(new NarrativeId(4), NarrativeType.Chronicle, draft, provider);

        Assert.Equal("A vila sofreu uma seca de 3 anos, e chorou.", doc.Prose);
        Assert.Single(doc.Claims);
        Assert.Equal(new long[] { 1, 2 }, doc.Claims[0].EventIds);
    }

    [Fact]
    public async Task Render_falls_back_to_template_when_llm_output_introduces_an_orphan_name_or_number()
    {
        var draft = MakeDraft();
        var provider = new ScriptedProvider((_, _) =>
            Task.FromResult(new LlmResponse("O relato citou Bartholomeu e 47 invernos.", "neutral", "none", [], [])));

        var doc = await NarrativeRenderer.RenderAsync(new NarrativeId(5), NarrativeType.Chronicle, draft, provider);

        Assert.Equal("A vila sofreu uma seca de 3 anos.", doc.Prose);
        Assert.Single(doc.Claims);
    }

    [Fact]
    public async Task Render_falls_back_to_template_when_provider_throws()
    {
        var draft = MakeDraft();
        var provider = new ScriptedProvider((_, _) => throw new InvalidOperationException("provider indisponível"));

        var doc = await NarrativeRenderer.RenderAsync(new NarrativeId(6), NarrativeType.Chronicle, draft, provider);

        Assert.Equal("A vila sofreu uma seca de 3 anos.", doc.Prose);
    }

    [Fact]
    public async Task Render_falls_back_to_template_when_provider_call_is_cancelled()
    {
        var draft = MakeDraft();
        var provider = new ScriptedProvider(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return new LlmResponse("nunca deveria chegar aqui", "neutral", "none", [], []);
        });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var doc = await NarrativeRenderer.RenderAsync(new NarrativeId(7), NarrativeType.Chronicle, draft, provider, cts.Token);

        Assert.Equal("A vila sofreu uma seca de 3 anos.", doc.Prose);
    }

    [Fact]
    public async Task NARR12_llm_on_and_off_produce_identical_claim_structure_only_prose_may_vary()
    {
        var draft = MakeDraft();
        var provider = new ScriptedProvider((_, _) =>
            Task.FromResult(new LlmResponse("A vila sofreu uma seca de 3 anos: tempos difíceis.", "neutral", "none", [], [])));

        var withoutLlm = await NarrativeRenderer.RenderAsync(new NarrativeId(8), NarrativeType.Chronicle, draft, llmProvider: null);
        var withLlm = await NarrativeRenderer.RenderAsync(new NarrativeId(8), NarrativeType.Chronicle, draft, provider);

        Assert.Equal(withoutLlm.Claims.Count, withLlm.Claims.Count);
        Assert.Equal(withoutLlm.Claims[0].EventIds, withLlm.Claims[0].EventIds);
        Assert.Equal(withoutLlm.Claims[0].Text, withLlm.Claims[0].Text);
        Assert.NotEqual(withoutLlm.Prose, withLlm.Prose);
    }
}
