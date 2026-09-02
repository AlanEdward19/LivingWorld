using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Behavior;

/// <summary>Slot de rotina diária (Fase 4, task 2): profissão específica ou <c>null</c> ("any"
/// — mesmo padrão de vazio-é-sem-restrição de <see cref="PopulationCatalog"/>), estágio de
/// vida e janela de hora `[HourStart, HourEnd]` (inclusiva) em que <see cref="Action"/>
/// vale.</summary>
public sealed record RoutineSlot(int? ProfessionId, LifeStage Stage, int HourStart, int HourEnd, ActionType Action);

/// <summary>Catálogo de ações do cenário (Fase 4, task 2): duração máxima obrigatória por
/// ação (as 6 do catálogo fechado) e a tabela de rotina diária por profissão/estágio de
/// vida/hora.</summary>
public sealed record ActionCatalog(
    IReadOnlyDictionary<ActionType, int> MaxDurationHours,
    IReadOnlyList<RoutineSlot> RoutineSlots,
    ActionType DefaultAction)
{
    public static Result<ActionCatalog> Create(
        IReadOnlyDictionary<ActionType, int> maxDurationHours,
        IReadOnlyList<RoutineSlot> routineSlots,
        ActionType defaultAction)
    {
        foreach (var action in Enum.GetValues<ActionType>())
        {
            if (!maxDurationHours.TryGetValue(action, out int duration))
                return Result<ActionCatalog>.Fail($"MaxDurationHours: falta duração declarada para {action}");
            if (duration <= 0)
                return Result<ActionCatalog>.Fail($"MaxDurationHours[{action}]: deve ser positivo");
        }

        return Result<ActionCatalog>.Ok(new ActionCatalog(maxDurationHours, routineSlots, defaultAction));
    }

    /// <summary>Resolve a ação de rotina: slot específico da profissão, senão slot "any"
    /// (<c>ProfessionId: null</c>), senão <see cref="DefaultAction"/> — nunca lança
    /// exceção.</summary>
    public ActionType RoutineOf(ProfessionType? profession, LifeStage stage, int hour)
    {
        foreach (var slot in RoutineSlots)
        {
            if (slot.ProfessionId != profession?.Id) continue;
            if (slot.Stage != stage) continue;
            if (hour < slot.HourStart || hour > slot.HourEnd) continue;
            return slot.Action;
        }

        foreach (var slot in RoutineSlots)
        {
            if (slot.ProfessionId is not null) continue;
            if (slot.Stage != stage) continue;
            if (hour < slot.HourStart || hour > slot.HourEnd) continue;
            return slot.Action;
        }

        return DefaultAction;
    }
}
