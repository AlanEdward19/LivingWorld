using LivingWorld.Api.Players;
using LivingWorld.Domain.Behavior;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Integration.Api;

/// <summary>ADR-0048 (AD-197a): <c>players.log</c> ao lado do save; depois de reiniciar, base + registro reproduzem o hash.</summary>
public class PlayerLogStoreTests
{
    [Fact]
    public void After_a_restart_the_saved_base_and_log_replay_to_the_same_hash()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lw-players-" + Environment.ProcessId + "-" + Environment.CurrentManagedThreadId);
        Directory.CreateDirectory(dir);
        try
        {
            var (world, _) = ScenarioRunner.Create(31, initialPopulation: 40);
            var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: new BufferingWorldEventSink());
            clock.Run(world, 48);
            var store = new PlayerLogStore(dir);
            var players = new PlayerService(new BufferingWorldEventSink());
            var adults = world.Npcs.Where(n => n.IsAlive && n.Employer is not null).Take(2).ToList();

            void Apply(Func<bool> command)
            {
                store.BeforeCommand("mundo", world);
                Assert.True(command());
                store.Append(players.Log.Last!);
            }

            Apply(() => players.Incarnate(world, adults[0].Id, "ana").IsSuccess);
            clock.Run(world, 5);
            Apply(() => players.Incarnate(world, adults[1].Id, "bia").IsSuccess);
            Apply(() => players.Act(world, "ana", ActionType.Idle).IsSuccess);
            clock.Run(world, 30);
            Apply(() => players.Logout(world, "bia").IsSuccess);
            clock.Run(world, 20);
            long saved = world.CurrentDate.TotalMilliseconds;
            string hash = WorldSnapshot.CanonicalHash(world);

            // reinício: nada em memória, só os arquivos
            var (baseJson, records) = PlayerLogStore.Read(dir, "mundo")!.Value;
            var replayed = PlayerReplay.Run(baseJson, records, new WorldClock(ScenarioRunner.DefaultSystems(), sink: new BufferingWorldEventSink()),
                new BufferingWorldEventSink(), saved);

            Assert.Equal(4, records.Count);
            Assert.Equal(hash, WorldSnapshot.CanonicalHash(replayed));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>ADR-0050 (AD-197b): registro gravado em horas (AD-197a) ou na forma em segundos de antes da troca para ms carrega
    /// convertido para milissegundos.</summary>
    [Fact]
    public void An_old_log_in_hours_or_seconds_loads_in_milliseconds()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lw-players-old-" + Environment.ProcessId + "-" + Environment.CurrentManagedThreadId);
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "mundo.players.base.json"), "{}");
            File.WriteAllLines(Path.Combine(dir, "mundo.players.log"),
            [
                "{\"Tick\":5,\"Sequence\":0,\"PlayerId\":\"ana\",\"Kind\":\"Incarnate\",\"NpcId\":3}",
                "{\"Tick\":\"18030s\",\"Sequence\":1,\"PlayerId\":\"admin\",\"Kind\":\"TickDelta\",\"DeltaSeconds\":60}",
            ]);

            var (_, records) = PlayerLogStore.Read(dir, "mundo")!.Value;

            Assert.Equal(
                (5 * 3_600_000L, 18_030_000L, (int?)60_000),
                (records[0].AtMs, records[1].AtMs, records[1].DeltaMs));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
