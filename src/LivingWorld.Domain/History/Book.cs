using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.History;

/// <summary>Objeto do mundo que carrega um relato por meio Livro/Crônica (Fase 10, HIST-09) —
/// pode ser copiado (com erro de copista), perdido e redescoberto por evento declarado. A linha
/// nunca é apagada; <see cref="Lost"/>/<see cref="RediscoveredAtTick"/> só marcam estado.</summary>
public sealed record Book(
    BookId Id,
    ReportId CarriesReportId,
    BookId? CopyOfBookId,
    bool Lost,
    long? LostAtTick,
    long? RediscoveredAtTick)
{
    public Book MarkLost(long tick) =>
        Lost ? this : this with { Lost = true, LostAtTick = tick };

    public Book WithRediscovered(long tick) =>
        this with { Lost = false, RediscoveredAtTick = tick };
}
