using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Hosting;

namespace LivingWorld.Api.NpcDirectory;

public sealed record NpcDirectoryItem(NpcId Id, string Name, CityId CityId, ActionType? Action, CellCoord Location);

public sealed record NpcDirectoryPage(IReadOnlyList<NpcDirectoryItem> Items, int Total);

/// <summary>Lista paginada de NPCs vivos para o cliente: filtro por cidade e por nome, ordem estável
/// (nome, depois id). Só leitura; o detalhe de um NPC continua em <c>GET /npcs/{id}</c>.</summary>
public static class NpcDirectoryEndpoints
{
    public const int MaxTake = 200;

    public static void MapNpcDirectoryEndpoints(this WebApplication app, WorldHost host) =>
        app.MapGet("/npcs", (Guid? cityId, string? q, int? skip, int? take) =>
        {
            int skipValue = skip ?? 0;
            int takeValue = take ?? 50;
            if (skipValue < 0 || takeValue < 1 || takeValue > MaxTake)
                return Results.BadRequest($"skip deve ser >= 0 e take entre 1 e {MaxTake}");

            var matches = host.Current.Npcs
                .Where(n => n.IsAlive)
                .Where(n => cityId is null || n.City == new CityId(cityId.Value))
                .Where(n => string.IsNullOrEmpty(q) || n.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n.Name, StringComparer.Ordinal)
                .ThenBy(n => n.Id.Value)
                .ToArray();

            var items = matches.Skip(skipValue).Take(takeValue)
                .Select(n => new NpcDirectoryItem(n.Id, n.Name, n.City, n.CurrentAction, n.CurrentLocation))
                .ToArray();
            return Results.Ok(new NpcDirectoryPage(items, matches.Length));
        })
        .Produces<NpcDirectoryPage>(StatusCodes.Status200OK)
        .Produces<string>(StatusCodes.Status400BadRequest);
}
