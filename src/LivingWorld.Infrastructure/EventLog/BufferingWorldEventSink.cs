using LivingWorld.Simulation.Core;

namespace LivingWorld.Infrastructure.EventLog;

/// <summary>Acumula eventos de história em memória entre duas fronteiras de snapshot (task 10)
/// — o banco só é tocado quando <see cref="DrainAll"/> é escrito, nunca durante o tick
/// (rules/database-entities.md: "zero round-trips de banco durante o tick").</summary>
public sealed class BufferingWorldEventSink : IWorldEventSink
{
    private readonly List<WorldEvent> _buffer = [];

    public void Record(WorldEvent evt) => _buffer.Add(evt);

    public IReadOnlyList<WorldEvent> EventsAt(long tick) => _buffer.Where(evt => evt.Tick == tick).ToList();

    /// <summary>Devolve e esvazia o buffer — chamado só na fronteira de snapshot.</summary>
    public IReadOnlyList<WorldEvent> DrainAll()
    {
        var copy = _buffer.ToList();
        _buffer.Clear();
        return copy;
    }
}
