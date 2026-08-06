using System.Net;
using System.Net.Http.Json;
using LivingWorld.Simulation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Visual;

/// <summary>Fase 15, T7 (spec.md story "Modo personagem com FOW", AC1; edge case "movimento
/// inválido"): <c>POST /visual/player/{id}/move</c> — válido move de verdade e publica delta
/// (replayável via T3); inválido rejeita com 400 e hash canônico inalterado; id inexistente 404.</summary>
public class VisualInputEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public VisualInputEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Move_to_an_adjacent_cell_succeeds_and_actually_moves_the_npc()
    {
        var (npcId, from) = GetFirstNpcLocation();
        var target = new { TargetX = from.X + 1, TargetY = from.Y, InputMode = "click" };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync($"/visual/player/{npcId}/move", target);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var world = scope.ServiceProvider.GetRequiredService<WorldState>();
        var npc = world.Npcs.First(n => n.Id.Value == npcId);
        Assert.Equal(from.X + 1, npc.CurrentLocation.X);
        Assert.Equal(from.Y, npc.CurrentLocation.Y);
    }

    [Fact]
    public async Task Move_beyond_one_step_is_rejected_with_400_and_leaves_the_canonical_hash_unchanged()
    {
        var (npcId, from) = GetFirstNpcLocation();
        var target = new { TargetX = from.X + 50, TargetY = from.Y, InputMode = "click" };

        using var scope = _factory.Services.CreateScope();
        var world = scope.ServiceProvider.GetRequiredService<WorldState>();
        var hashBefore = WorldSnapshot.CanonicalHash(world);

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync($"/visual/player/{npcId}/move", target);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(hashBefore, WorldSnapshot.CanonicalHash(world));
    }

    [Fact]
    public async Task Move_for_an_npc_that_does_not_exist_returns_404()
    {
        var target = new { TargetX = 0, TargetY = 0, InputMode = "click" };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/visual/player/999999/move", target);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private (long NpcId, LivingWorld.Domain.CellCoord Location) GetFirstNpcLocation()
    {
        using var scope = _factory.Services.CreateScope();
        var world = scope.ServiceProvider.GetRequiredService<WorldState>();
        var npc = world.Npcs.First();
        return (npc.Id.Value, npc.CurrentLocation);
    }
}
