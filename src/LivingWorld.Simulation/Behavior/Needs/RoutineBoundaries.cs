using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;

namespace LivingWorld.Simulation.Behavior.Needs;

/// <summary>Quantas horas faltam para a rotina do NPC mudar de ação — tabela por (profissão, fase da
/// vida) construída uma vez por catálogo, para o despertar na fronteira da rotina custar O(1) por wake.
/// Quem dorme ou trabalha por uma duração máxima fixa só reavalia o dia quando acorda; sem este despertar
/// o sono vai até o fim das 8 h mesmo que o turno comece antes, e o ciclo diário deriva.</summary>
internal static class RoutineBoundaries
{
    private sealed record Table(ActionType[] Actions, int[] UntilChange);

    private static readonly ConditionalWeakTable<ActionCatalog, ConcurrentDictionary<(int Profession, LifeStage Stage), Table>> Tables = new();

    private static Table TableFor(ActionCatalog catalog, ProfessionType? profession, LifeStage stage, int hoursPerDay) =>
        TableFor(Tables.GetValue(catalog, static _ => new()), catalog, profession, stage, hoursPerDay);

    /// <summary>Consulta sem alocar no caso comum: o lambda de <c>GetOrAdd</c> capturava quatro valores e alocava
    /// fecho e delegate a cada despertar, mesmo com a tabela já pronta.</summary>
    private static Table TableFor(
        ConcurrentDictionary<(int Profession, LifeStage Stage), Table> tables, ActionCatalog catalog,
        ProfessionType? profession, LifeStage stage, int hoursPerDay)
    {
        var key = (profession?.Id ?? -1, stage);
        return tables.TryGetValue(key, out var table) ? table : AddTable(tables, key, catalog, profession, stage, hoursPerDay);
    }

    /// <summary>Caminho raro (primeira vez do par). Fica em método próprio porque o compilador aloca o fecho de um lambda
    /// no início do método que o declara: com o lambda em <c>TableFor</c>, todo despertar pagava o fecho mesmo com a
    /// tabela pronta (o maior alocador do tick: 16% dos bytes a 10.000 NPCs).</summary>
    private static Table AddTable(
        ConcurrentDictionary<(int Profession, LifeStage Stage), Table> tables, (int, LifeStage) key, ActionCatalog catalog,
        ProfessionType? profession, LifeStage stage, int hoursPerDay) =>
        tables.GetOrAdd(key, _ => Build(catalog, profession, stage, hoursPerDay));

    /// <summary>Ação de rotina da hora — a mesma de <see cref="ActionCatalog.RoutineOf"/>, lida de uma tabela de
    /// 24 horas montada uma vez por (profissão, fase): a busca por faixas rodava várias vezes por despertar.</summary>
    public static ActionType RoutineAt(
        ActionCatalog catalog, ProfessionType? profession, LifeStage stage, int hour, int hoursPerDay) =>
        TableFor(catalog, profession, stage, hoursPerDay).Actions[hour % hoursPerDay];

    /// <summary>Horas até a ação de rotina diferir da atual (1..<paramref name="hoursPerDay"/>);
    /// <c>hoursPerDay</c> quando a rotina é constante o dia todo.</summary>
    public static int HoursUntilChange(
        ActionCatalog catalog, ProfessionType? profession, LifeStage stage, int hour, int hoursPerDay)
        => TableFor(catalog, profession, stage, hoursPerDay).UntilChange[hour % hoursPerDay];

    private static Table Build(ActionCatalog catalog, ProfessionType? profession, LifeStage stage, int hoursPerDay)
    {
        var actions = new ActionType[hoursPerDay];
        for (int h = 0; h < hoursPerDay; h++)
            actions[h] = catalog.RoutineOf(profession, stage, h);

        var untilChange = new int[hoursPerDay];
        for (int h = 0; h < hoursPerDay; h++)
        {
            int ahead = 1;
            while (ahead < hoursPerDay && actions[(h + ahead) % hoursPerDay] == actions[h]) ahead++;
            untilChange[h] = ahead;
        }
        return new Table(actions, untilChange);
    }
}
