using LivingWorld.Domain;
using LivingWorld.Domain.Narrative;

namespace LivingWorld.Simulation.Narrative;

/// <summary>Job periódico de crônicas (Fase 12, NARR-05..08) — publica, por cidade, a crônica da
/// janela do mês recém-fechado, reaproveitando <see cref="WindowedHistoryAggregator"/> (top-K
/// fatos por significância) e <see cref="NarrativeRenderer"/> (template determinístico, sem LLM).
/// <see cref="Frequency"/> é <see cref="TickFrequency.Monthly"/> — nunca diário (spec.md Edge
/// Cases: "geração narrativa roda por 10 anos simulados THEN sistema SHALL não executar sistema
/// narrativo no tick diário"); <see cref="WorldClock"/> só chama <see cref="Tick"/> em fronteira
/// de mês, então esta garantia vem da própria declaração de frequência, não de lógica extra
/// aqui. Publicação idempotente por <c>(local, periodStart, periodEnd)</c> (edge case de dois
/// jobs concorrentes processando a mesma janela): reprocessar a mesma chave devolve o mesmo
/// documento já publicado, nunca duplica. Estado próprio do sistema (não em <see
/// cref="WorldState"/>) — mesmo molde de <see
/// cref="LivingWorld.Simulation.Llm.ConversationSessionStore"/>: reprocessar do zero após religar
/// produz a mesma crônica a partir dos mesmos <see cref="Fact"/>s (NARR-08), então não há nada
/// que precise sobreviver ao snapshot.</summary>
public sealed class ChronicleGenerationSystem : ISimulationSystem
{
    public const string SystemName = "narrative-chronicle-generation";

    private readonly Dictionary<(CityId? Location, long PeriodStart, long PeriodEnd), NarrativeDocument> _published = new();
    private long _nextNarrativeId;

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Monthly;

    /// <summary>Crônicas publicadas até agora (uma por chave `(local, periodStart, periodEnd)`).</summary>
    public IReadOnlyCollection<NarrativeDocument> Chronicles => _published.Values;

    /// <summary>Fecha a janela do mês que acabou de terminar e publica a crônica de cada cidade
    /// conhecida. Sem janela fechada ainda (menos de um mês desde a origem do mundo) ou sem
    /// cidade nenhuma, não publica nada — nunca produz preenchimento genérico sem fato algum.</summary>
    public void Tick(WorldState world, TickContext ctx)
    {
        long periodEnd = ctx.CurrentTick;
        long periodStart = periodEnd - world.Calendar.HoursPerMonth;
        if (periodStart < 0)
            return;

        foreach (var city in world.ActiveCities().OrderBy(c => c.Id.Value))
            GenerateChronicle(world, city.Id, periodStart, periodEnd);
    }

    /// <summary>Gera (ou devolve a já publicada) a crônica de <paramref name="location"/> para
    /// <c>[periodStartTick, periodEndTick)</c>. Público para permitir disparo direto (job manual,
    /// teste, futura API/CLI) fora do <see cref="Tick"/> automático, com a mesma garantia de
    /// idempotência por chave — dois jobs concorrentes na mesma janela nunca publicam duas vezes
    /// (spec.md Edge Cases).</summary>
    public NarrativeDocument GenerateChronicle(
        WorldState world, CityId? location, long periodStartTick, long periodEndTick, int topK = 5)
    {
        var key = (location, periodStartTick, periodEndTick);
        if (_published.TryGetValue(key, out var existing))
            return existing;

        var topFacts = WindowedHistoryAggregator.TopFacts(world, location, periodStartTick, periodEndTick, topK);
        var claims = topFacts
            .Select(f => new NarrativeClaim(
                $"{f.Kind} (evento {f.Id.Value}): {f.Payload}", (IReadOnlyList<long>)[f.Id.Value]))
            .ToList();
        var draft = new NarrativeDraft(location, periodStartTick, periodEndTick, claims);

        var document = NarrativeRenderer
            .RenderAsync(new NarrativeId(_nextNarrativeId++), NarrativeType.Chronicle, draft)
            .GetAwaiter().GetResult();

        _published[key] = document;
        return document;
    }
}
