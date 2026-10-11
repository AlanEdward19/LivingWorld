using LivingWorld.Domain.Fauna;
using LivingWorld.Domain.Flora;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Snapshot;

namespace LivingWorld.Simulation.Core;

// Parte de WorldState — operações sobre fauna, flora e ajustes de temperatura. O estado (campos e propriedades) fica em WorldState.cs.
public sealed partial class WorldState
{
    private void SyncFlora()
    {
        var flush = FloraPendingFlush;
        if (flush is null) return;
        FloraPendingFlush = null;
        flush();
    }

    /// <summary>Aplica <c>estágio += delta × ticks</c> a cada planta, na ordem da lista, sem passar pelas
    /// APIs de mutação (que sincronizam e invalidam). Só o avanço adiado chama.</summary>
    internal void ApplyFloraStageDeltas(int[] deltas, int ticks)
    {
        for (int i = 0; i < deltas.Length; i++)
        {
            if (deltas[i] == 0) continue;
            var plant = _flora[i];
            var advanced = plant with { GrowthStage = unchecked(plant.GrowthStage + deltas[i] * ticks) };
            _flora[i] = advanced;
            _floraById[advanced.Id] = advanced;
        }
    }
    [Canonical] public long NextPlantId => _nextPlantId;

    internal void AddEnvironmentTemperatureAdjustment(EnvironmentTemperatureAdjustment adjustment)
    {
        _environmentTemperatureAdjustments.Add(adjustment);
        TemperatureAdjustmentsVersion++;
        CanonicalHashCache.MarkPropertyDirty(nameof(EnvironmentTemperatureAdjustments));
    }

    internal void ReplaceSeasonalEnvironmentTemperatureAdjustments(
        IReadOnlyList<EnvironmentTemperatureAdjustment> replacements)
    {
        _environmentTemperatureAdjustments.RemoveAll(
            adjustment => adjustment.UntilTick == Geography.TemperatureSeasonSystem.SeasonalUntilTick);
        foreach (var adjustment in replacements)
            _environmentTemperatureAdjustments.Add(adjustment);
        TemperatureAdjustmentsVersion++;
        CanonicalHashCache.MarkPropertyDirty(nameof(EnvironmentTemperatureAdjustments));
    }

    internal AnimalId NextAnimalIdAndAdvance() => new(_nextAnimalId++);

    // Fauna e flora ficam sempre ordenadas por Id (invariante do construtor). Add/Replace/Remove
    // preservam a ordem por busca binária — O(log n) + deslocamento, em vez de RemoveAll + Sort
    // completo por entidade, que tornava o tick horário O(n² log n).
    public void AddAnimal(Animal animal)
    {
        _faunaById[animal.Id] = animal;
        InsertSortedById(_fauna, animal, a => a.Id.Value);
    }

    public Animal? FindAnimal(AnimalId id) => _faunaById.GetValueOrDefault(id);

    internal void ReplaceAnimal(Animal animal)
    {
        _faunaById[animal.Id] = animal;
        ReplaceSortedById(_fauna, animal, a => a.Id.Value);
    }

    internal void RemoveAnimal(AnimalId id)
    {
        RemoveSortedById(_fauna, id.Value, a => a.Id.Value);
        _faunaById.Remove(id);
    }

    internal PlantId NextPlantIdAndAdvance() => new(_nextPlantId++);

    public void AddPlant(Plant plant)
    {
        SyncFlora();
        FloraMutationVersion++;
        _floraById[plant.Id] = plant;
        InsertSortedById(_flora, plant, p => p.Id.Value);
    }

    public Plant? FindPlant(PlantId id)
    {
        SyncFlora();
        return _floraById.GetValueOrDefault(id);
    }

    internal void ReplacePlant(Plant plant)
    {
        SyncFlora();
        FloraMutationVersion++;
        _floraById[plant.Id] = plant;
        ReplaceSortedById(_flora, plant, p => p.Id.Value);
    }

    /// <summary>Troca a planta na posição conhecida <paramref name="index"/> (quem varre a lista sabe
    /// onde está); se o índice não bater com o Id, cai no caminho por busca.</summary>
    internal void ReplacePlantAt(int index, Plant plant)
    {
        SyncFlora();
        FloraMutationVersion++;
        if ((uint)index < (uint)_flora.Count && _flora[index].Id == plant.Id)
        {
            _flora[index] = plant;
            _floraById[plant.Id] = plant;
        }
        else
        {
            ReplacePlant(plant);
        }
    }

    internal void RemovePlant(PlantId id)
    {
        SyncFlora();
        FloraMutationVersion++;
        RemoveSortedById(_flora, id.Value, p => p.Id.Value);
        _floraById.Remove(id);
    }
}
