namespace LivingWorld.Simulation;

/// <summary>Evento agendado no futuro (docs/domain/time-and-ticks.md): troca varredura por
/// população por O(eventos). <see cref="Id"/> é o desempate estável entre processos quando
/// dois eventos vencem no mesmo tick — nunca ordem de inserção.</summary>
public sealed record ScheduledEvent(long Id, long TargetTick, string SystemName, string? Payload = null);
