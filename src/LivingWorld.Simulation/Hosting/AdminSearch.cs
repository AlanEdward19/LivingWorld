using System.Globalization;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Hosting;

public sealed record AdminNpcHit(long Id, string Name, string City, int ProfessionId, bool Alive);

/// <summary>Busca de NPC do console (Fase 26) por nome, cidade ou profissão, ordenada por id. Só lê.</summary>
public static class AdminSearch
{
    public static readonly IReadOnlyList<string> Kinds = ["name", "city", "profession"];

    public static Result<IReadOnlyList<AdminNpcHit>> Npcs(WorldState world, string? kind, string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return Result<IReadOnlyList<AdminNpcHit>>.Fail("q: obrigatório");
        string term = q.Trim();

        Func<Domain.Population.Npc, bool> match;
        switch ((kind ?? "name").ToLowerInvariant())
        {
            case "name":
                match = n => n.Name.Contains(term, StringComparison.OrdinalIgnoreCase);
                break;
            case "city":
                var cities = world.ActiveCities().Where(c => c.Name.Contains(term, StringComparison.OrdinalIgnoreCase)).Select(c => c.Id).ToHashSet();
                match = n => cities.Contains(n.City);
                break;
            case "profession":
                if (!int.TryParse(term, NumberStyles.Integer, CultureInfo.InvariantCulture, out int professionId))
                    return Result<IReadOnlyList<AdminNpcHit>>.Fail("q: profissão é o número do ofício");
                match = n => n.Profession.Id == professionId;
                break;
            default:
                return Result<IReadOnlyList<AdminNpcHit>>.Fail($"kind: desconhecido (use {string.Join(", ", Kinds)})");
        }

        IReadOnlyList<AdminNpcHit> hits = world.Npcs
            .Where(match)
            .OrderBy(n => n.Id.Value)
            .Select(n => new AdminNpcHit(n.Id.Value, n.Name, world.FindCity(n.City)?.Name ?? "", n.Profession.Id, n.IsAlive))
            .ToList();
        return Result<IReadOnlyList<AdminNpcHit>>.Ok(hits);
    }
}
