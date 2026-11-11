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
    private const string EntitiesKey = "Entities", KeysKey = "Keys", RemovedKey = "Removed", PropsKey = "Props", OrderKey = "Order", MovedKey = "Moved";

    public static JsonObject Diff(JsonObject current, SnapshotFingerprint baseline, IReadOnlySet<long>? dirtyNpcIds)
    {
        var entities = new JsonObject();
        var keys = new JsonObject();
        var removed = new JsonObject();
        var props = new JsonObject();
        var order = new JsonObject();
        var moved = new JsonObject();

        foreach (var (name, value) in current)
        {
            if (value is JsonArray ca && baseline.Entities.TryGetValue(name, out var baseEntities) && TryIndex(ca, out var curIds))
                DiffEntities(name, ca, curIds, baseEntities, name == "Npcs" ? dirtyNpcIds : null, entities, removed, order);
            else if (value is JsonObject co && baseline.Keys.TryGetValue(name, out var baseKeys))
                DiffKeys(name, co, baseKeys, keys, removed, moved);
            else if (!baseline.Props.TryGetValue(name, out var print) || print != SnapshotFingerprint.Print(value))
                props[name] = value?.DeepClone();
        }

        var delta = new JsonObject { [EntitiesKey] = entities, [KeysKey] = keys, [RemovedKey] = removed, [PropsKey] = props, [OrderKey] = order };
        if (moved.Count > 0) delta[MovedKey] = moved; // só existe quando alguma chave saiu e voltou: diff antigo não tem a seção
        return delta;
    }

    public static JsonObject Apply(JsonObject baseTree, JsonObject delta)
    {
        var merged = baseTree.DeepClone().AsObject();
        var removed = delta[RemovedKey]!.AsObject();

        foreach (var (name, value) in delta[PropsKey]!.AsObject())
            merged[name] = value?.DeepClone();

        // Chave que saiu e voltou desde a base vai para o fim, como no mundo: tira antes de reaplicar.
        if (delta[MovedKey] is JsonObject movedSections)
            foreach (var (name, movedKeys) in movedSections)
                if (merged[name] is JsonObject dict)
                    foreach (var key in movedKeys!.AsArray()) dict.Remove(key!.GetValue<string>());

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

    private static void DiffKeys(
        string name, JsonObject cur, Dictionary<string, ulong> baseKeys, JsonObject keys, JsonObject removed, JsonObject moved)
    {
        var movedKeys = MovedKeysOf(cur, baseKeys);

        JsonObject? changed = null;
        foreach (var (key, value) in cur)
        {
            if (baseKeys.TryGetValue(key, out var print) && print == SnapshotFingerprint.Print(value)
                && (movedKeys is null || !movedKeys.Contains(key))) continue;
            changed ??= new JsonObject();
            changed[key] = value?.DeepClone();
        }

        if (changed is not null) keys[name] = changed;
        if (movedKeys is { Count: > 0 })
            moved[name] = new JsonArray(movedKeys.OrderBy(k => k, StringComparer.Ordinal).Select(k => (JsonNode)JsonValue.Create(k)!).ToArray());

        JsonArray? gone = null;
        foreach (var key in baseKeys.Keys)
        {
            if (cur.ContainsKey(key)) continue;
            gone ??= new JsonArray();
            gone.Add(key);
        }

        if (gone is not null) removed[name] = gone;
    }

    /// <summary>Chaves da base que, na árvore atual, não estão mais onde aplicar o diff as deixaria (saíram e voltaram, e
    /// por isso foram para o fim). Aplicar o diff mantém as chaves da base na ordem da base e acrescenta as novas no fim;
    /// qualquer chave da base que apareça depois de uma chave nova, ou fora da ordem da base, foi movida. <c>null</c> no
    /// caso comum (nenhuma), decidido numa passada sem alocar o índice.</summary>
    private static HashSet<string>? MovedKeysOf(JsonObject cur, Dictionary<string, ulong> baseKeys)
    {
        bool inPlace = true, sawNew = false;
        using (var expected = baseKeys.Keys.GetEnumerator())
        {
            foreach (var (key, _) in cur)
            {
                if (!baseKeys.ContainsKey(key)) { sawNew = true; continue; }
                if (sawNew) { inPlace = false; break; }

                string? next = null;
                while (expected.MoveNext())
                    if (cur.ContainsKey(expected.Current)) { next = expected.Current; break; }
                if (!string.Equals(next, key, StringComparison.Ordinal)) { inPlace = false; break; }
            }
        }

        if (inPlace) return null;

        var baseIndex = new Dictionary<string, int>(baseKeys.Count, StringComparer.Ordinal);
        foreach (var key in baseKeys.Keys) baseIndex[key] = baseIndex.Count;

        var result = new HashSet<string>(StringComparer.Ordinal);
        bool pastStable = false;
        int last = -1;
        foreach (var (key, _) in cur)
        {
            if (!baseIndex.TryGetValue(key, out int at)) { pastStable = true; continue; }
            if (!pastStable && at > last) { last = at; continue; }
            pastStable = true;
            result.Add(key);
        }

        return result;
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

    [ThreadStatic] private static System.Buffers.ArrayBufferWriter<byte>? t_buffer;
    [ThreadStatic] private static System.Text.Json.Utf8JsonWriter? t_writer;
    private const int KeepBufferBelow = 1 << 20;

    /// <summary>Impressão do JSON compacto do nó. Escrever o nó num buffer reusado não materializa o dicionário de cada
    /// objeto filho (a árvore vem do serializador e guarda o texto original): percorrer campo a campo materializava um
    /// dicionário por relação e era metade do custo de gravar um diff (#156).</summary>
    public static ulong Print(JsonNode? node)
    {
        if (node is null) return 1;

        var buffer = t_buffer ??= new System.Buffers.ArrayBufferWriter<byte>(512);
        buffer.ResetWrittenCount();
        var writer = t_writer ??= new System.Text.Json.Utf8JsonWriter(buffer, new System.Text.Json.JsonWriterOptions { SkipValidation = true });
        writer.Reset(buffer);
        node.WriteTo(writer);
        writer.Flush();

        ulong h = HashBytes(buffer.WrittenSpan);
        if (buffer.Capacity > KeepBufferBelow) { t_buffer = null; t_writer = null; }
        return h;
    }

    private static ulong HashBytes(ReadOnlySpan<byte> bytes)
    {
        ulong h = 14695981039346656037UL;
        int i = 0;
        for (; i + 8 <= bytes.Length; i += 8)
            Step(ref h, System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bytes[i..]));
        ulong tail = 0;
        for (int shift = 0; i < bytes.Length; i++, shift += 8)
            tail |= (ulong)bytes[i] << shift;
        Step(ref h, tail);
        Step(ref h, (ulong)bytes.Length);
        return h;
    }

    private static void Step(ref ulong h, ulong x)
    {
        h = (h ^ x) * 0x9E3779B97F4A7C15UL;
        h ^= h >> 32;
    }
}
