using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Hosting;

namespace LivingWorld.Api.Sandbox;

/// <summary>Uma sinapse da leitura: termo (entrada) → grupo (camada do meio) → ação (saída), o valor que somou e o ganho do NPC.</summary>
public sealed record BrainSynapseDto(ActionType Action, UtilityTerm Term, UtilityGroup Group, double Value, int GainMilli);

/// <summary>A nota de uma ação: a soma das sinapses dela.</summary>
public sealed record BrainScoreDto(ActionType Action, double Score);

/// <summary>O valor que o NPC dá ao próprio estado agora, parcela por parcela (a recompensa que ensina é a variação dele).</summary>
public sealed record BrainValueDto(double Body, double Pantry, double Money, double Mood, double Total);

/// <summary>Resposta de <c>GET /npcs/{id}/brain</c> (AD-160b): a rede da decisão de agora, termo → grupo → ação; a soma das sinapses
/// de uma ação é exatamente a nota que o motor dá a ela. <see cref="Enabled"/> falso = cérebro desligado no mundo (ganhos zero).</summary>
public sealed record BrainReadingResponse(
    bool Enabled, IReadOnlyList<BrainSynapseDto> Synapses, IReadOnlyList<BrainScoreDto> Scores, BrainValueDto? Value);

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
            return Results.Ok(new BrainReadingResponse(
                world.BrainRules is { Enabled: true },
                reading.Synapses.Select(s => new BrainSynapseDto(s.Action, s.Term, s.Group, s.Value, s.GainMilli)).ToList(),
                reading.Scores.OrderBy(kv => kv.Key).Select(kv => new BrainScoreDto(kv.Key, kv.Value)).ToList(), value));
        });
    }
}
