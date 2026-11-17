using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Cities.Streets;
using LivingWorld.Simulation.Hosting;

namespace LivingWorld.Api.Sandbox;

/// <summary>Uma sinapse da leitura: termo (entrada) → grupo (camada do meio) → ação (saída), o valor que somou e o ganho do NPC.</summary>
public sealed record BrainSynapseDto(ActionType Action, UtilityTerm Term, UtilityGroup Group, double Value, int GainMilli);

/// <summary>A nota de uma ação: a soma das sinapses dela.</summary>
public sealed record BrainScoreDto(ActionType Action, double Score);

/// <summary>O valor que o NPC dá ao próprio estado agora, parcela por parcela (a recompensa que ensina é a variação dele).</summary>
public sealed record BrainValueDto(double Body, double Pantry, double Money, double Mood, double Total);

/// <summary>AD-184: a decisão do NPC sobre a obra — a dele (diarista, família, parente, mutirão) ou a melhor que contrata na cidade
/// (candidato) — com os termos: jornal oferecido e o que ganha hoje por dia, a necessidade da casa (0–1), a parte da jornada que a
/// ida deixa (0–1), o valor (o termo <c>SiteWork</c>), o mínimo para aceitar e o motivo (aceita, salario, carencia, dono, longe,
/// turma-cheia, abaixo-do-minimo, sem-necessidade, idade, da-casa, outra-cidade, ou o papel de quem já está nela).</summary>
public sealed record BrainSiteWorkDto(
    long WorkId, string Role, LivingWorld.Domain.Geography.CellCoord Site, bool Joined, double OfferDay, double WageDay, double Need,
    double Share, double Value, double JoinMin, string Reason);

/// <summary>Resposta de <c>GET /npcs/{id}/brain</c> (AD-160b): a rede da decisão de agora, termo → grupo → ação; a soma das sinapses
/// de uma ação é exatamente a nota que o motor dá a ela. <see cref="Enabled"/> falso = cérebro desligado no mundo (ganhos zero).</summary>
public sealed record BrainReadingResponse(
    bool Enabled, IReadOnlyList<BrainSynapseDto> Synapses, IReadOnlyList<BrainScoreDto> Scores, BrainValueDto? Value,
    BrainSiteWorkDto? SiteWork = null);

/// <summary>"Ler o cérebro" (ADR-0046, opção b): projeção volátil do canônico; nunca escreve no mundo.</summary>
public static class BrainEndpoints
{
    public static void MapBrainEndpoints(this WebApplication app, WorldHost host)
    {
        app.MapGet("/npcs/{id:long}/brain", (long id) =>
        {
            var world = host.Current;
            var result = BrainReadout.Read(world, new NpcId(id));
            if (!result.IsSuccess) return Results.NotFound();
            var reading = result.Value!;
            var value = reading.Value is { } v ? new BrainValueDto(v.Body, v.Pantry, v.Money, v.Mood, v.Total) : null;
            // AD-184: aceitar ou recusar a obra, com os termos
            var site = world.FindNpc(new NpcId(id)) is { } npc && SiteWork.Explain(world, npc) is { } e
                ? new BrainSiteWorkDto(e.WorkId, e.Role, e.Site, e.Joined, e.Terms.OfferDay, e.Terms.WageDay, e.Terms.Need, e.Terms.Share,
                    e.Terms.Value, world.StreetRules.SiteJoinMin, e.Terms.Reason)
                : null;
            return Results.Ok(new BrainReadingResponse(
                world.BrainRules is { Enabled: true },
                reading.Synapses.Select(s => new BrainSynapseDto(s.Action, s.Term, s.Group, s.Value, s.GainMilli)).ToList(),
                reading.Scores.OrderBy(kv => kv.Key).Select(kv => new BrainScoreDto(kv.Key, kv.Value)).ToList(), value, site));
        });
    }
}
