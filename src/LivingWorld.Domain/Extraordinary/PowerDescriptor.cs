namespace LivingWorld.Domain;

/// <summary>
/// Capacidade extraordinária autorada como dados composicionais. Os textos são ids de catálogo
/// do cenário, não categorias fechadas do motor; nenhum arquétipo exige um tipo C# próprio.
/// </summary>
public sealed record PowerDescriptor(
    string Id,
    string Source,
    IReadOnlyList<string> Effects,
    string Mode,
    IReadOnlyList<string> Costs,
    string Reliability,
    IReadOnlyList<string> FailureModes,
    IReadOnlyList<string> IntrinsicVulnerabilities,
    IReadOnlyList<string> Manifestations,
    IReadOnlyList<string> AcquisitionRules,
    ExtraordinaryAppearanceDescriptor? Appearance = null,
    NeedSubstitutionDescriptor? NeedSubstitution = null,
    double SenescenceRateMultiplier = 1,
    string? ManifestationCondition = null);

/// <summary>Indícios visuais genéricos; strings são tokens autorados, nunca arquétipos.</summary>
public sealed record ExtraordinaryAppearanceDescriptor(
    double ScaleMultiplier,
    string SkinTint,
    string MovementTrail);

/// <summary>Troca uma necessidade por consumo de um recurso específico do catálogo.</summary>
public sealed record NeedSubstitutionDescriptor(
    string ReplacesNeed,
    ResourceType Resource,
    long UnitsPerUse);

/// <summary>Estado resolvido de apresentação que uma projeção pode consumir sem interpretar poder.</summary>
public sealed record ExtraordinaryAppearanceState(
    double ScaleMultiplier,
    string SkinTint,
    string MovementTrail);

/// <summary>
/// Estado canônico de um portador. Senescência zero impede envelhecimento, não outras mortes.
/// </summary>
public sealed record ExtraordinaryCarrierState(
    NpcId CarrierId,
    IReadOnlyList<string> PowerIds,
    bool IsManifested,
    string ManifestationState,
    ExtraordinaryAppearanceState Appearance,
    NeedSubstitutionDescriptor? NeedSubstitution,
    double SenescenceRateMultiplier);

/// <summary>Conteúdo extraordinário resolvido na borda do cenário.</summary>
public sealed record ExtraordinaryScenarioData(bool Enabled, IReadOnlyList<PowerDescriptor> Descriptors)
{
    public static ExtraordinaryScenarioData Disabled { get; } = new(false, []);
}
