using System.Text.Json;

namespace LivingWorld.Simulation.Snapshot;

/// <summary>Cache volátil dos bytes JSON canônicos por propriedade fria e por NPC (o texto que o serializador escreve).
/// Não entra no snapshot. Guardar bytes em vez de <c>JsonNode</c> poupa a cópia profunda a cada hash (#156).</summary>
internal sealed class CanonicalHashCache
{
    private readonly Dictionary<string, byte[]> _propertyBytes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CachedProperty> _propertyArtifacts = new(StringComparer.Ordinal);
    private readonly Dictionary<long, byte[]> _npcBytes = new();

    public void InvalidateAll()
    {
        _propertyBytes.Clear();
        _propertyArtifacts.Clear();
        _npcBytes.Clear();
    }

    public void MarkPropertyDirty(string propertyName)
    {
        _propertyBytes.Remove(propertyName);
        _propertyArtifacts.Remove(propertyName);
    }

    public void MarkNpcDirty(long npcId) => _npcBytes.Remove(npcId);

    public void MarkNpcsStructureDirty() => _npcBytes.Clear();

    public bool TryGetPropertyBytes(string propertyName, out byte[] bytes) =>
        _propertyBytes.TryGetValue(propertyName, out bytes!);

    public void StorePropertyBytes(string propertyName, byte[] bytes)
    {
        _propertyBytes[propertyName] = bytes;
        _propertyArtifacts.Remove(propertyName);
    }

    /// <summary>O que a gravação do snapshot já derivou dos mesmos bytes (documento, trecho codificado, impressão), para
    /// não refazer a cada gravação o trabalho sobre o mapa e as regras.</summary>
    public bool TryGetPropertyArtifacts(string propertyName, out CachedProperty artifacts) =>
        _propertyArtifacts.TryGetValue(propertyName, out artifacts!);

    public void StorePropertyArtifacts(string propertyName, CachedProperty artifacts) =>
        _propertyArtifacts[propertyName] = artifacts;

    public bool TryGetNpcBytes(long npcId, out byte[] bytes) => _npcBytes.TryGetValue(npcId, out bytes!);

    public void StoreNpcBytes(long npcId, byte[] bytes) => _npcBytes[npcId] = bytes;
}

/// <summary>Derivados de uma propriedade fria, válidos enquanto os bytes dela no cache valem (<see cref="Bytes"/> é o
/// próprio array guardado): o documento sobre esses bytes, o trecho já codificado pelo codec posicional e a impressão.</summary>
internal sealed class CachedProperty(byte[] bytes, JsonElement element)
{
    public byte[] Bytes { get; } = bytes;
    public JsonElement Element { get; } = element;
    public PositionalTreeCodec.Segment? Encoded { get; set; }
    public object? Print { get; set; }
}
