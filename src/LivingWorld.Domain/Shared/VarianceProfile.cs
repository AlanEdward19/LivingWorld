namespace LivingWorld.Domain.Shared;

public enum VarianceProfileKind
{
    /// <summary>d20: indivíduo, cena, confronto, cortejo. Críticos existem.</summary>
    Dramatico,

    /// <summary>Curva estreita: produção, preço, demografia. Nunca produz crítico.</summary>
    Agregado,

    /// <summary>Cauda longa: mutação, invenção, contato, catástrofe. Crítico é raro.</summary>
    Raro,
}

/// <summary>Perfil de variância é dado de cenário (ADR-0011), não constante de código —
/// por isso nasce de um <see cref="VarianceProfileCatalog"/> declarado, nunca de um enum fixo
/// de nomes.</summary>
public sealed record VarianceProfile(string Name, VarianceProfileKind Kind, int SuccessMargin, int PartialMargin)
{
    public static VarianceProfile Dramatico(string name) => new(name, VarianceProfileKind.Dramatico, SuccessMargin: 0, PartialMargin: 5);
    public static VarianceProfile Agregado(string name) => new(name, VarianceProfileKind.Agregado, SuccessMargin: 0, PartialMargin: 3);
    public static VarianceProfile Raro(string name) => new(name, VarianceProfileKind.Raro, SuccessMargin: 0, PartialMargin: 5);
}

/// <summary>Registro dos perfis declarados por um cenário. Perfil não declarado falha no load.</summary>
public sealed class VarianceProfileCatalog
{
    private readonly Dictionary<string, VarianceProfile> _profiles;

    public VarianceProfileCatalog(IEnumerable<VarianceProfile> profiles)
        => _profiles = profiles.ToDictionary(p => p.Name);

    public VarianceProfile Get(string name)
        => _profiles.TryGetValue(name, out var profile)
            ? profile
            : throw new InvalidOperationException($"perfil de variância não declarado no cenário: '{name}'");
}
