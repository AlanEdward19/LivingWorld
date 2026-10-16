namespace LivingWorld.Api.Visual.Layers;

/// <summary>Resultado de montar uma camada derivada. Formato plano
/// (discriminador + payload), não hierarquia de records — um record abstrato com subtipos não
/// serializa os campos dos subtipos via <c>System.Text.Json</c> sem atributos de polimorfismo
/// (o declared type do dicionário fica abstrato, então a reflexão padrão não vê propriedade
/// nenhuma). Algumas camadas do catálogo ainda não têm dado canônico no motor (nenhuma classe de
/// Kingdom/Border/Road/Climate existe, e Altitude não tem limiar documentado para "montanha") —
/// <see cref="IsModeled"/> falso é o mesmo padrão de fallback do design.md para asset ausente:
/// render não quebra sessão, só não tem conteúdo até o domínio ganhar esse conceito.</summary>
public sealed record LayerBuildResult(bool IsModeled, object? Payload)
{
    public static LayerBuildResult Available(object payload) => new(true, payload);

    public static readonly LayerBuildResult NotYetModeled = new(false, null);

    /// <summary>Fallback compartilhado dos builders de camada (<c>GlobalLayerBuilder</c>/
    /// <c>CityLayerBuilder</c>) pra um <see cref="VisualLayerId"/> que não pertence ao escopo de
    /// quem chamou — mesmo formato de mensagem nos dois, um só lugar pra manter.</summary>
    public static LayerBuildResult OutOfScope(VisualLayerId layerId, string scopeName) =>
        throw new ArgumentOutOfRangeException(nameof(layerId), layerId, $"camada não pertence ao escopo {scopeName}");
}
