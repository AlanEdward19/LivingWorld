using System.Text.Json;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure;
using LivingWorld.Infrastructure.EfCore;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Simulation.Snapshot;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LivingWorld.Api.World;

/// <summary>Metadado de um mundo salvo (arquivo <c>{id}.json</c>), fora do estado simulado. Os campos do AD-175 faltam nos
/// arquivos gravados antes dele (nulos ao ler) e são preenchidos na próxima gravação do mundo.</summary>
public sealed record SavedWorldInfo(Guid WorldId, string Name, ulong Seed, long CreatedAt, long CurrentDate, long Population, DateTimeOffset LastSavedAtUtc,
    string? PeriodId = null, long? Year = null, string? Origin = null, DateTimeOffset? LastPlayedAtUtc = null);

/// <summary>Item de <c>GET /worlds</c> (AD-175): os campos de antes mais período, ano, quando foi jogado, se é o mundo aberto
/// e a origem (<see cref="SavedWorldLibrary.PlayerOrigin"/> ou <see cref="SavedWorldLibrary.AutomaticOrigin"/>).</summary>
public sealed record SavedWorldSummary(Guid WorldId, string Name, ulong Seed, long CreatedAt, long CurrentDate, long Population,
    DateTimeOffset LastSavedAtUtc, string PeriodId, string PeriodName, long Year, DateTimeOffset LastPlayedAtUtc, bool IsCurrent, string Origin);

public enum RenameOutcome { Renamed, NotFound, Duplicate }

/// <summary>Catálogo operacional; cada entrada reutiliza o formato de snapshot e o repositório existentes.</summary>
public sealed class SavedWorldLibrary : IDisposable
{
    private sealed record Entry(SqliteConnection Connection, WorldDbContext Context, IWorldRepository Repository, PersistentWorldRunner Runner, ColdTierPersistence? ColdTier);
    private readonly string? _directory;
    private readonly Dictionary<Guid, SavedWorldInfo> _info = new();
    private readonly Dictionary<Guid, Entry> _entries = new();
    private readonly BufferingWorldEventSink _sink;
    private Guid _current;
    public const string PlayerOrigin = "player";
    public const string AutomaticOrigin = "automatic";
    /// <summary>Seed do mundo inicial que a API cria sozinha (<c>Program.cs</c>): só decide a origem de um arquivo gravado antes
    /// do AD-175, que não a guardava.</summary>
    public const ulong BootstrapSeed = 1;
    public object Gate { get; } = new();

    /// <summary>Pasta dos saves (nula em memória): o registro de comandos do jogador fica ao lado (AD-197a, ADR-0048).</summary>
    public string? WorldsDirectory => _directory;
    public Guid CurrentId { get { lock (Gate) return _current; } }
    public PersistentWorldRunner Runner => _entries[_current].Runner;
    public IWorldRepository Repository => _entries[_current].Repository;

    public SavedWorldLibrary(string? directory, BufferingWorldEventSink sink)
    {
        _directory = directory;
        _sink = sink;
        if (directory is null) return;
        Directory.CreateDirectory(directory);
        foreach (string file in Directory.EnumerateFiles(directory, "*.json"))
        {
            // #222: só o cartão do mundo ({id}.json); o registro do jogador ({id}.players.base.json) mora na mesma pasta e não é cartão
            if (!Guid.TryParse(Path.GetFileNameWithoutExtension(file), out _)) continue;
            var info = JsonSerializer.Deserialize<SavedWorldInfo>(File.ReadAllText(file));
            if (info is not null && File.Exists(DatabasePath(info.WorldId))) _info.Add(info.WorldId, info);
        }
    }

    public WorldState Initialize(WorldState legacy, IWorldRepository legacyRepository)
    {
        lock (Gate)
        {
            if (_info.Count > 0)
            {
                var active = List()[0].WorldId;
                if (_directory is not null && File.Exists(Path.Combine(_directory, "current"))
                    && Guid.TryParse(File.ReadAllText(Path.Combine(_directory, "current")), out var marked) && _info.ContainsKey(marked)) active = marked;
                var restored = Open(active).Runner.LoadLatest() ?? throw new InvalidDataException("Mundo salvo sem snapshot.");
                _current = active;
                ApplyStoredName(active, restored);
                Touch(restored, saved: false);
                return restored;
            }
            _current = WorldIdentity.WorldIdFor(legacy.Seed);
            var entry = Open(_current);
            // Importa pelo contrato do repositório, sem copiar tabelas nem usar backup exclusivo do provider.
            var events = legacyRepository.LoadEvents(BranchId.Root).Select(e => new WorldEvent(
                e.Tick, Enum.Parse<WorldEventKind>(e.Kind), e.Payload, e.EventId ?? 0, e.CauseEventId, e.SourceSystem ?? "Unknown")).ToArray();
            entry.Repository.SaveSnapshotWithEvents(BranchId.Root, legacy.CurrentDate.TotalHours, "",
                WorldSnapshot.CanonicalHash(legacy), WorldSnapshot.VolatileHash(legacy), events, PositionalSnapshot.EncodeFull(legacy, out _));
            entry.ColdTier?.Save(legacy.ColdArchive.AllNpcSummaries, legacy.Calendar);
            RecordInfo(legacy, AutomaticOrigin);
            return legacy;
        }
    }

    public IReadOnlyList<SavedWorldInfo> List()
    {
        lock (Gate) return _info.Values.OrderByDescending(w => w.LastSavedAtUtc).ThenBy(w => w.WorldId).ToArray();
    }
    /// <summary>Lista de <c>GET /worlds</c>; <paramref name="periodName"/> traduz o id do período no nome do cartão.</summary>
    public IReadOnlyList<SavedWorldSummary> Summaries(Func<string, string> periodName)
    {
        lock (Gate)
            return List().Select(w =>
            {
                string period = w.PeriodId ?? "";
                return new SavedWorldSummary(w.WorldId, w.Name, w.Seed, w.CreatedAt, w.CurrentDate, w.Population, w.LastSavedAtUtc,
                    period, period == "" ? "" : periodName(period),
                    w.Year ?? w.CurrentDate / LivingWorld.Simulation.Scenarios.ScenarioRunner.DefaultCalendar.HoursPerYear,
                    w.LastPlayedAtUtc ?? w.LastSavedAtUtc, w.WorldId == _current,
                    w.Origin ?? (w.Seed == BootstrapSeed ? AutomaticOrigin : PlayerOrigin));
            }).ToArray();
    }

    /// <summary>Grava o mundo aberto agora (o mesmo instantâneo do laço a cada 24 ticks) e devolve o metadado gravado.</summary>
    public SavedWorldInfo SaveNow(WorldState world)
    {
        lock (Gate)
        {
            Save(world);
            return _info[WorldIdentity.WorldIdFor(world.Seed)];
        }
    }

    /// <summary>Troca o nome de um mundo salvo. O nome é metadado volátil (fora do hash canônico): no mundo aberto vale na
    /// hora; num mundo fechado fica no catálogo e é aplicado ao carregá-lo.</summary>
    public RenameOutcome Rename(Guid id, string name, WorldHost host)
    {
        lock (Gate)
        {
            if (!_info.TryGetValue(id, out var info)) return RenameOutcome.NotFound;
            if (_info.Values.Any(w => w.WorldId != id && string.Equals(w.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)))
                return RenameOutcome.Duplicate;
            if (id == _current) host.Current.Rename(name);
            var renamed = info with { Name = name };
            if (_directory is not null) WriteAtomic(Path.Combine(_directory, $"{id:D}.json"), JsonSerializer.Serialize(renamed));
            _info[id] = renamed;
            return RenameOutcome.Renamed;
        }
    }

    public SavedWorldSummary? Summary(Guid id, Func<string, string> periodName) =>
        Summaries(periodName).FirstOrDefault(w => w.WorldId == id);

    public bool Contains(ulong seed)
    {
        lock (Gate) return _info.ContainsKey(WorldIdentity.WorldIdFor(seed));
    }
    public void Save(WorldState world)
    {
        lock (Gate)
        {
            var id = WorldIdentity.WorldIdFor(world.Seed);
            Open(id);
            _current = id;
            Runner.Snapshot(world, _sink);
            RecordInfo(world);
        }
    }

    private void RecordInfo(WorldState world, string? origin = null)
    {
        var id = WorldIdentity.WorldIdFor(world.Seed);
        var now = DateTimeOffset.UtcNow;
        _info.TryGetValue(id, out var previous);
        var info = new SavedWorldInfo(id, world.Name, world.Seed, 0, world.CurrentDate.TotalHours,
                world.Npcs.Count(n => n.IsAlive) + world.Cities.Sum(c => c.AggregatePool.Count), now,
                world.PeriodId, world.CurrentDate.Year,
                origin ?? previous?.Origin ?? (world.Seed == BootstrapSeed ? AutomaticOrigin : PlayerOrigin), now);
        Write(info);
    }

    /// <summary>Ao abrir um mundo sem gravá-lo (início da API): marca quando foi jogado e completa período e ano.</summary>
    private void Touch(WorldState world, bool saved)
    {
        var id = WorldIdentity.WorldIdFor(world.Seed);
        if (saved || !_info.TryGetValue(id, out var info)) return;
        Write(info with
        {
            PeriodId = world.PeriodId,
            Year = world.CurrentDate.Year,
            LastPlayedAtUtc = DateTimeOffset.UtcNow,
            Origin = info.Origin ?? (world.Seed == BootstrapSeed ? AutomaticOrigin : PlayerOrigin),
        });
    }

    /// <summary>Nome dado em "Renomear" com o mundo fechado: o catálogo vale sobre o nome guardado no instantâneo.</summary>
    private void ApplyStoredName(Guid id, WorldState world)
    {
        if (_info.TryGetValue(id, out var info) && info.Name != world.Name) world.Rename(info.Name);
    }

    private void Write(SavedWorldInfo info)
    {
        var id = info.WorldId;
        if (_directory is not null)
        {
            WriteAtomic(Path.Combine(_directory, $"{id:D}.json"), JsonSerializer.Serialize(info));
            WriteAtomic(Path.Combine(_directory, "current"), id.ToString("D"));
        }
        _info[id] = info;
    }

    public bool Create(WorldHost host, SimulationHost simulation, WorldState world, WorldClock clock)
    {
        lock (Gate)
        {
            if (Contains(world.Seed)) return false;
            Save(host.Current);
            var id = WorldIdentity.WorldIdFor(world.Seed);
            var entry = Open(id);
            // A falha de persistência do novo mundo não troca o mundo corrente nem seu identificador.
            var preparedClock = new WorldClock(clock.Systems, clock.MaxIterationsPerTick, _sink);
            entry.Runner.Snapshot(world, new BufferingWorldEventSink());
            RecordInfo(world, PlayerOrigin);
            _current = id;
            host.Replace(world, preparedClock);
            simulation.Resume(); // descarta o teto de pausa pertencente ao mundo anterior
            simulation.Pause();
            return true;
        }
    }
    public bool Load(Guid id, WorldHost host, SimulationHost simulation, Func<WorldState, WorldClock> clockFor)
    {
        lock (Gate)
        {
            if (!_info.ContainsKey(id)) return false;
            if (id == _current) { Save(host.Current); simulation.Resume(); simulation.Pause(); return true; }
            var restored = Open(id).Runner.LoadLatest() ?? throw new InvalidDataException("Mundo salvo sem snapshot.");
            var preparedClock = clockFor(restored);
            Save(host.Current);
            ApplyStoredName(id, restored);
            RecordInfo(restored);
            _current = id;
            host.Replace(restored, preparedClock);
            simulation.Resume();
            simulation.Pause();
            return true;
        }
    }
    public int Delete(Guid id)
    {
        lock (Gate)
        {
            if (id == _current) return 409;
            if (!_info.ContainsKey(id)) return 404;
            if (_entries.Remove(id, out var entry)) { entry.Context.Dispose(); entry.Connection.Dispose(); }
            if (_directory is not null)
            {
                // Remover metadados primeiro torna a exclusão invisível ao catálogo mesmo se a limpeza for interrompida.
                File.Delete(Path.Combine(_directory, $"{id:D}.json"));
                foreach (string suffix in new[] { "", "-wal", "-shm", ".coldtier" }) File.Delete(DatabasePath(id) + suffix);
            }
            _info.Remove(id);
            return 204;
        }
    }
    private Entry Open(Guid id)
    {
        if (_entries.TryGetValue(id, out var found)) return found;
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _directory is null ? ":memory:" : DatabasePath(id),
            Pooling = false,
        }.ToString());
        connection.Open();
        var context = new WorldDbContext(new DbContextOptionsBuilder<WorldDbContext>().UseSqlite(connection).Options);
        context.Database.Migrate();
        var repository = new SqliteWorldRepository(context, resetOnTickZero: false);
        var cold = _directory is null ? null : new ColdTierPersistence(DatabasePath(id) + ".coldtier");
        found = new(connection, context, repository, new PersistentWorldRunner(repository, BranchId.Root, 24, cold), cold);
        _entries.Add(id, found);
        return found;
    }
    private string DatabasePath(Guid id) => Path.Combine(_directory!, $"{id:D}.db");
    private static void WriteAtomic(string path, string text)
    {
        File.WriteAllText(path + ".tmp", text);
        File.Move(path + ".tmp", path, overwrite: true);
    }
    public void Dispose()
    {
        foreach (var entry in _entries.Values) { entry.Context.Dispose(); entry.Connection.Dispose(); }
    }
}
