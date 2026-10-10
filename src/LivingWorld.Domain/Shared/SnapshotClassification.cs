namespace LivingWorld.Domain.Shared;

/// <summary>Campo alimenta ao menos uma decisão — entra no hash canônico.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CanonicalAttribute : Attribute;

/// <summary>Campo é recomputável do canônico ou cosmético sem efeito causal —
/// nunca entra no hash canônico.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class VolatileAttribute : Attribute;

/// <summary>Volátil de sessão — não persiste em <c>WorldSnapshot.Serialize</c> (cognição,
/// observação, LOD cosmético). Reconstruído ao reidratar.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EphemeralAttribute : Attribute;
