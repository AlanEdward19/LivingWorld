using System.Text.Json.Nodes;
using LivingWorld.Domain;

namespace LivingWorld.Simulation.Snapshot;

/// <summary>Interning de strings repetidas no JSON do snapshot (Fase 28 T16, CMP-03): profissão
/// (tags <c>profession:</c>), traço (listas de fatores) e tag de evento
/// (<c>SystemName</c>, <c>SourceSystem</c>, <c>Kind</c>).</summary>
internal static class SnapshotStringInterning
{
    public const string StringTablePropertyName = "StringTable";

    private static readonly HashSet<string> DirectStringProperties = new(StringComparer.Ordinal)
    {
        "SystemName",
        "SourceSystem",
        "Kind",
        "Detail",
    };

    private static readonly HashSet<string> StringArrayProperties = new(StringComparer.Ordinal)
    {
        "Factors",
        "TopPositiveFactors",
        "TopNegativeFactors",
        "BlockingFactors",
    };

    private const string ProfessionPropertyName = "Profession";
    private const string ProfessionTagPrefix = "profession:";

    public static JsonObject Apply(JsonObject root)
    {
        var pool = new StringInternPool();
        CollectStrings(root, pool);
        if (pool.Count == 0)
            return root;

        ReplaceWithIds(root, pool);
        var table = new JsonArray();
        for (int i = 0; i < pool.Count; i++)
            table.Add(JsonValue.Create(pool.Resolve(i)));
        root[StringTablePropertyName] = table;
        return root;
    }

    public static JsonObject Resolve(JsonObject root)
    {
        if (!root.TryGetPropertyValue(StringTablePropertyName, out var tableNode) || tableNode is not JsonArray table)
            return root;

        var strings = table.Select(n => n!.GetValue<string>()).ToArray();
        ExpandIds(root, strings);
        root.Remove(StringTablePropertyName);
        return root;
    }

    private static void CollectStrings(JsonNode? node, StringInternPool pool)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var kv in obj.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                {
                    if (kv.Key == StringTablePropertyName)
                        continue;

                    if (kv.Key == ProfessionPropertyName && TryReadProfessionId(kv.Value, out int professionId))
                        pool.Intern(ProfessionTag(professionId));

                    if (DirectStringProperties.Contains(kv.Key) && kv.Value is JsonValue directValue
                        && directValue.GetValueKind() == System.Text.Json.JsonValueKind.String)
                        pool.Intern(directValue.GetValue<string>()!);

                    if (StringArrayProperties.Contains(kv.Key) && kv.Value is JsonArray arr)
                        foreach (var item in arr)
                            if (item is JsonValue arrayValue
                                && arrayValue.GetValueKind() == System.Text.Json.JsonValueKind.String)
                                pool.Intern(arrayValue.GetValue<string>()!);

                    CollectStrings(kv.Value, pool);
                }
                break;
            case JsonArray arr:
                foreach (var item in arr)
                    CollectStrings(item, pool);
                break;
        }
    }

    private static void ReplaceWithIds(JsonNode? node, StringInternPool pool)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var kv in obj)
                {
                    if (kv.Key == StringTablePropertyName)
                        continue;

                    if (kv.Key == ProfessionPropertyName && TryReadProfessionId(kv.Value, out int professionId))
                        obj[kv.Key] = JsonValue.Create(pool.Intern(ProfessionTag(professionId)));

                    else if (DirectStringProperties.Contains(kv.Key) && kv.Value is JsonValue directValue
                        && directValue.GetValueKind() == System.Text.Json.JsonValueKind.String)
                        obj[kv.Key] = JsonValue.Create(pool.Intern(directValue.GetValue<string>()!));

                    else if (StringArrayProperties.Contains(kv.Key) && kv.Value is JsonArray arr)
                    {
                        for (int i = 0; i < arr.Count; i++)
                            if (arr[i] is JsonValue arrayValue
                                && arrayValue.GetValueKind() == System.Text.Json.JsonValueKind.String)
                                arr[i] = JsonValue.Create(pool.Intern(arrayValue.GetValue<string>()!));
                    }

                    ReplaceWithIds(kv.Value, pool);
                }
                break;
            case JsonArray arr:
                foreach (var item in arr)
                    ReplaceWithIds(item, pool);
                break;
        }
    }

    private static void ExpandIds(JsonNode? node, string[] table)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var kv in obj)
                {
                    if (kv.Key == ProfessionPropertyName && kv.Value is JsonValue professionValue
                        && professionValue.TryGetValue<int>(out int professionRef)
                        && (uint)professionRef < (uint)table.Length
                        && TryParseProfessionTag(table[professionRef], out int restoredProfessionId))
                        obj[kv.Key] = new JsonObject { ["Id"] = restoredProfessionId };

                    else if (DirectStringProperties.Contains(kv.Key) && kv.Value is JsonValue v
                        && v.TryGetValue<int>(out int id) && (uint)id < (uint)table.Length)
                        obj[kv.Key] = JsonValue.Create(table[id]);

                    else if (StringArrayProperties.Contains(kv.Key) && kv.Value is JsonArray arr)
                    {
                        for (int i = 0; i < arr.Count; i++)
                            if (arr[i] is JsonValue item && item.TryGetValue<int>(out int arrayId)
                                && (uint)arrayId < (uint)table.Length)
                                arr[i] = JsonValue.Create(table[arrayId]);
                    }

                    ExpandIds(kv.Value, table);
                }
                break;
            case JsonArray arr:
                foreach (var item in arr)
                    ExpandIds(item, table);
                break;
        }
    }

    private static string ProfessionTag(int professionId) => $"{ProfessionTagPrefix}{professionId}";

    private static bool TryReadProfessionId(JsonNode? node, out int professionId)
    {
        if (node is JsonObject obj && obj.TryGetPropertyValue("Id", out var idNode)
            && idNode is JsonValue idValue && idValue.TryGetValue<int>(out professionId))
            return true;

        professionId = 0;
        return false;
    }

    private static bool TryParseProfessionTag(string tag, out int professionId)
    {
        if (tag.StartsWith(ProfessionTagPrefix, StringComparison.Ordinal)
            && int.TryParse(tag.AsSpan(ProfessionTagPrefix.Length), out professionId))
            return true;

        professionId = 0;
        return false;
    }
}
