using LivingWorld.Api.Visual.Layers;

namespace LivingWorld.Api.Visual.Scope;

/// <summary>Payload completo de um escopo/modo num cursor,
/// com as camadas ativas selecionadas pelo viewer. <typeparamref name="TPayload"/> é a
/// projeção concreta (global/cidade/interior), definida pelos projectors correspondentes.</summary>
public sealed record VisualSnapshotEnvelope<TPayload>(
    VisualScope Scope,
    ViewerMode Mode,
    VisualCursor Cursor,
    IReadOnlyList<VisualLayerId> ActiveLayers,
    TPayload Payload);

/// <summary>Alterações de um escopo entre dois cursores, sem escrita de volta
/// no domínio — usado pelo gateway realtime para push incremental.</summary>
public sealed record VisualDeltaEnvelope<TPayload>(
    VisualScope Scope,
    VisualCursor FromCursor,
    VisualCursor ToCursor,
    TPayload Payload);
