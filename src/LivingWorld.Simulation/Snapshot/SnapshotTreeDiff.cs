using System.Text.Json.Nodes;

namespace LivingWorld.Simulation.Snapshot;

/// <summary>Diff e aplicação de diff entre árvores de snapshot. O diff compara a árvore atual com as impressões
/// digitais da base (<see cref="SnapshotFingerprint"/>), não com a árvore da base: guardar a base inteira custaria
/// mais de 1 GB de memória com 10.000 NPCs. O diff (também uma árvore, codificada pelo mesmo
/// codec) tem quatro seções: <c>Entities</c> (arrays de objetos com <c>Id.Value</c>, como Npcs e Cities: só as entidades
/// novas ou alteradas, as alteradas campo a campo), <c>Keys</c> (objetos-dicionário, como Relationships: só as chaves
/// novas ou alteradas), <c>Removed</c> (ids ou chaves que saíram) e <c>Props</c> (o resto, propriedade inteira se mudou).
/// Aplicar o diff à base devolve exatamente a árvore atual.</summary>
internal static class SnapshotTreeDiff
{
    private const string EntitiesKey = "Entities", KeysKey = "Keys", RemovedKey = "Removed", PropsKey = "Props", OrderKey = "Order";

    public static JsonObject Diff(JsonObject current, SnapshotFingerprint baseline, IReadOnlySet<long>? dirtyNpcIds)
    {
        var entities = new JsonObject();
        var keys = new JsonObject();
        var removed = new JsonObject();
        var props = new JsonObject();
        var order = new JsonObject();

        foreach (var (name, value) in current)
        {
            if (value is JsonArray ca && baseline.Entities.TryGetValue(name, out var baseEntities) && TryIndex(ca, out var curIds))
                DiffEntities(name, ca, curIds, baseEntities, name == "Npcs" ? dirtyNpcIds : null, entities, removed, order);
            else if (value is JsonObject co && baseline.Keys.TryGetValue(name, out var baseKeys))
                DiffKeys(name, co, baseKeys, keys, removed);
            else if (!baseline.Props.TryGetValue(name, out var print) || print != SnapshotFingerprint.Print(value))
                props[name] = value?.DeepClone();
        }

        return new JsonObject { [EntitiesKey] = entities, [KeysKey] = keys, [RemovedKey] = removed, [PropsKey] = props, [OrderKey] = order };
    }

    public static JsonObject Apply(JsonObject baseTree, JsonObject delta)
    {
        var merged = baseTree.DeepClone().AsObject();
        var removed = delta[RemovedKey]!.AsObject();

        foreach (var (name, value) in delta[PropsKey]!.AsObject())
            merged[name] = value?.DeepClone();

        foreach (var (name, changed) in delta[KeysKey]!.AsObject())
        {
            var target = merged[name]!.AsObject();
            foreach (var (key, value) in changed!.AsObject())
                target[key] = value?.DeepClone();
        }

        foreach (var (name, gone) in removed)
        {
            if (merged[name] is JsonObject dict)
            {
                foreach (var key in gone!.AsArray()) dict.Remove(key!.GetValue<string>());
            }
        }

        var orders = delta[OrderKey]!.AsObject();
        foreach (var (name, changed) in delta[EntitiesKey]!.AsObject())
        {
            var target = merged[name]!.AsArray();
            TryIndex(target, out var byId);
            var goneIds = removed[name] is JsonArray g ? g.Select(x => x!.GetValue<long>()).ToHashSet() : [];
            foreach (var entry in changed!.AsArray())
            {
                var obj = entry!.AsObject();
                long id = obj["Id"]!["Value"]!.GetValue<long>();
                if (byId.TryGetValue(id, out int at))
                {
                    var targetObj = target[at]!.AsObject();
                    foreach (var (field, value) in obj) targetObj[field] = value?.DeepClone();
                }
                else target.Add(obj.DeepClone());
            }

            if (goneIds.Count > 0)
                for (int i = target.Count - 1; i >= 0; i--)
                    if (goneIds.Contains(target[i]!["Id"]!["Value"]!.GetValue<long>())) target.RemoveAt(i);

            if (orders[name] is JsonArray wanted)
            {
                TryIndex(target, out var now);
                var reordered = new JsonArray();
                foreach (var id in wanted) reordered.Add(target[now[id!.GetValue<long>()]]!.DeepClone());
                merged[name] = reordered;
            }
        }

        return merged;
    }

    private static void DiffEntities(
        string name, JsonArray cur, Dictionary<long, int> curIds, SnapshotFingerprint.EntityPrints baseEntities,
        IReadOnlySet<long>? dirty, JsonObject entities, JsonObject removed, JsonObject order)
    {
        var changed = new JsonArray();
        foreach (var (id, at) in curIds.OrderBy(kv => kv.Value))
        {
            var entry = cur[at]!.AsObject();
            if (!baseEntities.ById.TryGetValue(id, out var prints)) { changed.Add(entry.DeepClone()); continue; }
            if (dirty is not null && !dirty.Contains(id)) continue;

            JsonObject? fieldDiff = null;
            int f = 0;
            bool sameShape = prints is not null && entry.Count == baseEntities.Fields.Length;
            foreach (var (field, value) in entry)
            {
                if (sameShape && field == baseEntities.Fields[f] && prints![f] == SnapshotFingerprint.Print(value)) { f++; continue; }
                f++;
                if (field == "Id") continue;
                fieldDiff ??= new JsonObject { ["Id"] = entry["Id"]!.DeepClone() };
                fieldDiff[field] = value?.DeepClone();
            }

            if (fieldDiff is not null) changed.Add(fieldDiff);
        }

        if (changed.Count > 0) entities[name] = changed;

        var gone = new JsonArray();
        foreach (var id in baseEntities.Order)
            if (!curIds.ContainsKey(id)) gone.Add(id);
        if (gone.Count > 0) removed[name] = gone;

        // ordem esperada ao aplicar: base sem os removidos, depois os novos na ordem atual
        var currentOrder = curIds.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();
        var expected = baseEntities.Order.Where(curIds.ContainsKey)
            .Concat(currentOrder.Where(id => !baseEntities.ById.ContainsKey(id)));
        if (!expected.SequenceEqual(currentOrder))
            order[name] = new JsonArray(currentOrder.Select(id => (JsonNode)JsonValue.Create(id)).ToArray());
    }

    private static void DiffKeys(string name, JsonObject cur, Dictionary<string, ulong> baseKeys, JsonObject keys, JsonObject removed)
    {
        JsonObject? changed = null;
        foreach (var (key, value) in cur)
        {
            if (baseKeys.TryGetValue(key, out var print) && print == SnapshotFingerprint.Print(value)) continue;
            changed ??= new JsonObject();
            changed[key] = value?.DeepClone();
        }

        if (changed is not null) keys[name] = changed;

        JsonArray? gone = null;
        foreach (var key in baseKeys.Keys)
        {
            if (cur.ContainsKey(key)) continue;
            gone ??= new JsonArray();
            gone.Add(key);
        }

        if (gone is not null) removed[name] = gone;
    }

    /// <summary>Índice id→posição de um array de entidades; falso se algum elemento não tem <c>Id.Value</c>.</summary>
    internal static bool TryIndex(JsonArray array, out Dictionary<long, int> index)
    {
        index = new Dictionary<long, int>(array.Count);
        if (array.Count == 0) return false;
        for (int i = 0; i < array.Count; i++)
        {
            if (array[i] is not JsonObject o || o["Id"] is not JsonObject id || id["Value"] is not JsonValue v
                || !v.TryGetValue(out long value) || !index.TryAdd(value, i))
                return false;
        }

        return true;
    }
}

/// <summary>Impressões digitais de 64 bits de uma árvore de snapshot, o bastante para dizer o que mudou sem guardar a
/// árvore: por campo de cada entidade, por chave de cada dicionário e por propriedade solta. Só vale no processo que a
/// calculou, e só para comparar com árvores do mesmo formato.</summary>
internal sealed class SnapshotFingerprint
{
    internal sealed class EntityPrints
    {
        public string[] Fields = [];
        public List<long> Order = [];

        /// <summary>Impressões por campo, na ordem de <see cref="Fields"/>; nulo se a entidade tem outro conjunto de campos.</summary>
        public Dictionary<long, ulong[]?> ById = [];
    }

    public Dictionary<string, EntityPrints> Entities { get; } = [];
    public Dictionary<string, Dictionary<string, ulong>> Keys { get; } = [];
    public Dictionary<string, ulong> Props { get; } = [];

    public static SnapshotFingerprint Of(JsonObject tree)
    {
        var result = new SnapshotFingerprint();
        foreach (var (name, value) in tree)
        {
            if (value is JsonArray array && SnapshotTreeDiff.TryIndex(array, out var ids))
                result.Entities[name] = PrintEntities(array, ids);
            else if (value is JsonObject obj)
            {
                var keys = new Dictionary<string, ulong>(obj.Count);
                foreach (var (key, child) in obj) keys[key] = Print(child);
                result.Keys[name] = keys;
            }
            else
                result.Props[name] = Print(value);
        }

        return result;
    }

    private static EntityPrints PrintEntities(JsonArray array, Dictionary<long, int> ids)
    {
        var prints = new EntityPrints { Fields = array[0]!.AsObject().Select(kv => kv.Key).ToArray() };
        foreach (var (id, at) in ids.OrderBy(kv => kv.Value))
        {
            prints.Order.Add(id);
            var entry = array[at]!.AsObject();
            if (entry.Count != prints.Fields.Length) { prints.ById[id] = null; continue; }

            var fields = new ulong[entry.Count];
            int f = 0;
            bool same = true;
            foreach (var (field, value) in entry)
            {
                if (field != prints.Fields[f]) { same = false; break; }
                fields[f++] = Print(value);
            }

            prints.ById[id] = same ? fields : null;
        }

        return prints;
    }

    public static ulong Print(JsonNode? node)
    {
        ulong h = 14695981039346656037UL;
        Mix(node, ref h);
        return h;
    }

    private static void Step(ref ulong h, ulong x)
    {
        h = (h ^ x) * 0x9E3779B97F4A7C15UL;
        h ^= h >> 32;
    }

    private static void MixString(string s, ref ulong h)
    {
        foreach (char c in s) Step(ref h, c);
        Step(ref h, (ulong)s.Length);
    }

    private static void Mix(JsonNode? node, ref ulong h)
    {
        switch (node)
        {
            case null:
                Step(ref h, 1);
                break;
            case JsonObject obj:
                Step(ref h, 2);
                foreach (var (key, value) in obj)
                {
                    MixString(key, ref h);
                    Mix(value, ref h);
                }

                Step(ref h, 3);
                break;
            case JsonArray arr:
                Step(ref h, 4);
                foreach (var item in arr) Mix(item, ref h);
                Step(ref h, 5);
                break;
            case JsonValue v:
                switch (v.GetValueKind())
                {
                    case System.Text.Json.JsonValueKind.String:
                        Step(ref h, 6);
                        MixString(v.GetValue<string>(), ref h);
                        break;
                    case System.Text.Json.JsonValueKind.Number:
                        Step(ref h, 7);
                        MixNumber(v, ref h);
                        break;
                    case System.Text.Json.JsonValueKind.True: Step(ref h, 8); break;
                    case System.Text.Json.JsonValueKind.False: Step(ref h, 9); break;
                    default: Step(ref h, 1); break;
                }

                break;
        }
    }

    private static void MixNumber(JsonValue v, ref ulong h)
    {
        if (v.TryGetValue(out long l)) { Step(ref h, (ulong)l); return; }
        if (v.TryGetValue(out ulong u)) { Step(ref h, u); return; }
        if (v.TryGetValue(out int i)) { Step(ref h, (ulong)(long)i); return; }
        double d = v.TryGetValue(out double dd) ? dd : v.TryGetValue(out float ff) ? ff : double.Parse(v.ToJsonString(), System.Globalization.CultureInfo.InvariantCulture);
        if (d == Math.Floor(d) && Math.Abs(d) < 9e15) Step(ref h, (ulong)(long)d);
        else Step(ref h, (ulong)BitConverter.DoubleToInt64Bits(d));
    }
}
