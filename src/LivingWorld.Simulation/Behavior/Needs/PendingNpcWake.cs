namespace LivingWorld.Simulation.Behavior.Needs;

/// <summary>Despertar pendente de um NPC, como entra no snapshot (AD-129). <see cref="Sequence"/> sai do mesmo
/// contador de <c>WorldState.NextEventId</c> no momento do agendamento e decide a ordem do lote: no mesmo tick,
/// quem foi agendado por último acorda primeiro (a ordem de quando o despertar era um <c>ScheduledEvent</c>).</summary>
public sealed record PendingNpcWake(long NpcId, long TargetTick, long Sequence);
