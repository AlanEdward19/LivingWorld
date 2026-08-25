using System.Text.Json.Nodes;

namespace LivingWorld.Simulation.Snapshot;

/// <summary>Cache volátil de fragmentos JSON canônicos (PERF-12). Não entra no snapshot.</summary>
internal sealed class CanonicalHashCache
{
    private readonly Dictionary<string, JsonNode?> _propertyNodes = new(StringComparer.Ordinal);
    private readonly Dictionary<long, JsonNode?> _npcNodes = new();
    private int _npcListVersion;
    private int _cachedNpcListVersion = -1;
    private JsonNode? _cachedNpcsArray;
    private int _npcCount = -1;

    public void InvalidateAll()
    {
        _propertyNodes.Clear();
        _npcNodes.Clear();
        _cachedNpcsArray = null;
        _cachedNpcListVersion = -1;
        _npcCount = -1;
        _npcListVersion++;
    }

    public void MarkPropertyDirty(string propertyName) => _propertyNodes.Remove(propertyName);

    public void MarkNpcDirty(long npcId)
    {
        _npcNodes.Remove(npcId);
        _cachedNpcsArray = null;
        _cachedNpcListVersion = -1;
    }

    public void MarkNpcsStructureDirty()
    {
        _npcNodes.Clear();
        _cachedNpcsArray = null;
        _cachedNpcListVersion = -1;
        _npcListVersion++;
    }

    public bool TryGetPropertyNode(string propertyName, out JsonNode? node) =>
        _propertyNodes.TryGetValue(propertyName, out node);

    public void StorePropertyNode(string propertyName, JsonNode? node) =>
        _propertyNodes[propertyName] = node;

    public bool TryGetNpcNode(long npcId, out JsonNode? node) =>
        _npcNodes.TryGetValue(npcId, out node);

    public void StoreNpcNode(long npcId, JsonNode? node) => _npcNodes[npcId] = node;

    public bool TryGetNpcsArray(int npcCount, out JsonNode? array)
    {
        if (_cachedNpcsArray is not null && _cachedNpcListVersion == _npcListVersion && _npcCount == npcCount)
        {
            array = _cachedNpcsArray;
            return true;
        }

        array = null;
        return false;
    }

    public void StoreNpcsArray(int npcCount, JsonNode array)
    {
        _cachedNpcsArray = array;
        _cachedNpcListVersion = _npcListVersion;
        _npcCount = npcCount;
    }
}
