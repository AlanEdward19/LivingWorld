using System.Collections;
using System.Reflection;
using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Sweep genérico de integridade referencial (task 12): por reflexão sobre o grafo
/// público de <see cref="WorldState"/>, garante que toda referência a um id aponta para uma
/// entidade que existe. Cobertura cresce sozinha a cada fase nova — um tipo de id novo sem
/// entrada em <see cref="ValidIdResolvers"/> reprova o teste de cobertura
/// (LivingWorld.Tests), nunca passa silenciosamente sem checagem.</summary>
public static class ReferentialIntegritySweep
{
    /// <summary>Como obter o conjunto de ids válidos de cada tipo, dado o mundo. Tipo ainda sem
    /// uso real (ex.: <see cref="CityId"/>/<see cref="LocationId"/> antes da Fase 8) resolve
    /// para conjunto vazio — vazio e sem-referência passa por vacuidade, o esperado.</summary>
    private static readonly Dictionary<Type, Func<WorldState, HashSet<object>>> ValidIdResolvers = new()
    {
        [typeof(NpcId)] = w => w.Npcs.Select(n => (object)n.Id).ToHashSet(),
        [typeof(HouseholdId)] = w => w.Households.Select(h => (object)h.Id).ToHashSet(),
        [typeof(RegionId)] = w => w.Map.Regions.Select(r => (object)r.Id).ToHashSet(),
        [typeof(CultureId)] = w => w.PopulationCatalog.CultureIds.Select(id => (object)new CultureId(id)).ToHashSet(),
        [typeof(BranchId)] = w => [w.BranchId],
        [typeof(CityId)] = _ => [],
        [typeof(LocationId)] = _ => [],
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
