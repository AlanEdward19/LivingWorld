using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Works;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Players;

/// <summary>#241: a ação em andamento que ocupa o corpo do jogador, como o cliente a vê: o quê (<c>Action</c>), quando acaba (<c>UntilMs</c>,
/// ms de mundo), o tempo total e o que falta (<c>RemainingMs</c>, para o cliente não depender do relógio do mundo).</summary>
public sealed record BusyView(string Action, long UntilMs, long TotalMs, long RemainingMs);

/// <summary>#241: tarefas que ocupam o corpo conduzido (hoje, cortar lenha na mata). O motor decide a duração, o que fica feito ao
/// interromper (a parte proporcional da carga, arredondada para baixo: determinístico, sem sorteio) e quando o corpo volta a andar.</summary>
public static class PlayerTasks
{
    public const string Gather = "gather";

    /// <summary>A tarefa em curso agora, ou nula (nunca houve, ou já venceu).</summary>
    public static BusyView? Of(WorldState world, Npc npc)
    {
        long now = world.CurrentDate.TotalMilliseconds;
        return npc.BodyTask is { } task && now < task.UntilMs
            ? new BusyView(task.Action, task.UntilMs, task.UntilMs - task.StartedAtMs, task.UntilMs - now)
            : null;
    }

    /// <summary>O que uma sessão de corte rende e dura, pela conta dos NPCs da obra: o tempo de cortar por unidade
    /// (<c>WorkHaul.CutHours</c> da carga que o corpo aguenta, sem a viagem: o jogador anda de verdade) e, por sessão, o que cabe numa
    /// jornada de trabalho (<c>CrewHoursPerDay</c>), no limite de carga: o NPC corta no mesmo ritmo, mas em jornadas.</summary>
    internal static (long Units, long DurationMs) CutSession(WorldState world, Npc npc, ResourceType resource, CellCoord from, CellCoord wood, long? wanted)
    {
        long cap = WorkHaul.CapacityUnits(world, npc, resource);
        double perUnitMs = (double)WorkHaul.CutHours(world, from, wood, cap) * WorldMillis.PerHour / cap;
        double shiftMs = Math.Max(1, world.StreetRules.CrewHoursPerDay) * (double)WorldMillis.PerHour;
        long units = Math.Clamp((long)Math.Floor(shiftMs / perUnitMs), 1, cap);
        if (wanted is { } w) units = Math.Min(units, Math.Max(1, w));
        return (units, Math.Max(1, (long)Math.Ceiling(perUnitMs * units)));
    }

    /// <summary>Para o que o corpo faz agora: o que já cortou (proporção do tempo, para baixo) fica nas mãos, o resto não existiu. Sem
    /// tarefa em curso, recusa (409) e o mundo não muda.</summary>
    public static Result<BusyView> Interrupt(WorldState world, Npc npc)
    {
        if (Of(world, npc) is not { } busy || npc.BodyTask is not { } task)
            return Result<BusyView>.Fail($"{PlayerCommands.Conflict} interrupt: não há ação em andamento");
        long now = world.CurrentDate.TotalMilliseconds;
        if (task.Action == Gather && task.Units > 0 && npc.IsCarrying && npc.CarriedResourceId == task.ResourceId)
        {
            long total = Math.Max(1, task.UntilMs - task.StartedAtMs);
            long kept = task.Units * Math.Clamp(now - task.StartedAtMs, 0, total) / total;
            var resource = new ResourceType(task.ResourceId);
            npc.TakeCarried();
            if (kept > 0) npc.PickUp(resource, kept, Math.Max(kept, npc.CarryCapacity));
            world.RecordResourceProduced(resource, kept - task.Units); // o que não chegou a ser cortado não entra na produção
        }
        npc.Step(now, 0, now);
        npc.SetBodyTask(null);
        return Result<BusyView>.Ok(busy);
    }
}
