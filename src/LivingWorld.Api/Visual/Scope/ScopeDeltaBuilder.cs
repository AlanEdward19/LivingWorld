using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Api.Visual.Scope;

/// <summary>Diff puro entre duas fotos de posição de um escopo — nunca
/// recebe <c>WorldState</c> nem invoca <c>GlobalLayerBuilder</c>/<c>CityLayerBuilder</c>. Só o
/// snapshot inicial monta camadas; o caminho de delta nunca as recomputa.</summary>
public static class ScopeDeltaBuilder
{
    /// <summary>AD-172: <paramref name="tick"/> menos o tick de <paramref name="before"/> é quanto os processos lineares (descanso e
    /// refeição) avançaram; processo que andou exatamente o previsto por <see cref="ProcessAdvance.Advance"/> não vai no delta.</summary>
    public static ScopeTickDelta Diff(long tick, LivingScopeState before, LivingScopeState after)
    {
        long elapsed = tick - before.Tick;
        var resets = new List<NpcId>();
        var npcUpserts = ChangedNpcs(before.Npcs, after.Npcs, resets);
        var npcRemoved = Removed(before.Npcs, after.Npcs, item => item.Id);
        return new(
            tick,
            npcUpserts,
            resets.Count == 0 ? npcRemoved : [.. npcRemoved, .. resets],
            Changed(before.Cities, after.Cities, item => item.Id),
            Removed(before.Cities, after.Cities, item => item.Id),
            Changed(before.Buildings, after.Buildings, item => item.Id),
            Removed(before.Buildings, after.Buildings, item => item.Id),
            ChangedProcesses(before.Processes, after.Processes, elapsed),
            Removed(before.Processes, after.Processes, item => item.Id),
            after.Indicators,
            after.Events)
        {
            BuildingStateUpserts = Changed(before.BuildingStates, after.BuildingStates, item => item.BuildingId),
            BuildingStateRemoved = Removed(before.BuildingStates, after.BuildingStates, item => item.BuildingId),
            CropUpserts = Changed(before.Crops, after.Crops, item => item.Id),
            CropRemoved = Removed(before.Crops, after.Crops, item => item.Id),
            AnimalUpserts = Changed(before.Animals, after.Animals, item => item.Id),
            AnimalRemoved = Removed(before.Animals, after.Animals, item => item.Id),
            FaunaGroupUpserts = Changed(before.FaunaGroups, after.FaunaGroups, FaunaKey),
            FaunaGroupRemoved = Removed(before.FaunaGroups, after.FaunaGroups, FaunaKey),
            LeavingUpserts = Changed(before.Leavings, after.Leavings, item => item.Id),
            LeavingRemoved = Removed(before.Leavings, after.Leavings, item => item.Id),
            ObjectUpserts = Changed(before.Objects, after.Objects, item => item.Id),
            ObjectRemoved = Removed(before.Objects, after.Objects, item => item.Id),
            // AD-189: o clima só volta quando muda de verdade (dia do tempo, estado); a hora o cliente lê de hourlyC
            Climate = after.Climate is { } climate && climate.SameDayAs(before.Climate) ? null : after.Climate,
            RoadUpserts = ReferenceEquals(before.Roads, after.Roads) ? [] : Changed(before.Roads, after.Roads, item => item.Cell),
            RoadRemoved = ReferenceEquals(before.Roads, after.Roads) ? [] : Removed(before.Roads, after.Roads, item => item.Cell),
        };
    }

    /// <summary>Chave de um bando no delta: espécie e região (<c>espécie:região</c>).</summary>
    public static string FaunaKey(LivingWorld.Api.Visual.Observable.FaunaGroupVisual group) => $"{group.Species}:{group.RegionId}";

    public static ScopeTickDelta Diff(long tick, IReadOnlyDictionary<NpcId, CellCoord> before, IReadOnlyDictionary<NpcId, CellCoord> after)
    {
        var moved = new List<NpcPositionDelta>();
        foreach (var (id, location) in after)
        {
            if (!before.TryGetValue(id, out var previousLocation) || previousLocation != location)
                moved.Add(new NpcPositionDelta(id, location));
        }

        var removed = before.Keys.Where(id => !after.ContainsKey(id)).ToList();

        return new ScopeTickDelta(tick, moved, removed);
    }

    /// <summary>AD-170/AD-172: no upsert de um NPC que já estava no escopo, <c>observable</c> e <c>appearance</c> vêm omitidos e
    /// <c>city</c> vem <c>null</c> quando não mudaram; sem <c>actionStartedAtTick</c>, o grupo da ação (início, alvo, intenção e
    /// alvo da intenção) é o anterior. O cliente mantém o que já tinha. NPC novo no escopo sempre leva tudo.</summary>
    private static IReadOnlyList<NpcVisual> ChangedNpcs(IReadOnlyList<NpcVisual> before, IReadOnlyList<NpcVisual> after, List<NpcId> resets)
    {
        if (after.Count == 0) return [];
        var previous = Index(before, item => item.Id.Value);
        var changed = new List<NpcVisual>();
        foreach (var item in after)
        {
            if (!previous.TryGetValue(item.Id.Value, out var old)) { changed.Add(item); continue; }
            if (ReferenceEquals(old, item) || old.Equals(item)) continue;
            if (LostKeptField(old, item))
            {
                // um campo "ausente = igual" voltou a nulo: troca o registro inteiro (id em npcRemoved e em npcUpserts)
                resets.Add(item.Id);
                changed.Add(item);
                continue;
            }

            bool sameObservable = Equals(old.Observable, item.Observable);
            bool sameCity = old.City == item.City;
            bool sameAppearance = Equals(old.Appearance, item.Appearance);
            bool sameAction = old.ActionStartedAtTick == item.ActionStartedAtTick && Equals(old.ActionTarget, item.ActionTarget)
                && old.Intent == item.Intent && old.IntentTarget == item.IntentTarget;
            changed.Add(sameObservable || sameCity || sameAppearance || sameAction
                ? item with
                {
                    Observable = sameObservable ? null : item.Observable,
                    City = sameCity ? null : item.City,
                    Appearance = sameAppearance ? null : item.Appearance,
                    ActionStartedAtTick = sameAction ? null : item.ActionStartedAtTick,
                    ActionTarget = sameAction ? null : item.ActionTarget,
                    Intent = sameAction ? null : item.Intent,
                    IntentTarget = sameAction ? null : item.IntentTarget,
                }
                : item);
        }
        return changed;
    }

    private static bool LostKeptField(NpcVisual old, NpcVisual item) =>
        (old.Observable is not null && item.Observable is null) || (old.City is not null && item.City is null)
        || (old.Appearance is not null && item.Appearance is null) || (old.ActionStartedAtTick is not null && item.ActionStartedAtTick is null);

    private static IReadOnlyList<ProcessVisual> ChangedProcesses(
        IReadOnlyList<ProcessVisual> before, IReadOnlyList<ProcessVisual> after, long elapsed)
    {
        if (after.Count == 0) return [];
        var previous = Index(before, item => item.Id);
        List<ProcessVisual>? changed = null;
        foreach (var item in after)
            if (!previous.TryGetValue(item.Id, out var old) || !ProcessAdvance.Predicts(old, elapsed, item))
                (changed ??= new List<ProcessVisual>()).Add(item);
        return changed ?? (IReadOnlyList<ProcessVisual>)[];
    }

    private static IReadOnlyList<T> Changed<T, TId>(
        IReadOnlyList<T> before,
        IReadOnlyList<T> after,
        Func<T, TId> id) where TId : notnull
    {
        if (after.Count == 0) return [];
        var previous = Index(before, id);
        var comparer = EqualityComparer<T>.Default;
        List<T>? changed = null;
        foreach (var item in after)
            if (!previous.TryGetValue(id(item), out var old) || !comparer.Equals(old, item))
                (changed ??= new List<T>()).Add(item);
        return changed ?? (IReadOnlyList<T>)[];
    }

    private static IReadOnlyList<TId> Removed<T, TId>(
        IReadOnlyList<T> before,
        IReadOnlyList<T> after,
        Func<T, TId> id) where TId : notnull
    {
        if (before.Count == 0) return [];
        var current = new HashSet<TId>(after.Count);
        foreach (var item in after) current.Add(id(item));
        List<TId>? removed = null;
        foreach (var item in before)
            if (!current.Contains(id(item)))
                (removed ??= new List<TId>()).Add(id(item));
        return removed ?? (IReadOnlyList<TId>)[];
    }

    private static Dictionary<TId, T> Index<T, TId>(IReadOnlyList<T> items, Func<T, TId> id) where TId : notnull
    {
        var index = new Dictionary<TId, T>(items.Count);
        foreach (var item in items) index[id(item)] = item;
        return index;
    }
}

/// <summary>AD-172: avanço previsível de um processo entre dois ticks, o mesmo no servidor e no cliente. Descanso (<c>rest</c>) e
/// refeição (<c>food</c>) com <c>remainingHours</c> &gt; 0 e <c>progress</c> &lt; 1 perdem <c>elapsed</c> horas: a duração sai do
/// próprio registro, <c>round(remainingHours / (1 − progress))</c>, e <c>progress = clamp(1 − remainingHours / duração, 0, 1)</c>.
/// Qualquer outro processo fica como está. O servidor só manda o processo quando o real difere do previsto.</summary>
public static class ProcessAdvance
{
    public static ProcessVisual Advance(ProcessVisual process, long elapsed) =>
        TryAdvance(process, elapsed, out long left, out double progress)
            ? process with { RemainingHours = left, Progress = progress }
            : process;

    /// <summary>Verdadeiro quando <paramref name="current"/> é exatamente o avanço previsto de <paramref name="previous"/> (sem alocar).</summary>
    public static bool Predicts(ProcessVisual previous, long elapsed, ProcessVisual current)
    {
        if (!TryAdvance(previous, elapsed, out long left, out double progress)) return previous.Equals(current);
        // Os demais campos, como na igualdade do registro.
        return current.RemainingHours == left && current.Progress.Equals(progress)
            && previous.Id == current.Id && previous.Kind == current.Kind && previous.TargetId == current.TargetId
            && previous.DescriptorKey == current.DescriptorKey && Nullable.Equals(previous.Quality, current.Quality)
            && Nullable.Equals(previous.Location, current.Location) && Equals(previous.Footprint, current.Footprint)
            && previous.AppearanceToken == current.AppearanceToken && previous.ResourceId == current.ResourceId
            && previous.ResourceName == current.ResourceName;
    }

    private static bool TryAdvance(ProcessVisual process, long elapsed, out long left, out double progress)
    {
        left = 0;
        progress = 0;
        if (elapsed <= 0 || process.Kind is not ("rest" or "food") || process.RemainingHours is not { } remaining
            || remaining <= 0 || process.Progress >= 1)
            return false;
        long duration = (long)Math.Round(remaining / (1.0 - process.Progress));
        if (duration < 1) return false;
        left = Math.Max(0, remaining - elapsed);
        progress = Math.Clamp(1.0 - left / (double)duration, 0, 1);
        return true;
    }
}

public static class LivingDeltaReducer
{
    public static LivingScopeState Apply(LivingScopeState state, ScopeTickDelta delta) =>
        new(
            // AD-170: ids tipados não são comparáveis; ordena pelo valor, na mesma ordem da projeção
            Merge(state.Npcs, KeepUnchanged(state.Npcs, delta.NpcUpserts, delta.NpcRemoved), delta.NpcRemoved, item => item.Id, item => item.Id.Value),
            Merge(state.Cities, delta.CityUpserts, delta.CityRemoved, item => item.Id, item => item.Id.Value),
            Merge(state.Buildings, delta.BuildingUpserts, delta.BuildingRemoved, item => item.Id, item => item.Id.Value),
            Merge(Advanced(state.Processes, delta.Tick - state.Tick), delta.ProcessUpserts, delta.ProcessRemoved, item => item.Id),
            delta.Indicators,
            delta.Events)
        {
            BuildingStates = Merge(state.BuildingStates, delta.BuildingStateUpserts, delta.BuildingStateRemoved, item => item.BuildingId),
            Crops = Merge(state.Crops, delta.CropUpserts, delta.CropRemoved, item => item.Id),
            Animals = Merge(state.Animals, delta.AnimalUpserts, delta.AnimalRemoved, item => item.Id),
            FaunaGroups = Merge(state.FaunaGroups, delta.FaunaGroupUpserts, delta.FaunaGroupRemoved, ScopeDeltaBuilder.FaunaKey),
            Leavings = Merge(state.Leavings, delta.LeavingUpserts, delta.LeavingRemoved, item => item.Id),
            Objects = Merge(state.Objects, delta.ObjectUpserts, delta.ObjectRemoved, item => item.Id),
            // AD-189: sem clima no delta, a hora sai da curva do dia (o tick do delta à frente do estado)
            Climate = delta.Climate ?? (delta.Tick > state.Tick ? state.Climate?.AtTick(delta.Tick) : state.Climate),
            Roads = Merge(state.Roads, delta.RoadUpserts, delta.RoadRemoved, item => item.Cell, item => (item.Cell.Y, item.Cell.X)),
            Tick = delta.Tick,
        };

    /// <summary>Upsert de NPC sem observável, cidade, aparência ou grupo da ação mantém o que o estado já tinha (AD-170, AD-172).</summary>
    private static IReadOnlyList<NpcVisual> KeepUnchanged(
        IReadOnlyList<NpcVisual> current, IReadOnlyList<NpcVisual> upserts, IReadOnlyList<NpcId> removed)
    {
        if (upserts.All(u => u.Observable is not null && u.City is not null && u.Appearance is not null && u.ActionStartedAtTick is not null))
            return upserts;
        var known = current.ToDictionary(item => item.Id);
        foreach (var reset in removed) known.Remove(reset); // removido e reenviado no mesmo delta = registro novo inteiro
        return upserts.Select(u => known.TryGetValue(u.Id, out var old) ? Restore(u, old) : u).ToList();
    }

    private static NpcVisual Restore(NpcVisual upsert, NpcVisual old)
    {
        var kept = upsert with
        {
            Observable = upsert.Observable ?? old.Observable,
            City = upsert.City ?? old.City,
            Appearance = upsert.Appearance ?? old.Appearance,
        };
        return upsert.ActionStartedAtTick is not null
            ? kept
            : kept with
            {
                ActionStartedAtTick = old.ActionStartedAtTick,
                ActionTarget = old.ActionTarget,
                Intent = old.Intent,
                IntentTarget = old.IntentTarget,
            };
    }

    /// <summary>Processos que o delta não cita avançam como o servidor previu (AD-172).</summary>
    private static IReadOnlyList<ProcessVisual> Advanced(IReadOnlyList<ProcessVisual> processes, long elapsed) =>
        elapsed <= 0 ? processes : processes.Select(process => ProcessAdvance.Advance(process, elapsed)).ToList();

    private static IReadOnlyList<T> Merge<T, TId>(
        IReadOnlyList<T> current,
        IReadOnlyList<T> upserts,
        IReadOnlyList<TId> removed,
        Func<T, TId> id) where TId : notnull => Merge(current, upserts, removed, id, id);

    private static IReadOnlyList<T> Merge<T, TId, TOrder>(
        IReadOnlyList<T> current,
        IReadOnlyList<T> upserts,
        IReadOnlyList<TId> removed,
        Func<T, TId> id,
        Func<T, TOrder> order) where TId : notnull
    {
        var result = current.ToDictionary(id);
        foreach (var itemId in removed)
            result.Remove(itemId);
        foreach (var item in upserts)
            result[id(item)] = item;
        return result.Values.OrderBy(order).ToList();
    }
}
