namespace LivingWorld.Domain.Ontogeny;

public readonly record struct FluencyEntry(int Culture, double Level);

/// <summary>Fluência por idioma (um idioma = a cultura de quem o fala), ordenada por cultura. Igualdade por valor, para o estado de
/// desenvolvimento seguir comparável byte a byte. Vazia ao nascer: ninguém nasce falando.</summary>
public sealed class LanguageFluency : IEquatable<LanguageFluency>
{
    public static LanguageFluency Empty { get; } = new([]);

    public IReadOnlyList<FluencyEntry> Entries { get; }

    public LanguageFluency(IReadOnlyList<FluencyEntry> entries) => Entries = entries.OrderBy(e => e.Culture).ToArray();

    public double LevelOf(int culture) => Entries.FirstOrDefault(e => e.Culture == culture).Level;

    /// <summary>Idioma de maior fluência (empate: menor id de cultura); nulo se ainda não fala nenhum.</summary>
    public int? Dominant()
    {
        int? best = null;
        double level = 0;
        foreach (var e in Entries)
            if (e.Level > level) { level = e.Level; best = e.Culture; }
        return best;
    }

    public LanguageFluency Add(int culture, double amount)
    {
        var list = Entries.ToList();
        int i = list.FindIndex(e => e.Culture == culture);
        if (i >= 0) list[i] = new FluencyEntry(culture, list[i].Level + amount);
        else list.Add(new FluencyEntry(culture, amount));
        return new LanguageFluency(list);
    }

    public LanguageFluency Scale(double factor) =>
        new(Entries.Select(e => new FluencyEntry(e.Culture, e.Level * factor)).ToArray());

    public bool Equals(LanguageFluency? other) =>
        other is not null && Entries.SequenceEqual(other.Entries);

    public override bool Equals(object? obj) => Equals(obj as LanguageFluency);

    public override int GetHashCode()
    {
        var h = new HashCode();
        foreach (var e in Entries) h.Add(e);
        return h.ToHashCode();
    }
}
