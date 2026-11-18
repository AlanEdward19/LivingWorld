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

/// <summary>AD-183: por que um termo da decisão tem o valor que tem, quando o nome pesou: <c>xara</c> = o luto é pelo avô/avó (ou
/// pai/mãe) de quem a pessoa leva o nome (<see cref="OtherId"/> = o morto). Só termos que somaram algo na leitura.</summary>
public sealed record BrainMotiveDto(UtilityTerm Term, string Motive, long OtherId);

/// <summary>Resposta de <c>GET /npcs/{id}/brain</c> (AD-160b): a rede da decisão de agora, termo → grupo → ação; a soma das sinapses
/// de uma ação é exatamente a nota que o motor dá a ela. <see cref="Enabled"/> falso = cérebro desligado no mundo (ganhos zero).
/// <see cref="Motives"/> (AD-183): o nome por trás de um termo (vazio quando o nome não pesou).</summary>
public sealed record BrainReadingResponse(
    bool Enabled, IReadOnlyList<BrainSynapseDto> Synapses, IReadOnlyList<BrainScoreDto> Scores, BrainValueDto? Value,
    IReadOnlyList<BrainMotiveDto>? Motives = null);

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
                reading.Scores.OrderBy(kv => kv.Key).Select(kv => new BrainScoreDto(kv.Key, kv.Value)).ToList(), value,
                NameMotives(world, new NpcId(id), reading.Synapses.Any(s => s.Term == UtilityTerm.Grief && s.Value != 0))));
        });
    }

    /// <summary>AD-183: o luto que pesa no termo <c>Grief</c> é pelo homenageado (o xará enluta o avô/avó de quem leva o nome)?
    /// Lê as lembranças de morte ainda vivas e devolve, das que são pelo homenageado, a mais forte.</summary>
    public static IReadOnlyList<BrainMotiveDto> NameMotives(LivingWorld.Simulation.Core.WorldState world, NpcId id, bool griefWeighs)
    {
        if (!griefWeighs || world.FindNpc(id) is not { Psyche: { } psyche } npc) return [];
        long tick = world.CurrentDate.TotalHours;
        var rules = world.NeedsRules.Psyche;
        BrainMotiveDto? best = null;
        double strongest = 0;
        foreach (var memory in psyche.Memories)
        {
            if (memory.Kind != LivingWorld.Domain.Cognition.LifeEventKind.FriendDied || memory.Other < 0) continue;
            if (world.FindNpc(new NpcId(memory.Other)) is not { } dead
                || !LivingWorld.Simulation.Population.Naming.NameBonds.IsNamesake(world, npc, dead)) continue;
            double strength = LivingWorld.Simulation.Cognition.PsycheMechanic.StrengthOf(rules, memory, tick, world.Calendar.HoursPerDay);
            if (strength > strongest) (strongest, best) = (strength, new BrainMotiveDto(UtilityTerm.Grief, "xara", memory.Other));
        }
        return best is null ? [] : [best];
    }
}
