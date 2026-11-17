using System.Text.Json;
using System.Text.Json.Serialization;
using LivingWorld.Api.Visual.Scope;

namespace LivingWorld.Api.Realtime;

/// <summary>Opções de serialização do que vai pelo fio (WS, SSE e replay). Snapshot: camelCase com nulos escritos, como o HTTP.
/// AD-172: o delta de escopo (<see cref="ScopeTickDelta"/>) omite toda chave nula — no delta, ausente = nulo, salvo os campos
/// "ausente = igual ao anterior" de <c>docs/domain/observable-projection.md</c>.</summary>
public static class VisualWire
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);

    public static JsonSerializerOptions DeltaOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>As opções certas para o que vai sair: envelope (ou lista de envelopes) de delta de escopo usa
    /// <see cref="DeltaOptions"/>; o resto, <see cref="Options"/>.</summary>
    public static JsonSerializerOptions For(object? payload) => IsScopeDelta(payload) ? DeltaOptions : Options;

    public static string Serialize(object? payload) => JsonSerializer.Serialize(payload, For(payload));

    private static bool IsScopeDelta(object? payload) => payload switch
    {
        ScopeTickDelta => true,
        VisualDeltaEnvelope<object?> { Payload: ScopeTickDelta } => true,
        IEnumerable<VisualDeltaEnvelope<object?>> list => list.Any(e => e.Payload is ScopeTickDelta),
        _ => false,
    };
}
