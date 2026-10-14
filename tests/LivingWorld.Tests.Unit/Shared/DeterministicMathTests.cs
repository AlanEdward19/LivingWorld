using System.Text.RegularExpressions;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Tests.Unit.Shared;

/// <summary>A libm da plataforma erra o último ulp de forma diferente em cada sistema, então o mundo não pode usar
/// <c>Math.Log/Cos</c>. Os valores fixados em bits são o sensor entre máquinas: o mesmo teste roda no Windows, no Linux e no macOS.</summary>
public class DeterministicMathTests
{
    [Theory]
    [InlineData(0.3, unchecked((long)0xbff34378fcbda720))]
    [InlineData(1e-12, unchecked((long)0xc03ba18a998fffa0))]
    [InlineData(12345.678, unchecked((long)0x4022d79559791e31))]
    public void Log_returns_the_same_bits_on_every_platform(double x, long bits) =>
        Assert.Equal(bits, BitConverter.DoubleToInt64Bits(DeterministicMath.Log(x)));

    [Theory]
    [InlineData(0.1234, unchecked((long)0x3fe6da8f0e67232c))]
    [InlineData(0.7, unchecked((long)0xbfd3c6ef372fe954))]
    [InlineData(0.25, 0L)]
    public void CosTurns_returns_the_same_bits_on_every_platform(double turns, long bits) =>
        Assert.Equal(bits, BitConverter.DoubleToInt64Bits(DeterministicMath.CosTurns(turns)));

    [Fact]
    public void SinTurns_returns_the_same_bits_on_every_platform() =>
        Assert.Equal(unchecked((long)0x3fee6f0e134454ff), BitConverter.DoubleToInt64Bits(DeterministicMath.SinTurns(0.2)));

    [Fact]
    public void The_functions_agree_with_the_platform_math_to_within_a_few_ulps_over_a_sweep()
    {
        for (int i = 1; i <= 2000; i++)
        {
            double x = i / 100.0;
            Assert.Equal(Math.Log(x), DeterministicMath.Log(x), 1e-12);
            Assert.Equal(Math.Log(1.0 / x), DeterministicMath.Log(1.0 / x), 1e-12);
            double turns = i / 2000.0 * 3 - 1; // inclui voltas negativas e acima de 1
            Assert.Equal(Math.Cos(2 * Math.PI * turns), DeterministicMath.CosTurns(turns), 1e-12);
            Assert.Equal(Math.Sin(2 * Math.PI * turns), DeterministicMath.SinTurns(turns), 1e-12);
        }
    }

    [Fact]
    public void Log_of_non_positive_is_not_a_number_or_negative_infinity()
    {
        Assert.Equal(double.NegativeInfinity, DeterministicMath.Log(0));
        Assert.True(double.IsNaN(DeterministicMath.Log(-1)));
    }

    [Fact]
    public void The_world_code_never_calls_the_platform_transcendental_math()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "LivingWorld.sln"))) root = Directory.GetParent(root)!.FullName;
        var banned = new Regex(@"\b(?:Math|MathF|double|Double|float|Single)\.(?:Log|Log2|Log10|Exp|Pow|Sin|Cos|Tan|Asin|Acos|Atan|Atan2|Sinh|Cosh|Tanh|Cbrt)\b|using static System\.(?:Math|MathF|Double|Single);");

        var offenders = new[] { "LivingWorld.Domain", "LivingWorld.Simulation" }
            .SelectMany(p => Directory.EnumerateFiles(Path.Combine(root, "src", p), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !f.EndsWith("DeterministicMath.cs"))
            .Where(f => File.ReadLines(f).Any(l => !l.TrimStart().StartsWith("//") && !l.TrimStart().StartsWith("///") && banned.IsMatch(l)))
            .Select(f => Path.GetRelativePath(root, f));

        Assert.Empty(offenders);
    }
}
