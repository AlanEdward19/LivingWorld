namespace LivingWorld.Domain.Population.Skills;

/// <summary>Vocação de uma pessoa (AD-136). <see cref="Taste"/> é o gosto próprio por ofício (escala de desvios-padrão, média
/// 0), herdado em parte dos pais e sorteado no resto: é o "desde pequeno gostei disso". A escolha do ofício na juventude grava
/// o ofício da casa (<see cref="HouseProfession"/>, 0 = nenhum), a força com que a casa empurrou para ele (<see
/// cref="Pressure"/>, 0..1) e se o jovem cedeu contra a própria inclinação (<see cref="Pressured"/>).</summary>
public sealed record VocationState(
    IReadOnlyDictionary<int, double> Taste,
    bool Chosen = false,
    int HouseProfession = 0,
    double Pressure = 0,
    bool Pressured = false)
{
    public double TasteFor(int profession) => Taste.TryGetValue(profession, out var value) ? value : 0.0;
}
