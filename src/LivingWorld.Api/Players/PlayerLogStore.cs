using System.Text.Json;
using System.Text.Json.Serialization;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;

namespace LivingWorld.Api.Players;

/// <summary>O registro de comandos do jogador persistido ao lado do save (ADR-0048, AD-197a): <c>{mundo}.players.base.json</c> é o
/// snapshot do mundo antes do primeiro comando aceito desde que o mundo foi aberto, e <c>{mundo}.players.log</c> tem um comando
/// por linha (JSON), gravado e descarregado no disco antes da resposta. Reaplicar base + registro até o tick do save reproduz o
/// hash do mundo salvo, também depois de reiniciar o hospedeiro (<see cref="PlayerReplay"/>). Sem pasta (mundo em memória), só
/// guarda em memória. Não é estado do mundo: fora do snapshot e do hash. Chamado sob a trava do mundo.</summary>
public sealed class PlayerLogStore(string? directory)
{
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    private string? _worldKey;
    private WorldState? _world;
    private string? _base;
    private readonly List<PlayerCommandRecord> _records = [];

    public string? BaseSnapshot => _base;

    public IReadOnlyList<PlayerCommandRecord> Records => _records;

    /// <summary>Antes de aplicar um comando ao mundo <paramref name="worldKey"/>: na primeira vez desde que o mundo foi aberto
    /// (ou trocado), fixa a base com o mundo como está e começa um registro novo. Se o mundo já roda com Δ diferente de uma hora
    /// (ADR-0050), o registro começa com essa duração (sequência -1), porque o replay parte de uma hora.</summary>
    public void BeforeCommand(string worldKey, WorldState world, int tickDeltaMs = WorldMillis.HourTick)
    {
        if (_worldKey == worldKey && ReferenceEquals(_world, world) && _base is not null) return;
        _worldKey = worldKey;
        _world = world;
        _base = WorldSnapshot.Serialize(world);
        _records.Clear();
        if (directory is not null)
        {
            File.WriteAllText(BasePath(worldKey), _base);
            File.WriteAllText(LogPath(worldKey), "");
        }
        if (tickDeltaMs != WorldMillis.HourTick)
            Append(new PlayerCommandRecord(world.CurrentDate.TotalMilliseconds, -1, "host", PlayerCommandKind.TickDelta, DeltaMs: tickDeltaMs));
    }

    public void Append(PlayerCommandRecord record)
    {
        _records.Add(record);
        if (directory is null || _worldKey is null) return;
        using var stream = new FileStream(LogPath(_worldKey), FileMode.Append, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream);
        writer.WriteLine(JsonSerializer.Serialize(record, Json));
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    /// <summary>Lê base e registro gravados de um mundo (depois de reiniciar). Nulo se não há base.</summary>
    public static (string Base, List<PlayerCommandRecord> Records)? Read(string directory, string worldKey)
    {
        string basePath = Path.Combine(directory, $"{worldKey}.players.base.json");
        if (!File.Exists(basePath)) return null;
        string logPath = Path.Combine(directory, $"{worldKey}.players.log");
        var records = File.Exists(logPath)
            ? File.ReadLines(logPath).Where(l => l.Length > 0).Select(ReadRecord).ToList()
            : [];
        return (File.ReadAllText(basePath), records);
    }

    /// <summary>Uma linha do registro. A forma de antes da troca para ms (<c>DeltaSeconds</c>, ADR-0050) carrega convertida; o
    /// instante (<c>Tick</c>) já é lido em horas, em segundos ou em ms pelo conversor do campo.</summary>
    private static PlayerCommandRecord ReadRecord(string line)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(line)!.AsObject();
        if (node["DeltaSeconds"] is { } seconds && node["DeltaMs"] is null)
            node["DeltaMs"] = seconds.GetValue<int>() * WorldMillis.PerSecond;
        node.Remove("DeltaSeconds");
        return node.Deserialize<PlayerCommandRecord>(Json)!;
    }

    private string BasePath(string worldKey) => Path.Combine(directory!, $"{worldKey}.players.base.json");

    private string LogPath(string worldKey) => Path.Combine(directory!, $"{worldKey}.players.log");
}
