using System.Text.Json;

namespace LivingWorld.Tests.Baselines;

/// <summary>Roda um valor por seed contra um baseline gravado em disco. Regravar é comando
/// explícito (<see cref="Record"/>) — nunca efeito colateral de <see cref="AssertMatches"/>.</summary>
public static class BaselineFixture
{
    public static void AssertMatches<T>(string baselinesDir, string name, IReadOnlyDictionary<int, T> actualBySeed)
    {
        var path = Path.Combine(baselinesDir, name + ".json");
        if (!File.Exists(path))
            throw new BaselineMissingException(path);

        var expected = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path))!;
        var actualJson = JsonSerializer.Serialize(actualBySeed.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value));
        var actual = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(actualJson)!;

        var mismatches = expected.Keys.Union(actual.Keys).OrderBy(k => k)
            .SelectMany(seed => DiffSeed(seed, expected, actual))
            .ToList();

        if (mismatches.Count > 0)
            throw new BaselineMismatchException(name, mismatches);
    }

    public static void Record<T>(string baselinesDir, string name, IReadOnlyDictionary<int, T> valueBySeed)
    {
        Directory.CreateDirectory(baselinesDir);
        var dict = valueBySeed.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
        var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(baselinesDir, name + ".json"), json);
    }

    private static IEnumerable<string> DiffSeed(string seed, Dictionary<string, JsonElement> expected, Dictionary<string, JsonElement> actual)
    {
        if (!expected.TryGetValue(seed, out var e))
        {
            yield return $"seed {seed}: não está no baseline, obtido {actual[seed].GetRawText()}";
            yield break;
        }
        if (!actual.TryGetValue(seed, out var a))
        {
            yield return $"seed {seed}: esperado {e.GetRawText()}, não produzido nesta rodada";
            yield break;
        }

        if (e.ValueKind != JsonValueKind.Object)
        {
            if (e.GetRawText() != a.GetRawText())
                yield return $"seed {seed}: esperado {e.GetRawText()}, obtido {a.GetRawText()}";
            yield break;
        }

        foreach (var prop in e.EnumerateObject())
        {
            if (!a.TryGetProperty(prop.Name, out var actualValue))
            {
                yield return $"seed {seed} campo {prop.Name}: ausente no resultado atual";
                continue;
            }
            if (prop.Value.GetRawText() != actualValue.GetRawText())
                yield return $"seed {seed} campo {prop.Name}: esperado {prop.Value.GetRawText()}, obtido {actualValue.GetRawText()}";
        }
    }
}

public sealed class BaselineMismatchException(string name, IReadOnlyList<string> mismatches)
    : Exception($"baseline '{name}' diverge:\n{string.Join("\n", mismatches)}");

public sealed class BaselineMissingException(string path)
    : Exception($"baseline ausente: {path}. Regravar é comando explícito (BaselineFixture.Record) — nunca gerado pelo gate.");
