using LivingWorld.Simulation;
using LivingWorld.Workers;

// Modo CLI usado pelo teste de determinismo entre processos (Fase 1, task 8): calcula os
// hashes de um cenário e sai, sem subir o host. `dotnet <dll> hash <seed> <ticks>`.
if (args.Length == 3 && args[0] == "hash")
{
    var seed = ulong.Parse(args[1]);
    var ticks = long.Parse(args[2]);
    var (canonical, volatileHash) = ScenarioRunner.RunAndHash(seed, ticks);
    Console.WriteLine($"{canonical};{volatileHash}");
    return;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
