using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Snapshot;

/// <summary>O mundo serializado uma vez por gravação (#156): cada propriedade persistível (pública, sem
/// <see cref="EphemeralAttribute"/>, em ordem de nome) vai a UTF-8 num buffer do pool e é lida como documento. Daí saem o
/// bloco completo, o diff e os dois hashes, sem montar <c>JsonNode</c> nem serializar o mundo três vezes. Os bytes de cada
/// valor são os que o serializador escreve, os mesmos que a árvore de <see cref="WorldSnapshot.BuildPersistentTree"/> e os
/// hashes escreviam: o formato e os hashes não mudam. Propriedade canônica fria (regras, catálogos, o mapa) sai dos bytes
/// que o cache do hash canônico já guarda, com documento, trecho codificado e impressão também guardados.</summary>
internal sealed class WorldJsonCapture : IDisposable
{
    /// <summary>Compacto e com o codificador do serializador do snapshot: o texto que <c>ToJsonString</c> escreveria.</summary>
    internal static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = WorldSnapshot.SnapshotJsonOptions.Encoder,
        SkipValidation = true,
    };

    private sealed record Meta(
        PropertyInfo Property, byte[] HashPrefix, bool Ephemeral, bool Canonical, bool Volatile, bool OmitsNull, bool Cacheable);

    private static readonly Meta[] Metas = WorldSnapshot.ReflectedProperties.Select(p => new Meta(
        p, Encoding.UTF8.GetBytes($"\"{p.Name}\":"),
        p.GetCustomAttribute<EphemeralAttribute>() is not null,
        p.GetCustomAttribute<CanonicalAttribute>() is not null,
        p.GetCustomAttribute<VolatileAttribute>() is not null,
        WorldSnapshot.OmitsNull(p),
        p.GetCustomAttribute<CanonicalAttribute>() is not null && IncrementalHasher.IsCacheable(p.Name))).ToArray();

    private const byte Absent = 0, InPersistent = 1, InExtra = 2, InCache = 3, Omitted = 4;

    private readonly WorldState _world;
    private readonly PooledBufferWriter _persistent = new(1 << 20);
    private readonly Utf8JsonWriter _writer;
    private PooledBufferWriter? _extra;
    private readonly byte[] _where = new byte[Metas.Length];
    private readonly int[] _start = new int[Metas.Length], _length = new int[Metas.Length];
    private readonly byte[]?[] _cached = new byte[]?[Metas.Length];
    private readonly Npc[] _npcs;
    private readonly List<JsonDocument> _documents = [];
    private KeyValuePair<string, JsonElement>[]? _properties;
    private CachedProperty?[]? _artifacts;
    private Dictionary<Npc, JsonElement>? _npcElements;

    private WorldJsonCapture(WorldState world)
    {
        _world = world;
        _npcs = [.. world.Npcs];
        _writer = new Utf8JsonWriter(_persistent, WriterOptions);
        var cache = world.CanonicalHashCache;
        for (int i = 0; i < Metas.Length; i++)
        {
            var meta = Metas[i];
            if (meta.Ephemeral) continue;
            var value = meta.Property.GetValue(world);
            if (value is null && meta.OmitsNull) { _where[i] = Omitted; continue; }

            if (meta.Cacheable && cache.TryGetPropertyBytes(meta.Property.Name, out var bytes))
            {
                _where[i] = InCache;
                _cached[i] = bytes;
                continue;
            }

            Serialize(i, value, _persistent, WorldSnapshot.SnapshotJsonOptions);
            _where[i] = InPersistent;
            if (meta.Cacheable)
            {
                _cached[i] = ValueOf(i).ToArray();
                _where[i] = InCache;
                cache.StorePropertyBytes(meta.Property.Name, _cached[i]!);
            }
        }
    }

    public static WorldJsonCapture Of(WorldState world) => new(world);

    public WorldState World => _world;

    /// <summary>As propriedades persistíveis em ordem, cada valor lido como documento; valem enquanto a captura não for
    /// descartada. Propriedade fria reaproveita o documento do cache.</summary>
    public IReadOnlyList<KeyValuePair<string, JsonElement>> Properties
    {
        get
        {
            if (_properties is null) ParseProperties();
            return _properties!;
        }
    }

    /// <summary>Em paralelo a <see cref="Properties"/>: os derivados em cache de cada propriedade fria, nulo nas outras.</summary>
    public IReadOnlyList<CachedProperty?> Artifacts
    {
        get
        {
            if (_artifacts is null) ParseProperties();
            return _artifacts!;
        }
    }

    private void ParseProperties()
    {
        var cache = _world.CanonicalHashCache;
        var properties = new List<KeyValuePair<string, JsonElement>>(Metas.Length);
        var artifacts = new List<CachedProperty?>(Metas.Length);
        for (int i = 0; i < Metas.Length; i++)
        {
            if (_where[i] is not (InPersistent or InCache)) continue;
            string name = Metas[i].Property.Name;
            CachedProperty? cached = null;
            JsonElement element;
            if (_where[i] == InCache)
            {
                if (!cache.TryGetPropertyArtifacts(name, out cached) || !ReferenceEquals(cached.Bytes, _cached[i]))
                {
                    // o documento fica com o cache, sobre os mesmos bytes (sem cópia), enquanto eles valerem
                    cached = new CachedProperty(_cached[i]!, JsonDocument.Parse(_cached[i]).RootElement);
                    cache.StorePropertyArtifacts(name, cached);
                }

                element = cached.Element;
            }
            else
            {
                var document = JsonDocument.Parse(_persistent.WrittenMemory.Slice(_start[i], _length[i]));
                _documents.Add(document);
                element = document.RootElement;
            }

            properties.Add(new(name, element));
            artifacts.Add(cached);
        }

        _properties = [.. properties];
        _artifacts = [.. artifacts];
    }

    /// <summary>Hash canônico: SHA-256 do objeto <c>{"Nome":valor,...}</c> das propriedades canônicas, em ordem de nome.</summary>
    public string CanonicalHash() => HashOf(static m => m.Canonical, WorldSnapshot.SnapshotJsonOptions);

    /// <summary>Hash volátil, com as propriedades efêmeras voláteis serializadas agora. NPC que elas listam
    /// (<c>AliveNpcIndex</c>, <c>NpcWakeBatch</c>) é copiado do texto dos NPCs persistidos em vez de serializado de novo.</summary>
    public string VolatileHash()
    {
        t_npcSource = this;
        try
        {
            return HashOf(static m => m.Volatile, NpcReuseOptions);
        }
        finally
        {
            t_npcSource = null;
        }
    }

    /// <summary>Hash volátil sem captura: só as propriedades voláteis, serializadas uma a uma.</summary>
    public static string VolatileHashOf(WorldState world)
    {
        using var buffer = new PooledBufferWriter();
        using var writer = new Utf8JsonWriter(buffer, WriterOptions);
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha.AppendData("{"u8);
        bool first = true;
        foreach (var meta in Metas)
        {
            if (!meta.Volatile) continue;
            var value = meta.Property.GetValue(world);
            if (value is null && meta.OmitsNull) continue;
            if (!first) sha.AppendData(","u8);
            first = false;
            sha.AppendData(meta.HashPrefix);
            buffer.Clear();
            writer.Reset(buffer);
            JsonSerializer.Serialize(writer, value, meta.Property.PropertyType, WorldSnapshot.SnapshotJsonOptions);
            writer.Flush();
            sha.AppendData(buffer.WrittenSpan);
        }

        sha.AppendData("}"u8);
        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }

    private string HashOf(Func<Meta, bool> filter, JsonSerializerOptions extraOptions)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha.AppendData("{"u8);
        bool first = true;
        for (int i = 0; i < Metas.Length; i++)
        {
            if (!filter(Metas[i])) continue;
            if (_where[i] == Absent) SerializeExtra(i, extraOptions);
            if (_where[i] == Omitted) continue;
            if (!first) sha.AppendData(","u8);
            first = false;
            sha.AppendData(Metas[i].HashPrefix);
            sha.AppendData(ValueOf(i));
        }

        sha.AppendData("}"u8);
        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }

    private ReadOnlySpan<byte> ValueOf(int i) => _where[i] switch
    {
        InCache => _cached[i],
        InPersistent => _persistent.WrittenSpan.Slice(_start[i], _length[i]),
        _ => _extra!.WrittenSpan.Slice(_start[i], _length[i]),
    };

    private void Serialize(int i, object? value, PooledBufferWriter target, JsonSerializerOptions options)
    {
        _writer.Reset(target);
        int start = target.WrittenCount;
        JsonSerializer.Serialize(_writer, value, Metas[i].Property.PropertyType, options);
        _writer.Flush();
        _start[i] = start;
        _length[i] = target.WrittenCount - start;
    }

    private void SerializeExtra(int i, JsonSerializerOptions options)
    {
        var meta = Metas[i];
        var value = meta.Property.GetValue(_world);
        if (value is null && meta.OmitsNull) { _where[i] = Omitted; return; }

        _extra ??= new PooledBufferWriter(1 << 16);
        Serialize(i, value, _extra, options);
        _where[i] = InExtra;
    }

    private bool TryGetNpcElement(Npc npc, out JsonElement element)
    {
        if (_npcElements is null)
        {
            _npcElements = new Dictionary<Npc, JsonElement>(_npcs.Length, ReferenceEqualityComparer.Instance);
            var array = Properties.FirstOrDefault(static p => p.Key == nameof(WorldState.Npcs)).Value;
            if (array.ValueKind == JsonValueKind.Array && array.GetArrayLength() == _npcs.Length)
            {
                int k = 0;
                foreach (var item in array.EnumerateArray()) _npcElements[_npcs[k++]] = item;
            }
        }

        return _npcElements.TryGetValue(npc, out element);
    }

    [ThreadStatic] private static WorldJsonCapture? t_npcSource;

    private static readonly JsonSerializerOptions NpcReuseOptions = CreateNpcReuseOptions();

    private static JsonSerializerOptions CreateNpcReuseOptions()
    {
        var options = new JsonSerializerOptions(WorldSnapshot.SnapshotJsonOptions);
        options.Converters.Add(new NpcReuseConverter());
        return options;
    }

    /// <summary>NPC já presente nas propriedades persistidas sai com os bytes que o serializador escreveu lá; o resto (ou
    /// fora de uma captura) é serializado normalmente.</summary>
    private sealed class NpcReuseConverter : JsonConverter<Npc>
    {
        public override Npc Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException("só escrita");

        public override void Write(Utf8JsonWriter writer, Npc value, JsonSerializerOptions options)
        {
            if (t_npcSource is { } source && source.TryGetNpcElement(value, out var element))
                writer.WriteRawValue(JsonMarshal.GetRawUtf8Value(element), skipInputValidation: true);
            else
                JsonSerializer.Serialize(writer, value, WorldSnapshot.SnapshotJsonOptions);
        }
    }

    public void Dispose()
    {
        foreach (var document in _documents) document.Dispose();
        _documents.Clear();
        _properties = null;
        _artifacts = null;
        _npcElements = null;
        _writer.Dispose();
        _extra?.Dispose();
        _persistent.Dispose();
    }
}
