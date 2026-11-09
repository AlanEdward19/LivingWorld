using System.Text;
using System.Text.Json;

namespace LivingWorld.Simulation.Observation;

/// <summary>Relatório multi-seed do observatório (issue #112 fatia B). Formato em
/// <c>docs/domain/civilization-observatory.md</c>. A saída é determinística: ordem de campos fixa, métricas por
/// chave ordinal, números no formato mais curto que volta ao mesmo double, sem duração nem data.</summary>
public static class CivilizationReport
{
    public static string ToJson(string scenario, IReadOnlyList<SeedRun> runs)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            w.WriteString("scenario", scenario);
            w.WriteStartArray("seeds");
            foreach (var run in runs.OrderBy(r => r.Seed))
            {
                w.WriteStartObject();
                w.WriteNumber("seed", run.Seed);
                w.WriteStartArray("years");
                foreach (var year in run.Years)
                {
                    w.WriteStartObject();
                    w.WriteNumber("year", year.Year);
                    w.WriteNumber("tick", year.Tick);
                    WriteMetrics(w, "world", year.World);
                    w.WriteStartArray("cities");
                    foreach (var city in year.Cities)
                    {
                        w.WriteStartObject();
                        w.WriteString("id", city.Id);
                        WriteMetrics(w, "metrics", city.Metrics);
                        w.WriteEndObject();
                    }

                    w.WriteEndArray();
                    w.WriteEndObject();
                }

                w.WriteEndArray();
                w.WriteEndObject();
            }

            w.WriteEndArray();
            WriteSummary(w, runs);
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    /// <summary>Resumo: mínimo, média e máximo de cada métrica do mundo no último ano, entre as seeds.</summary>
    private static void WriteSummary(Utf8JsonWriter w, IReadOnlyList<SeedRun> runs)
    {
        w.WriteStartObject("summary");
        w.WriteNumber("seeds", runs.Count);
        w.WriteNumber("years", runs.Count == 0 ? 0 : runs.Min(r => r.Years.Count));
        w.WriteStartObject("final");
        var finals = runs.Where(r => r.Years.Count > 0).Select(r => r.Years[^1].World).ToList();
        foreach (var key in finals.SelectMany(f => f.Keys).Distinct().Order(StringComparer.Ordinal))
        {
            var values = finals.Where(f => f.ContainsKey(key)).Select(f => f[key]).ToList();
            w.WriteStartObject(key);
            WriteNumber(w, "min", values.Min());
            WriteNumber(w, "mean", values.Sum() / values.Count);
            WriteNumber(w, "max", values.Max());
            w.WriteEndObject();
        }

        w.WriteEndObject();

        var diversity = TrajectoryDiversity.Compute(runs);
        w.WriteStartObject("diversity");
        w.WriteStartObject("byMetric");
        foreach (var (name, value) in diversity.ByMetric) WriteNumber(w, name, value);
        w.WriteEndObject();
        WriteNumber(w, "index", diversity.Index);
        w.WriteNumber("seeds", diversity.Seeds);
        w.WriteNumber("years", diversity.Years);
        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static void WriteMetrics(Utf8JsonWriter w, string name, IReadOnlyDictionary<string, double> metrics)
    {
        w.WriteStartObject(name);
        foreach (var (key, value) in metrics.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            WriteNumber(w, key, value);
        w.WriteEndObject();
    }

    private static void WriteNumber(Utf8JsonWriter w, string key, double value) =>
        w.WriteNumber(key, double.IsFinite(value) ? value : 0);
}
