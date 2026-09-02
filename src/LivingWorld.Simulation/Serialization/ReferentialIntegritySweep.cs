using System.Collections;
using System.Reflection;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Fauna;
using LivingWorld.Domain.Flora;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Serialization;

/// <summary>Sweep genérico de integridade referencial (task 12): por reflexão sobre o grafo
/// público de <see cref="WorldState"/>, garante que toda referência a um id aponta para uma
/// entidade que existe. Cobertura cresce sozinha a cada fase nova — um tipo de id novo sem
/// entrada em <see cref="ValidIdResolvers"/> reprova o teste de cobertura
/// (LivingWorld.Tests), nunca passa silenciosamente sem checagem.</summary>
public static class ReferentialIntegritySweep
{
    /// <summary>Como obter o conjunto de ids válidos de cada tipo, dado o mundo. Tipo ainda sem
    /// uso real resolve para conjunto vazio — vazio e sem-referência passa por vacuidade, o
    /// esperado.</summary>
    // Fase 8, T6: CityId agora resolve contra world.Cities de verdade. O sentinela
    // default(CityId) continua aceito além dos ids reais — Foundation (T1-T8) não liga nenhum
    // sistema que atribua cidade de verdade a Npc/Household ainda (isso entra em fases
    // seguintes/CityScenarioLoader), então todo Npc/Household do cenário default carrega o
    // sentinela "ainda sem cidade atribuída", que não é um ponteiro solto — é a ausência
    // documentada de atribuição, distinta de apontar para uma cidade removida/inexistente.
    // BuildingId/LocationId seguem sem uso real (nenhum campo do domínio guarda um valor desses
    // tipos ainda) — vazio por vacuidade, mesmo padrão anterior à Fase 8.
    private static readonly Dictionary<Type, Func<WorldState, HashSet<object>>> ValidIdResolvers = new()
    {
        [typeof(NpcId)] = w => w.Npcs.Select(n => (object)n.Id).ToHashSet(),
        [typeof(HouseholdId)] = w => w.Households.Select(h => (object)h.Id).ToHashSet(),
        [typeof(RegionId)] = w => w.Map.Regions.Select(r => (object)r.Id).ToHashSet(),
        [typeof(CultureId)] = w => w.PopulationCatalog.CultureIds.Select(id => (object)new CultureId(id)).ToHashSet(),
        [typeof(BranchId)] = w => [w.BranchId],
        [typeof(CityId)] = w => w.Cities.Select(c => (object)c.Id).Append((object)default(CityId)).ToHashSet(),
        [typeof(LocationId)] = _ => [],
        [typeof(WorkplaceId)] = w => w.Workplaces.Select(wp => (object)wp.Id).ToHashSet(),
        [typeof(BuildingId)] = w => w.Buildings.Select(b => (object)b.Id).ToHashSet(),
        [typeof(RestPlaceId)] = w => w.RestPlaces.Select(place => (object)place.Id).ToHashSet(),
        [typeof(ResourceProcessId)] = w => w.ResourceProcesses.Select(process => (object)process.Id).ToHashSet(),
        [typeof(CropBatchId)] = w => w.CropBatches.Select(crop => (object)crop.Id).ToHashSet(),
        [typeof(FactId)] = w => w.Facts.Select(f => (object)f.Id).ToHashSet(),
        [typeof(ReportId)] = w => w.Reports.Select(r => (object)r.Id)
            .Concat(w.Cities.SelectMany(c => c.CanonSlots).Select(r => (object)r.Id))
            .ToHashSet(),
        [typeof(BookId)] = w => w.Books.Select(b => (object)b.Id).ToHashSet(),
        // Fase 12: NarrativeDocument.Id vive dentro de ChronicleGenerationSystem (chave
        // (local, período)), não em WorldState — nenhum campo do domínio hoje guarda uma
        // referência a NarrativeId, então vazio por vacuidade, mesmo padrão de LocationId.
        [typeof(NarrativeId)] = _ => [],
        [typeof(AnimalId)] = w => w.Fauna.Select(animal => (object)animal.Id).ToHashSet(),
        [typeof(PlantId)] = w => w.Flora.Select(plant => (object)plant.Id).ToHashSet(),
        [typeof(CombatEncounterId)] = w => w.CombatEncounters.Select(e => (object)e.Id).ToHashSet(),
        // Fase 28: rotas cosméticas vivem em CosmeticDetail (efêmero) — nenhum campo canônico
        // referencia RouteId ainda; vazio por vacuidade até haver consumidor causal real.
        [typeof(RouteId)] = _ => [],
    };

    /// <summary>Todo tipo de id do assembly Domain — o teste de cobertura reprova se algum
    /// aparecer aqui sem entrada correspondente em <see cref="ValidIdResolvers"/>.</summary>
    public static IReadOnlyList<Type> AllIdTypesInDomainAssembly() =>
        typeof(NpcId).Assembly.GetTypes()
            .Where(t => t.IsValueType && !t.IsEnum && t.Name.EndsWith("Id", StringComparison.Ordinal))
            .ToList();

    public static IReadOnlyList<Type> RegisteredIdTypes => ValidIdResolvers.Keys.ToList();

    /// <summary>Devolve toda violação encontrada — vazio significa mundo íntegro.</summary>
    public static IReadOnlyList<string> Check(WorldState world)
    {
        var validSets = ValidIdResolvers.ToDictionary(kv => kv.Key, kv => kv.Value(world));
        var violations = new List<string>();
        Walk(world, "world", validSets, violations, []);
        return violations;
    }

    private static void Walk(
        object? value, string path, Dictionary<Type, HashSet<object>> validSets,
        List<string> violations, HashSet<object> visited)
    {
        if (value is null) return;
        var type = value.GetType();

        if (validSets.TryGetValue(type, out var validSet))
        {
            if (!validSet.Contains(value))
                violations.Add($"{path}: {type.Name} '{value}' não existe no mundo");
            return; // Id é folha — não desce nos campos internos dele
        }

        if (type.IsPrimitive || value is string || type.IsEnum) return;

        if (value is IEnumerable enumerable)
        {
            int i = 0;
            foreach (var item in enumerable)
            {
                Walk(item, $"{path}[{i}]", validSets, violations, visited);
                i++;
            }
            return;
        }

        // Ciclo só é possível entre referências (classes) — value types não criam ciclo real,
        // e comparar boxed structs por igualdade de valor daria falso positivo de "já visitado".
        if (!type.IsValueType && !visited.Add(value)) return;

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0) continue;
            object? propValue;
            try { propValue = prop.GetValue(value); }
            catch { continue; }
            Walk(propValue, $"{path}.{prop.Name}", validSets, violations, visited);
        }
    }
}
