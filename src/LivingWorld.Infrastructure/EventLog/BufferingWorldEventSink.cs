using LivingWorld.Simulation.Core;

namespace LivingWorld.Infrastructure.EventLog;

/// <summary>Acumula eventos de história em memória entre duas fronteiras de snapshot
/// — o banco só é tocado quando <see cref="DrainAll"/> é escrito, nunca durante o tick
/// (rules/database-entities.md: "zero round-trips de banco durante o tick").</summary>
public sealed class BufferingWorldEventSink : IWorldEventSink
{
    private readonly List<WorldEvent> _buffer = [];

    public void Record(WorldEvent evt) => _buffer.Add(evt);

    public IReadOnlyList<WorldEvent> EventsAt(long tick) => _buffer.Where(evt => evt.Tick == tick).ToList();

    /// <summary>AD-170: os eventos mais novos primeiro que casam com <paramref name="match"/>, olhando no máximo
    /// <paramref name="scanLimit"/> do fim do buffer (leitura para a inspeção; não esvazia nada).</summary>
    public IReadOnlyList<WorldEvent> NewestMatching(Func<WorldEvent, bool> match, int take, int scanLimit)
    {
        var found = new List<WorldEvent>();
        for (int i = _buffer.Count - 1, seen = 0; i >= 0 && seen < scanLimit && found.Count < take; i--, seen++)
            if (match(_buffer[i])) found.Add(_buffer[i]);
        return found;
    }

    /// <summary>Devolve e esvazia o buffer — chamado só na fronteira de snapshot.</summary>
    public IReadOnlyList<WorldEvent> DrainAll()
    {
        var copy = _buffer.ToList();
        _buffer.Clear();
        return copy;
    }
}
