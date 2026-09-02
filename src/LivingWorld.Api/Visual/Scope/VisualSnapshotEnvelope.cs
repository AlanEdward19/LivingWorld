using LivingWorld.Api.Visual.Layers;

namespace LivingWorld.Api.Visual.Scope;

/// <summary>Fase 15, T1 (VTT-01, VTT-04): payload completo de um escopo/modo num cursor,
/// com as camadas ativas selecionadas pelo viewer. <typeparamref name="TPayload"/> é a
/// projeção concreta (global/cidade/interior), definida pelos projectors das tasks seguintes.</summary>
public sealed record VisualSnapshotEnvelope<TPayload>(
    VisualScope Scope,
    ViewerMode Mode,
    VisualCursor Cursor,
    IReadOnlyList<VisualLayerId> ActiveLayers,
    TPayload Payload);

/// <summary>Fase 15, T1: alterações de um escopo entre dois cursores, sem escrita de volta
/// no domínio — usado pelo gateway realtime para push incremental.</summary>
public sealed record VisualDeltaEnvelope<TPayload>(
    VisualScope Scope,
    VisualCursor FromCursor,
    VisualCursor ToCursor,
    TPayload Payload);
