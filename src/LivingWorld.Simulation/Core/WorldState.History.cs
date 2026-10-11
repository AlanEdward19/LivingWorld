using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.History.Distortion;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.History;

namespace LivingWorld.Simulation.Core;

// Parte de WorldState — operações sobre fatos, relatos, livros e memórias. O estado (campos e propriedades) fica em WorldState.cs.
public sealed partial class WorldState
{
    internal FactId NextFactIdAndAdvance() => new(_nextFactId++);

    internal ReportId NextReportIdAndAdvance() => new(_nextReportId++);

    internal BookId NextBookIdAndAdvance() => new(_nextBookId++);

    internal void RegisterReport(ReportState report)
    {
        _reports.Add(report);
        HistoryIndex.OnReportAdded(report, this);
    }

    internal ReportState? FindReport(ReportId id)
    {
        foreach (var report in _reports)
        {
            if (report.Id == id)
                return report;
        }
        return null;
    }

    internal void AddBook(Book book) => _books.Add(book);

    internal Book? FindBook(BookId id)
    {
        foreach (var book in _books)
        {
            if (book.Id == id)
                return book;
        }
        return null;
    }

    internal void ReplaceBook(Book updated)
    {
        for (int i = 0; i < _books.Count; i++)
        {
            if (_books[i].Id == updated.Id)
            {
                _books[i] = updated;
                return;
            }
        }
    }

    internal void AddFact(Fact fact)
    {
        _facts.Add(fact);
        HistoryIndex.OnFactAdded(fact);
    }

    internal Fact? FindFact(FactId id)
    {
        foreach (var fact in _facts)
        {
            if (fact.Id == id)
                return fact;
        }
        return null;
    }

    /// <summary>Único ponto de criação de <see cref="NpcMemory"/>
    /// — a classificação canônico/volátil é decidida aqui, uma vez, contra
    /// <paramref name="canonicalImportanceThreshold"/> (vem de <c>LlmRules</c> do cenário no
    /// chamador, mesmo padrão de <c>ConversationAvailabilityPolicy</c>: a regra nunca mora em
    /// <see cref="WorldState"/>, só o efeito de aplicá-la).</summary>
    internal void AddNpcMemory(
        NpcId ownerId, MemoryCategory category, string content, int importance, long originTick,
        IReadOnlyList<NpcId> participants, CellCoord location, int canonicalImportanceThreshold)
    {
        var memory = new NpcMemory(_nextMemoryId++, ownerId, category, content, importance, originTick, participants, location);
        if (importance >= canonicalImportanceThreshold)
            _canonicalMemories.Add(memory);
        else
            _volatileMemories.Add(memory);
    }

    /// <summary>Único ponto de mutação de <see cref="VolatileMemories"/> por compactação
    /// (<c>MemoryCompactionJob</c>) — troca um grupo de memórias
    /// voláteis por um resumo. Não passa por <see cref="AddNpcMemory"/>/<c>_nextMemoryId</c> de
    /// propósito: <see cref="NextMemoryId"/> é <see cref="CanonicalAttribute"/> e compactação
    /// precisa deixar o hash canônico intacto (spec.md, "hash canônico permanece idêntico"), por
    /// isso <paramref name="replacement"/> reaproveita o <see cref="NpcMemory.Id"/> de uma das
    /// memórias removidas em vez de tirar um id novo do contador compartilhado.</summary>
    internal void ReplaceVolatileMemories(IReadOnlyList<long> idsToRemove, NpcMemory replacement)
    {
        var idSet = idsToRemove.ToHashSet();
        _volatileMemories.RemoveAll(m => idSet.Contains(m.Id));
        _volatileMemories.Add(replacement);
    }
}
