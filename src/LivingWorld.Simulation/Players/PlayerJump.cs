using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Players;

/// <summary>AD-197u: o pulo do corpo conduzido. O motor decide se o corpo consegue e por quanto tempo fica no ar; a posição na grade
/// não muda (não atravessa parede nem pula prédio). O corpo recusa (<c>conflict:</c>, 409) quando já está no ar, exausto (sono abaixo de
/// <see cref="PlayerRules.JumpMinSleep"/>), ferido demais (saúde abaixo de <see cref="PlayerRules.ExertionMinHealth"/>) ou fraco demais
/// para a mecânica do corpo (<see cref="BodyMechanic"/>: passada curta de criança pequena, peso e fraqueza no custo de movimento;
/// massa muscular baixa de idoso e frágil na capacidade de força). O tempo no ar sai da mesma força
/// (<see cref="BodyMechanic.WorkCapacityMultiplier"/>, 0,8 a 1,2 do base). Só o necessário vai ao NPC: <see cref="Npc.JumpUntilMs"/>.
/// Determinístico: tudo vem do corpo e do tempo do mundo.</summary>
public static class PlayerJump
{
    /// <summary>Tempo no ar (ms) que o corpo teria agora (ver <see cref="Refusal"/> para quando nem pula).</summary>
    public static int AirMs(WorldState world, Npc npc)
    {
        double capacity = BodyMechanic.WorkCapacityMultiplier(world, npc);
        return (int)Math.Round(world.Players.JumpAirMs * Math.Clamp(capacity, 0.8, 1.2));
    }

    /// <summary>Por que o corpo não pula agora, ou nulo se pula.</summary>
    public static string? Refusal(WorldState world, Npc npc)
    {
        var rules = world.Players;
        long now = world.CurrentDate.TotalMilliseconds;
        if (now < npc.JumpUntilMs) return $"{PlayerCommands.Conflict} jump: ainda no ar (faltam {npc.JumpUntilMs - now} ms)";
        if (npc.SleepNeed.ValueAtMs(now) < rules.JumpMinSleep) return $"{PlayerCommands.Conflict} jump: {npc.Name} está exausto";
        if (npc.Health < rules.ExertionMinHealth) return $"{PlayerCommands.Conflict} jump: {npc.Name} está ferido ou fraco demais";
        if (npc.AgeYears(world.CurrentDate) < rules.JumpMinAgeYears) return $"{PlayerCommands.Conflict} jump: {npc.Name} ainda é pequeno demais para pular";
        if (npc.AgeYears(world.CurrentDate) > rules.JumpMaxAgeYears) return $"{PlayerCommands.Conflict} jump: {npc.Name} já não tem idade para pular";
        if (BodyMechanic.MovementCostMultiplier(world, npc) > rules.JumpMaxBodyCost)
            return $"{PlayerCommands.Conflict} jump: {npc.Name} é pequeno ou pesado demais para pular";
        if (BodyMechanic.WorkCapacityMultiplier(world, npc) < rules.JumpMinCapacity)
            return $"{PlayerCommands.Conflict} jump: {npc.Name} não tem força para pular";
        return null;
    }

    /// <summary>O corpo pula: gasta sono, fica no ar por <see cref="AirMs"/> e devolve esse tempo.</summary>
    public static Result<int> Jump(WorldState world, Npc npc)
    {
        if (Refusal(world, npc) is { } refused) return Result<int>.Fail(refused);
        long now = world.CurrentDate.TotalMilliseconds;
        int air = AirMs(world, npc);
        npc.Jump(now + air, world.Players.JumpSleepCost, now);
        return Result<int>.Ok(air);
    }
}
