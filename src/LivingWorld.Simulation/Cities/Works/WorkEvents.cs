using LivingWorld.Domain.History;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Cities.Works;

/// <summary>Eventos de obra com lugar (AD-184): todo evento de obra leva o id da obra, a cidade, o que ela levanta e a célula, para o
/// cliente marcar no mapa. Payload: <c>obraId|cidadeId|tipo|x|y|...</c> (tipo: casa, oficina, predio-N, calcamento).</summary>
internal static class WorkEvents
{
    public const string SourceSystem = "build-works";

    public static long Log(TickContext ctx, WorldEventKind kind, long workId, Guid city, string type, int x, int y, string rest,
        string sourceSystem = SourceSystem, long? cause = null) =>
        ctx.LogEvent(kind, $"{workId}|{city}|{type}|{x}|{y}|{rest}", sourceSystem: sourceSystem, causeEventId: cause is > 0 ? cause : null);
}
