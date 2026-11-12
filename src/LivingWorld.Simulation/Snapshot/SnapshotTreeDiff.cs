using System.Runtime.InteropServices;
using System.Text.Json;
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
    private const string FullKey = "$full";

    /// <summary>Escreve em <paramref name="output"/> o JSON do diff de <paramref name="current"/> (a árvore persistível já
    /// em texto) sobre <paramref name="baseline"/>. Percorre o documento sem materializar nó: o que entra no diff é copiado
    /// cru, e o codec só vê a árvore lógica, a mesma que o diff sobre <see cref="JsonNode"/> montava (#156).</summary>
    public static void Diff(
        IReadOnlyList<KeyValuePair<string, JsonElement>> current, SnapshotFingerprint baseline, IReadOnlySet<long>? dirtyNpcIds,
        PooledBufferWriter output, IReadOnlyList<CachedProperty?>? cached = null)
    {
        using var sections = new DiffSections();
        for (int i = 0; i < current.Count; i++)
        {
            var (name, value) = current[i];
            if (cached?[i]?.Print is { } print && Unchanged(name, print, baseline)) continue;
            if (value.ValueKind == JsonValueKind.Array && baseline.Entities.TryGetValue(name, out var baseEntities)
                && TryIndex(value, out var curIds))
                DiffEntities(name, value, curIds, baseEntities, name == "Npcs" ? dirtyNpcIds : null, sections);
            else if (value.ValueKind == JsonValueKind.Object && baseline.Keys.TryGetValue(name, out var baseKeys))
                DiffKeys(name, value, baseKeys, sections);
            else if (!baseline.Props.TryGetValue(name, out var propPrint) || propPrint != SnapshotFingerprint.Print(value))
            {
                sections.Props.WritePropertyName(name);
                WriteRaw(sections.Props, value);
            }
        }

        sections.WriteTo(output);
    }

    /// <summary>Propriedade fria cuja impressão em cache é a da base (o mesmo objeto, logo os mesmos bytes): o diff dela sai
    /// vazio sem percorrê-la. Entidade com campos fora do formato da primeira (impressão nula) sempre entra no diff, então
    /// nesse caso não se pula.</summary>
    private static bool Unchanged(string name, object print, SnapshotFingerprint baseline) => print switch
    {
        SnapshotFingerprint.EntityPrints entities => baseline.Entities.TryGetValue(name, out var b) && ReferenceEquals(b, entities)
            && entities.ById.Values.All(static p => p is not null),
        Dictionary<string, ulong> keys => baseline.Keys.TryGetValue(name, out var b) && ReferenceEquals(b, keys),
        ulong p => baseline.Props.TryGetValue(name, out var b) && b == p,
        _ => false,
    };

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
                if (obj.ContainsKey(FullKey))
                {
                    var whole = obj.DeepClone().AsObject();
                    whole.Remove(FullKey);
                    if (byId.TryGetValue(id, out int slot)) target[slot] = whole;
                    else target.Add(whole);
                }
                else if (byId.TryGetValue(id, out int at))
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
        string name, JsonElement cur, Dictionary<long, int> curIds, SnapshotFingerprint.EntityPrints baseEntities,
        IReadOnlySet<long>? dirty, DiffSections sections)
    {
        // curIds cobre todo elemento (TryIndex falha se algum não tem id), na ordem do array
        var currentOrder = new long[curIds.Count];
        foreach (var (id, at) in curIds) currentOrder[at] = id;

        var changed = sections.Scratch();
        changed.WriteStartArray();
        int changedCount = 0, index = 0;
        foreach (var entry in cur.EnumerateArray())
        {
            long id = currentOrder[index++];
            if (!baseEntities.ById.TryGetValue(id, out var prints))
            {
                WriteRaw(changed, entry);
                changedCount++;
                continue;
            }

            if (dirty is not null && !dirty.Contains(id)) continue;

            bool started = false;
            int f = 0;
            bool sameShape = prints is not null && entry.GetPropertyCount() == baseEntities.Fields.Length;
            if (sameShape)
            {
                int n = 0;
                foreach (var field in entry.EnumerateObject())
                    if (!field.NameEquals(baseEntities.Fields[n++])) { sameShape = false; break; }
            }

            // Forma diferente da base: algum campo pode ter sumido (campo omitido quando volta ao padrão). O diff campo a
            // campo deixaria o valor antigo na base, então a entidade vai inteira, marcada para substituir.
            if (!sameShape)
            {
                changed.WriteStartObject();
                changed.WriteBoolean(FullKey, true);
                foreach (var field in entry.EnumerateObject())
                {
                    WriteName(changed, field);
                    WriteRaw(changed, field.Value);
                }

                changed.WriteEndObject();
                changedCount++;
                continue;
            }

            foreach (var field in entry.EnumerateObject())
            {
                if (prints![f] == SnapshotFingerprint.Print(field.Value))
                {
                    f++;
                    continue;
                }

                f++;
                if (field.NameEquals("Id")) continue;
                if (!started)
                {
                    changed.WriteStartObject();
                    changed.WritePropertyName("Id");
                    WriteRaw(changed, entry.GetProperty("Id"));
                    started = true;
                }

                WriteName(changed, field);
                WriteRaw(changed, field.Value);
            }

            if (started)
            {
                changed.WriteEndObject();
                changedCount++;
            }
        }

        changed.WriteEndArray();
        if (changedCount > 0)
        {
            sections.Entities.WritePropertyName(name);
            sections.Entities.WriteRawValue(sections.ScratchBytes(), skipInputValidation: true);
        }

        bool anyGone = false;
        foreach (var id in baseEntities.Order)
        {
            if (curIds.ContainsKey(id)) continue;
            if (!anyGone)
            {
                sections.Removed.WritePropertyName(name);
                sections.Removed.WriteStartArray();
                anyGone = true;
            }

            sections.Removed.WriteNumberValue(id);
        }

        if (anyGone) sections.Removed.WriteEndArray();

        // ordem esperada ao aplicar: base sem os removidos, depois os novos na ordem atual
        var expected = baseEntities.Order.Where(curIds.ContainsKey)
            .Concat(currentOrder.Where(id => !baseEntities.ById.ContainsKey(id)));
        if (!expected.SequenceEqual(currentOrder))
        {
            sections.Order.WritePropertyName(name);
            sections.Order.WriteStartArray();
            foreach (var id in currentOrder) sections.Order.WriteNumberValue(id);
            sections.Order.WriteEndArray();
        }
    }

    private static void DiffKeys(string name, JsonElement cur, Dictionary<string, ulong> baseKeys, DiffSections sections)
    {
        var curKeys = new List<string>();
        foreach (var property in cur.EnumerateObject()) curKeys.Add(property.Name);
        var curSet = new HashSet<string>(curKeys, StringComparer.Ordinal);
        var movedKeys = MovedKeysOf(curKeys, curSet, baseKeys);

        var changed = sections.Scratch();
        changed.WriteStartObject();
        bool anyChanged = false;
        int k = 0;
        foreach (var property in cur.EnumerateObject())
        {
            string key = curKeys[k++];
            if (baseKeys.TryGetValue(key, out var print) && print == SnapshotFingerprint.Print(property.Value)
                && (movedKeys is null || !movedKeys.Contains(key))) continue;
            changed.WritePropertyName(key);
            WriteRaw(changed, property.Value);
            anyChanged = true;
        }

        changed.WriteEndObject();
        if (anyChanged)
        {
            sections.Keys.WritePropertyName(name);
            sections.Keys.WriteRawValue(sections.ScratchBytes(), skipInputValidation: true);
        }

        if (movedKeys is { Count: > 0 })
        {
            sections.Moved.WritePropertyName(name);
            sections.Moved.WriteStartArray();
            foreach (var key in movedKeys.OrderBy(x => x, StringComparer.Ordinal)) sections.Moved.WriteStringValue(key);
            sections.Moved.WriteEndArray();
            sections.MovedCount++;
        }

        bool anyGone = false;
        foreach (var key in baseKeys.Keys)
        {
            if (curSet.Contains(key)) continue;
            if (!anyGone)
            {
                sections.Removed.WritePropertyName(name);
                sections.Removed.WriteStartArray();
                anyGone = true;
            }

            sections.Removed.WriteStringValue(key);
        }

        if (anyGone) sections.Removed.WriteEndArray();
    }

    /// <summary>Chaves da base que, na árvore atual, não estão mais onde aplicar o diff as deixaria (saíram e voltaram, e
    /// por isso foram para o fim). Aplicar o diff mantém as chaves da base na ordem da base e acrescenta as novas no fim;
    /// qualquer chave da base que apareça depois de uma chave nova, ou fora da ordem da base, foi movida. <c>null</c> no
    /// caso comum (nenhuma), decidido numa passada sem alocar o índice.</summary>
    private static HashSet<string>? MovedKeysOf(List<string> cur, HashSet<string> curSet, Dictionary<string, ulong> baseKeys)
    {
        bool inPlace = true, sawNew = false;
        using (var expected = baseKeys.Keys.GetEnumerator())
        {
            foreach (var key in cur)
            {
                if (!baseKeys.ContainsKey(key)) { sawNew = true; continue; }
                if (sawNew) { inPlace = false; break; }

                string? next = null;
                while (expected.MoveNext())
                    if (curSet.Contains(expected.Current)) { next = expected.Current; break; }
                if (!string.Equals(next, key, StringComparison.Ordinal)) { inPlace = false; break; }
            }
        }

        if (inPlace) return null;

        var baseIndex = new Dictionary<string, int>(baseKeys.Count, StringComparer.Ordinal);
        foreach (var key in baseKeys.Keys) baseIndex[key] = baseIndex.Count;

        var result = new HashSet<string>(StringComparer.Ordinal);
        bool pastStable = false;
        int last = -1;
        foreach (var key in cur)
        {
            if (!baseIndex.TryGetValue(key, out int at)) { pastStable = true; continue; }
            if (!pastStable && at > last) { last = at; continue; }
            pastStable = true;
            result.Add(key);
        }

        return result;
    }

    private static void WriteRaw(Utf8JsonWriter writer, JsonElement value) =>
        writer.WriteRawValue(JsonMarshal.GetRawUtf8Value(value), skipInputValidation: true);

    private static void WriteName(Utf8JsonWriter writer, JsonProperty property)
    {
        var raw = JsonMarshal.GetRawUtf8PropertyName(property);
        if (raw.IndexOf((byte)'\\') >= 0) writer.WritePropertyName(property.Name);
        else writer.WritePropertyName(raw);
    }

    /// <summary>As seções do diff, cada uma num buffer próprio, alimentadas na ordem das propriedades do mundo.</summary>
    private sealed class DiffSections : IDisposable
    {
        private const int SectionCount = 6, ScratchSlot = 6;
        private static readonly string[] Names = [EntitiesKey, KeysKey, RemovedKey, PropsKey, OrderKey, MovedKey];
        private readonly PooledBufferWriter[] _buffers = new PooledBufferWriter[SectionCount + 1];
        private readonly Utf8JsonWriter[] _writers = new Utf8JsonWriter[SectionCount + 1];

        public DiffSections()
        {
            for (int i = 0; i <= SectionCount; i++)
            {
                _buffers[i] = new PooledBufferWriter(1 << 12);
                _writers[i] = new Utf8JsonWriter(_buffers[i], WorldJsonCapture.WriterOptions);
                if (i < SectionCount) _writers[i].WriteStartObject();
            }
        }

        public Utf8JsonWriter Entities => _writers[0];
        public Utf8JsonWriter Keys => _writers[1];
        public Utf8JsonWriter Removed => _writers[2];
        public Utf8JsonWriter Props => _writers[3];
        public Utf8JsonWriter Order => _writers[4];
        public Utf8JsonWriter Moved => _writers[5];
        public int MovedCount { get; set; }

        /// <summary>Rascunho de uma entrada (array de entidades ou objeto de chaves) que só entra se não ficar vazia.</summary>
        public Utf8JsonWriter Scratch()
        {
            _buffers[ScratchSlot].Clear();
            _writers[ScratchSlot].Reset(_buffers[ScratchSlot]);
            return _writers[ScratchSlot];
        }

        public ReadOnlySpan<byte> ScratchBytes()
        {
            _writers[ScratchSlot].Flush();
            return _buffers[ScratchSlot].WrittenSpan;
        }

        public void WriteTo(PooledBufferWriter output)
        {
            using var writer = new Utf8JsonWriter(output, WorldJsonCapture.WriterOptions);
            writer.WriteStartObject();
            for (int i = 0; i < SectionCount; i++)
            {
                if (i == 5 && MovedCount == 0) continue; // só existe quando alguma chave saiu e voltou: diff antigo não tem a seção
                _writers[i].WriteEndObject();
                _writers[i].Flush();
                writer.WritePropertyName(Names[i]);
                writer.WriteRawValue(_buffers[i].WrittenSpan, skipInputValidation: true);
            }

            writer.WriteEndObject();
        }

        public void Dispose()
        {
            foreach (var w in _writers) w.Dispose();
            foreach (var b in _buffers) b.Dispose();
        }
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

    /// <summary>A mesma regra de <see cref="TryIndex(JsonArray, out Dictionary{long, int})"/> sobre o documento.</summary>
    internal static bool TryIndex(JsonElement array, out Dictionary<long, int> index)
    {
        int length = array.GetArrayLength();
        index = new Dictionary<long, int>(length);
        if (length == 0) return false;
        int i = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("Id", out var id)
                || id.ValueKind != JsonValueKind.Object || !id.TryGetProperty("Value", out var v)
                || v.ValueKind != JsonValueKind.Number || !v.TryGetInt64(out long value) || !index.TryAdd(value, i))
                return false;
            i++;
        }

        return true;
    }
}

/// <summary>Impressões digitais de 64 bits de uma árvore de snapshot, o bastante para dizer o que mudou sem guardar a
/// árvore: por campo de cada entidade, por chave de cada dicionário e por propriedade solta. Só vale no processo que a
/// calculou, e só para comparar com árvores do mesmo formato. A impressão é o hash do JSON compacto do valor.</summary>
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

    /// <summary>Impressões de uma árvore em memória (a base lida do banco): vai a texto compacto, como o serializador o
    /// escreve, e segue pelo documento.</summary>
    public static SnapshotFingerprint Of(JsonObject tree)
    {
        using var json = new PooledBufferWriter(1 << 20);
        using (var writer = new Utf8JsonWriter(json, WorldJsonCapture.WriterOptions)) tree.WriteTo(writer);
        using var document = JsonDocument.Parse(json.WrittenMemory);
        var properties = new List<KeyValuePair<string, JsonElement>>();
        foreach (var property in document.RootElement.EnumerateObject()) properties.Add(new(property.Name, property.Value));
        return Of(properties);
    }

    /// <summary>Impressões das propriedades dadas; <paramref name="cached"/>, em paralelo, guarda e reaproveita a impressão
    /// de propriedade fria (o mapa), que não muda enquanto o cache vale. A base só lê as impressões: compartilhar é seguro.</summary>
    public static SnapshotFingerprint Of(
        IReadOnlyList<KeyValuePair<string, JsonElement>> tree, IReadOnlyList<CachedProperty?>? cached = null)
    {
        var result = new SnapshotFingerprint();
        for (int i = 0; i < tree.Count; i++)
        {
            var (name, value) = tree[i];
            var artifacts = cached?[i];
            object print = artifacts?.Print ?? PrintProperty(value);
            if (artifacts is not null) artifacts.Print = print;
            switch (print)
            {
                case EntityPrints entities: result.Entities[name] = entities; break;
                case Dictionary<string, ulong> keys: result.Keys[name] = keys; break;
                default: result.Props[name] = (ulong)print; break;
            }
        }

        return result;
    }

    private static object PrintProperty(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array && SnapshotTreeDiff.TryIndex(value, out var ids))
            return PrintEntities(value, ids);
        if (value.ValueKind == JsonValueKind.Object)
        {
            var keys = new Dictionary<string, ulong>();
            foreach (var child in value.EnumerateObject()) keys[child.Name] = Print(child.Value);
            return keys;
        }

        return Print(value);
    }

    private static EntityPrints PrintEntities(JsonElement array, Dictionary<long, int> ids)
    {
        var order = new long[ids.Count];
        foreach (var (id, at) in ids) order[at] = id;

        var prints = new EntityPrints { Order = new List<long>(order) };
        bool firstRow = true;
        int index = 0;
        foreach (var entry in array.EnumerateArray())
        {
            if (firstRow)
            {
                var names = new List<string>();
                foreach (var field in entry.EnumerateObject()) names.Add(field.Name);
                prints.Fields = [.. names];
                firstRow = false;
            }

            long id = order[index++];
            int count = entry.GetPropertyCount();
            if (count != prints.Fields.Length) { prints.ById[id] = null; continue; }

            var fields = new ulong[count];
            int f = 0;
            bool same = true;
            foreach (var field in entry.EnumerateObject())
            {
                if (!field.NameEquals(prints.Fields[f])) { same = false; break; }
                fields[f++] = Print(field.Value);
            }

            prints.ById[id] = same ? fields : null;
        }

        return prints;
    }

    /// <summary>Impressão do JSON compacto do valor, lida direto do texto do documento; <c>null</c> tem impressão própria.</summary>
    public static ulong Print(JsonElement value) =>
        value.ValueKind == JsonValueKind.Null ? 1 : HashBytes(JsonMarshal.GetRawUtf8Value(value));

    internal static ulong HashBytes(ReadOnlySpan<byte> bytes)
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
