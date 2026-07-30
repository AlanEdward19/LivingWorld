using System.Text.Json;
using LivingWorld.Domain;
using LivingWorld.Infrastructure;
using LivingWorld.Simulation;
using LivingWorld.Workers;
using Microsoft.EntityFrameworkCore;

// Modo CLI usado pelo teste de determinismo entre processos (Fase 1, task 8): calcula os
// hashes de um cenário e sai, sem subir o host. `dotnet <dll> hash <seed> <ticks>`.
if (args.Length == 3 && args[0] == "hash")
{
    var seed = ulong.Parse(args[1]);
    var ticks = long.Parse(args[2]);
    var (canonical, volatileHash) = ScenarioRunner.RunAndHash(seed, ticks);
    Console.WriteLine($"{canonical};{volatileHash}");
    return 0;
}

// Fase 10: digest de significância para teste de dois processos.
// `dotnet <dll> history-significance-digest <seed> <ticks>`.
if (args.Length == 3 && args[0] == "history-significance-digest")
{
    var seed = ulong.Parse(args[1]);
    var ticks = long.Parse(args[2]);
    Console.WriteLine(HistorySimulationDigest.SignificanceDigest(seed, ticks, HistoryRules.Default));
    return 0;
}

// Fase 10: digest de memória viva para teste de dois processos.
// `dotnet <dll> history-living-memory-digest <seed> <ticks>`.
if (args.Length == 3 && args[0] == "history-living-memory-digest")
{
    var seed = ulong.Parse(args[1]);
    var ticks = long.Parse(args[2]);
    Console.WriteLine(HistorySimulationDigest.LivingMemoryDigest(seed, ticks, HistoryRules.Default));
    return 0;
}

// Modo CLI do teste de persistência entre processos (Fase 3, task 10): roda até <ticks>,
// salva snapshot+log em <dbPath> e sai — simula o processo terminando de verdade.
// `dotnet <dll> persist-save <seed> <dbPath> <ticks>`.
if (args.Length == 4 && args[0] == "persist-save")
{
    var seed = ulong.Parse(args[1]);
    var dbPath = args[2];
    var ticks = long.Parse(args[3]);

    using var context = OpenDb(dbPath);
    var repository = new SqliteWorldRepository(context);
    var runner = new PersistentWorldRunner(repository, BranchId.Root, snapshotIntervalTicks: ticks);
    var sink = new BufferingWorldEventSink();
    var (world, _) = ScenarioRunner.Create(seed);
    var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
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
    var runner = new PersistentWorldRunner(repository, BranchId.Root, snapshotIntervalTicks: extraTicks);
    var world = runner.LoadLatest() ?? throw new InvalidOperationException("nenhum snapshot salvo em " + dbPath);
    var sink = new BufferingWorldEventSink();
    var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
    runner.Run(world, clock, sink, extraTicks);
    Console.WriteLine(WorldSnapshot.CanonicalHash(world));
    return 0;
}

// Inspeciona um NPC vivo (Fase 8, T16, CITY-06): mesma NpcInspectionQuery da API, nenhuma
// lógica duplicada (AC #2 da story P1) — mesmo SPEC_DEVIATION do Api/Program.cs (T15) sobre
// cenário default no lugar de um snapshot persistido real. `dotnet <dll> inspect-npc <id>`.
if (args.Length == 2 && args[0] == "inspect-npc")
{
    var id = long.Parse(args[1]);
    var (world, _) = ScenarioRunner.Create(seed: 1);
    var result = NpcInspectionQuery.Inspect(world, new NpcId(id));
    if (!result.IsSuccess)
    {
        Console.Error.WriteLine(result.Error);
        return 1;
    }

    Console.WriteLine(JsonSerializer.Serialize(result.Value));
    return 0;
}

static WorldDbContext OpenDb(string dbPath)
{
    var options = new DbContextOptionsBuilder<WorldDbContext>().UseSqlite($"Data Source={dbPath}").Options;
    var context = new WorldDbContext(options);
    context.Database.Migrate();
    return context;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
return 0;
