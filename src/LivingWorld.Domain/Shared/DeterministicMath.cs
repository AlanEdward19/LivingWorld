namespace LivingWorld.Domain.Shared;

/// <summary>Log e cosseno só com <c>+ - * /</c> e operações de bits, que o IEEE 754 fixa bit a bit em qualquer CPU e sistema.
/// <c>Math.Log</c>/<c>Math.Cos</c> chamam a libm da plataforma, que erra no último ulp de forma diferente no Windows, no glibc, no musl
/// e no macOS; um ulp a mais no sorteio de um NPC muda o hash do mundo e a trajetória inteira. Tudo que alimenta o estado do mundo
/// usa isto, nunca a <c>Math</c> transcendente (ver <c>rules/simulation-determinism.md</c>).</summary>
public static class DeterministicMath
{
    private const double Ln2 = 0.6931471805599453;
    private const double TwoPi = 6.283185307179586;
    private const double Sqrt2 = 1.4142135623730951;

    /// <summary>Logaritmo natural de um número positivo normal; zero devolve -∞ e negativo ou NaN devolve NaN. Subnormais não são
    /// suportados (o resultado é errado): quem chama limita o argumento antes. Erro relativo abaixo de 1e-15.</summary>
    public static double Log(double x)
    {
        if (!(x > 0)) return x == 0 ? double.NegativeInfinity : double.NaN;
        if (double.IsPositiveInfinity(x)) return x;

        long bits = BitConverter.DoubleToInt64Bits(x);
        int exponent = (int)((bits >> 52) & 0x7FF) - 1023;
        double mantissa = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L); // [1, 2)
        if (mantissa > Sqrt2)
        {
            mantissa *= 0.5;
            exponent++;
        }

        // ln m = 2 atanh(s), s = (m - 1) / (m + 1), |s| <= 0.1716
        double s = (mantissa - 1.0) / (mantissa + 1.0), s2 = s * s, sum = 0;
        for (int k = 21; k >= 0; k--) sum = sum * s2 + 1.0 / (2 * k + 1);
        return exponent * Ln2 + 2.0 * s * sum;
    }

    /// <summary>Cosseno de 2π·<paramref name="turns"/> (o argumento é uma fração de volta, não radianos): reduz por simetria,
    /// só com subtrações exatas, a [0, 1/4] de volta e aplica Taylor.</summary>
    public static double CosTurns(double turns)
    {
        double u = turns - Math.Floor(turns);
        if (u > 0.5) u = 1.0 - u;
        double sign = 1.0;
        if (u > 0.25)
        {
            u = 0.5 - u;
            sign = -1.0;
        }

        double x = TwoPi * u, x2 = x * x, sum = 0;
        for (int k = 14; k >= 0; k--) sum = 1.0 - x2 / ((2.0 * k + 1) * (2.0 * k + 2)) * sum;
        return sign * sum;
    }

    /// <summary>Seno de 2π·<paramref name="turns"/>.</summary>
    public static double SinTurns(double turns) => CosTurns(turns - 0.25);
}
