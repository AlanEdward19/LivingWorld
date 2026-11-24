using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using LivingWorld.Api.Players;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Shared;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Api;

/// <summary>AD-197a, medida (não é gate): ida e volta de um comando do jogador em localhost real (Kestrel), pela rota HTTP e pelo canal
/// persistente, mediana e p95 em ms de N comandos depois de aquecer. <c>LW_MEASURE_OUT</c> grava o resultado.</summary>
public class PlayerLatencyHarness
{
    private const int Warmup = 50, Samples = 500;

    private static (double Median, double P95) Stats(List<double> ms)
    {
        ms.Sort();
        return (ms[ms.Count / 2], ms[(int)(ms.Count * 0.95)]);
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public async Task Command_round_trip_over_http_and_over_the_persistent_channel()
    {
        using var factory = new LivingWorldApiFactory();
        factory.UseKestrel(0);
        factory.StartServer();
        string address = factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        factory.Services.GetRequiredService<SimulationHost>().Resume();
        using var http = new HttpClient { BaseAddress = new Uri(address) };
        var session = (await (await http.PostAsync("/sessions", null)).Content.ReadFromJsonAsync<SessionResponse>())!;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.SessionToken);
        long npc = factory.Services.GetRequiredService<WorldHost>().Current.Npcs.First(n => n.IsAlive && n.ControllerId is null).Id.Value;
        Assert.True((await http.PostAsJsonAsync($"/players/{npc}/incarnate", new { })).IsSuccessStatusCode);

        var httpMs = new List<double>();
        for (int i = 0; i < Warmup + Samples; i++)
        {
            long start = Stopwatch.GetTimestamp();
            var response = await http.PostAsJsonAsync($"/players/{session.PlayerId}/action", new { action = i % 2 == 0 ? "Idle" : "Eat" });
            await response.Content.ReadAsByteArrayAsync();
            if (i >= Warmup) httpMs.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            Assert.True(response.IsSuccessStatusCode);
        }

        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Authorization", "Bearer " + session.SessionToken);
        await socket.ConnectAsync(new Uri(address.Replace("http", "ws") + "/players/ws"), CancellationToken.None);
        var buffer = new byte[16 * 1024];
        var wsMs = new List<double>();
        for (int i = 0; i < Warmup + Samples; i++)
        {
            long start = Stopwatch.GetTimestamp();
            string command = $$"""{"seq":{{i}},"command":"action","action":"{{(i % 2 == 0 ? "Idle" : "Eat")}}"}""";
            await socket.SendAsync(Encoding.UTF8.GetBytes(command), WebSocketMessageType.Text, true, CancellationToken.None);
            var received = await socket.ReceiveAsync(buffer, CancellationToken.None);
            if (i >= Warmup) wsMs.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            Assert.Contains("\"ok\":true", Encoding.UTF8.GetString(buffer, 0, received.Count), StringComparison.Ordinal);
        }

        // AD-197d: o passo (intenção validada no chão do motor, ida e volta alternadas) e a oferta de interações, do comando à resposta
        var moveMs = new List<double>();
        var reachMs = new List<double>();
        for (int i = 0; i < Warmup + Samples; i++)
        {
            long start = Stopwatch.GetTimestamp();
            string move = $$"""{"seq":{{i}},"command":"move","dx":{{(i % 2 == 0 ? 1 : -1)}},"dy":0}""";
            await socket.SendAsync(Encoding.UTF8.GetBytes(move), WebSocketMessageType.Text, true, CancellationToken.None);
            var reply = await socket.ReceiveAsync(buffer, CancellationToken.None);
            if (i >= Warmup) moveMs.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            Assert.Contains("\"status\":", Encoding.UTF8.GetString(buffer, 0, reply.Count), StringComparison.Ordinal);
            start = Stopwatch.GetTimestamp();
            await socket.SendAsync(Encoding.UTF8.GetBytes($$"""{"seq":{{i}},"command":"reach"}"""), WebSocketMessageType.Text, true, CancellationToken.None);
            reply = await socket.ReceiveAsync(buffer, CancellationToken.None);
            if (i >= Warmup) reachMs.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            Assert.Contains("\"ok\":true", Encoding.UTF8.GetString(buffer, 0, reply.Count), StringComparison.Ordinal);
        }

        var (hMed, hP95) = Stats(httpMs);
        var (wMed, wP95) = Stats(wsMs);
        var (mMed, mP95) = Stats(moveMs);
        var (rMed, rP95) = Stats(reachMs);
        string line = FormattableString.Invariant(
            $"httpMedianMs={hMed:0.000} httpP95Ms={hP95:0.000} wsMedianMs={wMed:0.000} wsP95Ms={wP95:0.000} wsMoveMedianMs={mMed:0.000} wsMoveP95Ms={mP95:0.000} wsReachMedianMs={rMed:0.000} wsReachP95Ms={rP95:0.000} samples={Samples}");
        if (Environment.GetEnvironmentVariable("LW_MEASURE_OUT") is { Length: > 0 } path) File.WriteAllText(path, line + "\n");
        Assert.True(wMed > 0 && hMed > 0, line);
    }
}
