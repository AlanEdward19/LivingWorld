using System.Text.Json;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure;
using LivingWorld.Infrastructure.EfCore;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.History.Books;
using LivingWorld.Simulation.History.Distortion;
using LivingWorld.Simulation.Llm;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Workers;
using Microsoft.EntityFrameworkCore;

// Modo CLI usado pelo teste de determinismo entre processos: calcula os
// hashes de um cenário e sai, sem subir o host. `dotnet <dll> hash <seed> <ticks>`.
if (args.Length == 3 && args[0] == "hash")
{
    var seed = ulong.Parse(args[1]);
    var ticks = long.Parse(args[2]);
    var (canonical, volatileHash) = ScenarioRunner.RunAndHash(seed, ticks);
    Console.WriteLine($"{canonical};{volatileHash}");
    return 0;
}

// SHA-256 do bloco binário posicional (formato v2) do mundo depois de <ticks>: o teste de dois processos exige bytes
// idênticos. `dotnet <dll> snapshot-bytes <seed> <ticks>`.
if (args.Length == 3 && args[0] == "snapshot-bytes")
{
    var (world, clock) = ScenarioRunner.Create(ulong.Parse(args[1]), initialPopulation: 20);
    clock.Run(world, long.Parse(args[2]));
    var block = LivingWorld.Simulation.Snapshot.PositionalSnapshot.EncodeFull(world, out _);
    Console.WriteLine(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(block)));
    return 0;
}

// SHA-256 do event log compactado (lotes mensais) depois de <ticks>: o teste de dois processos exige o mesmo agregado.
// `dotnet <dll> eventlog-digest <seed> <ticks>`.
if (args.Length == 3 && args[0] == "eventlog-digest")
{
    var (world, baseClock) = ScenarioRunner.Create(ulong.Parse(args[1]), initialPopulation: 40);
    var buffer = new BufferingWorldEventSink();
    var clock = new WorldClock(baseClock.Systems, baseClock.MaxIterationsPerTick, buffer);
    var lines = new List<string>();
    long total = long.Parse(args[2]);
    for (long t = 1; t <= total; t++)
    {
        clock.Tick(world);
        if (t % world.Calendar.HoursPerMonth == 0 || t == total)
            lines.AddRange(LivingWorld.Infrastructure.EventLog.EventLogCompactor.Compact(buffer.DrainAll(), world.Calendar)
                .Select(e => $"{e.Tick}|{e.Kind}|{e.Payload}|{e.SourceSystem}"));
    }

    Console.WriteLine(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join((char)10, lines)))));
    return 0;
}

// Hash canônico de uma vila em que um jogador encarna um NPC, pede ação e sai (desaparecimento): o teste de dois processos exige o
// mesmo hash. `dotnet <dll> player-hash <seed> <ticks>`.
if (args.Length == 3 && args[0] == "player-hash")
{
    var (world, _) = ScenarioRunner.Create(ulong.Parse(args[1]));
    var sink = new BufferingWorldEventSink();
    var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
    clock.Run(world, 96);
    var npc = world.Npcs.First(n => n.IsAlive && n.Employer is not null && n.Household is not null);
    var players = new LivingWorld.Simulation.Players.PlayerService(sink);
    players.Incarnate(world, npc.Id, "alan");
    players.Act(world, "alan", LivingWorld.Domain.Behavior.ActionType.Idle);
    clock.Run(world, 48);
    players.Logout(world, "alan");
    clock.Run(world, long.Parse(args[2]));
    Console.WriteLine($"{WorldSnapshot.CanonicalHash(world)};{npc.DisappearedAtTick}");
    return 0;
}

// Hash canônico de uma vila com doença ligada (surto por densidade, contágio, cura e morte): o teste de dois processos exige o
// mesmo hash. `dotnet <dll> disease-hash <seed> <ticks>`.
if (args.Length == 3 && args[0] == "disease-hash")
{
    var disease = new LivingWorld.Domain.Health.DiseaseRules(
        true, [new LivingWorld.Domain.Health.DiseaseProfile(1, 0.08, 0.15, 6, 180)], OutbreakPopulationThreshold: 20, OutbreakDailyChance: 0.02);
    var (world, clock) = ScenarioRunner.Create(ulong.Parse(args[1]), diseaseRules: disease);
    clock.Run(world, long.Parse(args[2]));
    Console.WriteLine($"{WorldSnapshot.CanonicalHash(world)};{world.Infections.Count};{world.Npcs.Count(n => !n.IsAlive)}");
    return 0;
}

// Hash canônico de uma vila com clima ligado (anos de safra por região): o teste de dois processos exige o mesmo hash.
// `dotnet <dll> climate-hash <seed> <ticks>`.
if (args.Length == 3 && args[0] == "climate-hash")
{
    var (world, clock) = ScenarioRunner.Create(ulong.Parse(args[1]), climateRules: new LivingWorld.Domain.Climate.ClimateRules(true, 300, 1500));
    clock.Run(world, long.Parse(args[2]));
    Console.WriteLine($"{WorldSnapshot.CanonicalHash(world)};{world.ClimateYears.Count}");
    return 0;
}

// Hash canônico de uma vila com furto ligado (desespero, temperamento, sorteio, descoberta): o teste de dois processos exige o
// mesmo hash. `dotnet <dll> theft-hash <seed> <ticks>`.
if (args.Length == 3 && args[0] == "theft-hash")
{
    var (world, _) = ScenarioRunner.Create(ulong.Parse(args[1]), theftRules: new LivingWorld.Domain.Crime.TheftRules(true, 90, 0.9, 0.6, 0.5, 0.5, -1, 0.25));
    int thefts = 0;
    var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: new TheftCounter(() => thefts++));
    clock.Run(world, long.Parse(args[2]));
    Console.WriteLine($"{WorldSnapshot.CanonicalHash(world)};{thefts}");
    return 0;
}

// Hash canônico de uma vila com emergência ligada (pressão de trajeto, protótipo, vetor, adoção): o teste de dois processos exige o
// mesmo hash e as mesmas tecnologias. `dotnet <dll> emergence-hash <seed> <ticks>`.
if (args.Length == 3 && args[0] == "emergence-hash")
{
    var (world, clock) = ScenarioRunner.Create(
        ulong.Parse(args[1]), emergenceRules: LivingWorld.Domain.Emergence.EmergenceRules.Mobility with { PressureTravelTicks = 2 });
    world.ActiveCities().First().DepositStock(new LivingWorld.Domain.Geography.ResourceType(4), 5000);
    clock.Run(world, long.Parse(args[2]));
    Console.WriteLine($"{WorldSnapshot.CanonicalHash(world)};{world.Technologies.Count};{string.Join(',', world.Technologies.Select(t => t.Vector.Key))}");
    return 0;
}

// Hash canônico de uma vila com aparência herdada ligada: o teste de dois processos exige o mesmo hash. Imprime também quantos
// nascidos têm genes de aparência. `dotnet <dll> appearance-hash <seed> <ticks>`.
if (args.Length == 3 && args[0] == "appearance-hash")
{
    var (world, clock) = ScenarioRunner.Create(
        ulong.Parse(args[1]), appearanceRules: new LivingWorld.Domain.Population.AppearanceRules(0.6));
    clock.Run(world, long.Parse(args[2]));
    Console.WriteLine($"{WorldSnapshot.CanonicalHash(world)};{world.Npcs.Count(n => n.MotherId is not null && n.Appearance is not null)}");
    return 0;
}

// `dotnet <dll> history-significance-digest <seed> <ticks>`.
if (args.Length == 3 && args[0] == "history-significance-digest")
{
    var seed = ulong.Parse(args[1]);
    var ticks = long.Parse(args[2]);
    Console.WriteLine(HistorySimulationDigest.SignificanceDigest(seed, ticks, HistoryRules.Default));
    return 0;
}

// `dotnet <dll> history-living-memory-digest <seed> <ticks>`.
if (args.Length == 3 && args[0] == "history-living-memory-digest")
{
    var seed = ulong.Parse(args[1]);
    var ticks = long.Parse(args[2]);
    Console.WriteLine(HistorySimulationDigest.LivingMemoryDigest(seed, ticks, HistoryRules.Default));
    return 0;
}

// `dotnet <dll> history-distortion-digest <seed>`.
if (args.Length == 2 && args[0] == "history-distortion-digest")
{
    var seed = ulong.Parse(args[1]);
    Console.WriteLine(HistoryDistortionDigest.Compute(seed, HistoryRules.Default));
    return 0;
}

// `dotnet <dll> history-book-digest <seed>`.
if (args.Length == 2 && args[0] == "history-book-digest")
{
    var seed = ulong.Parse(args[1]);
    Console.WriteLine(HistoryBookDigest.Compute(seed, HistoryRules.Default));
    return 0;
}

// `dotnet <dll> history-index-digest <seed> <ticks>`.
if (args.Length == 3 && args[0] == "history-index-digest")
{
    var seed = ulong.Parse(args[1]);
    var ticks = long.Parse(args[2]);
    Console.WriteLine(HistorySimulationDigest.HistoryIndexDigest(seed, ticks, HistoryRules.Default));
    return 0;
}

// `dotnet <dll> history-correction-digest <seed>`.
if (args.Length == 2 && args[0] == "history-correction-digest")
{
    var seed = ulong.Parse(args[1]);
    Console.WriteLine(HistoryCorrectionDigest.Compute(seed));
    return 0;
}

// Modo CLI do teste de persistência entre processos: roda até <ticks>,
// salva snapshot+log em <dbPath> e sai — simula o processo terminando de verdade.
// `dotnet <dll> persist-save <seed> <dbPath> <ticks>`.
if (args.Length == 4 && args[0] == "persist-save")
{
    var seed = ulong.Parse(args[1]);
    var dbPath = args[2];
    var ticks = long.Parse(args[3]);

    using var context = OpenDb(dbPath);
    var repository = new SqliteWorldRepository(context);
    var runner = new PersistentWorldRunner(repository, BranchId.Root, snapshotIntervalTicks: ticks, coldTier: OpenColdTier(dbPath));
    var sink = new BufferingWorldEventSink();
    var (world, _) = ScenarioRunner.Create(seed);
    var clock = new WorldClock(ScenarioRunner.DefaultSystems(extraordinary: world.Extraordinary), sink: sink);
    runner.Run(world, clock, sink, ticks);
    return 0;
}

// Continua do último snapshot salvo em <dbPath> por mais <extraTicks> e imprime o hash
// canônico final. `dotnet <dll> persist-resume <dbPath> <extraTicks>`.
if (args.Length == 3 && args[0] == "persist-resume")
{
    var dbPath = args[1];
    var extraTicks = long.Parse(args[2]);

    using var context = OpenDb(dbPath);
    var repository = new SqliteWorldRepository(context);
    var runner = new PersistentWorldRunner(repository, BranchId.Root, snapshotIntervalTicks: extraTicks, coldTier: OpenColdTier(dbPath));
    var world = runner.LoadLatest() ?? throw new InvalidOperationException("nenhum snapshot salvo em " + dbPath);
    var sink = new BufferingWorldEventSink();
    var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
    runner.Run(world, clock, sink, extraTicks);
    Console.WriteLine(WorldSnapshot.CanonicalHash(world));
    return 0;
}

// Inspeciona um NPC: mesma NpcInspectionQuery da API, nenhuma lógica duplicada. `dotnet <dll>
// inspect-npc <id>`. Ferramenta de debug ad-hoc de processo único, sem concorrência de leitores —
// materializar sob demanda continua o comportamento útil aqui.
if (args.Length == 2 && args[0] == "inspect-npc")
{
    var id = long.Parse(args[1]);
    var (world, _) = ScenarioRunner.Create(seed: 1);
    var result = NpcInspectionQuery.MaterializeAndInspect(world, new NpcId(id));
    if (!result.IsSuccess)
    {
        Console.Error.WriteLine(result.Error);
        return 1;
    }

    Console.WriteLine(JsonSerializer.Serialize(result.Value));
    return 0;
}

// Job de compactação de memória batch, fora do caminho crítico do tick (`WorldClock`) — CLI
// separado, mesmo padrão de `hash`/`persist-resume`.
// `dotnet <dll> compact-memory <seed>`.
if (args.Length == 2 && args[0] == "compact-memory")
{
    var seed = ulong.Parse(args[1]);
    var (world, _) = ScenarioRunner.Create(seed);
    MemoryCompactionJob.Compact(world);
    Console.WriteLine(WorldSnapshot.CanonicalHash(world));
    return 0;
}

static WorldDbContext OpenDb(string dbPath)
{
    var options = new DbContextOptionsBuilder<WorldDbContext>().UseSqlite($"Data Source={dbPath}").Options;
    var context = new WorldDbContext(options);
    context.Database.Migrate();
    return context;
}

// Arquivo frio (ColdTierArchive) vive ao lado do .db, mesma convenção de nome — sem parâmetro de
// CLI novo. dbPath do CLI é sempre um arquivo real (nunca :memory:), então sempre há cold tier.
static ColdTierPersistence OpenColdTier(string dbPath) => new(dbPath + ".coldtier");

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
return 0;

internal sealed class TheftCounter(Action onTheft) : LivingWorld.Simulation.Core.IWorldEventSink
{
    public void Record(LivingWorld.Simulation.Core.WorldEvent evt)
    {
        if (evt.Kind == LivingWorld.Domain.History.WorldEventKind.TheftDiscovered) onTheft();
    }
}
