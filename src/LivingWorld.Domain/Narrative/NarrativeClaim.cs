namespace LivingWorld.Domain.Narrative;

/// <summary>Unidade narrativa mínima (Fase 12, NARR-01) — todo texto publicado deriva de um
/// claim com <see cref="EventIds"/> não vazio; nunca prosa livre sem ancoragem. A validação de
/// ancoragem (claim descartado sem evento válido, nome/número órfão) é responsabilidade do
/// <c>ClaimAnchorValidator</c> — este tipo só carrega o dado estruturado.</summary>
public sealed record NarrativeClaim(string Text, IReadOnlyList<long> EventIds);
