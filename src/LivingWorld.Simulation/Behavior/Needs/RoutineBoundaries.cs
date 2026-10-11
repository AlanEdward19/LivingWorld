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
        Tables.GetValue(catalog, _ => new())
            .GetOrAdd((profession?.Id ?? -1, stage), _ => Build(catalog, profession, stage, hoursPerDay));

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
