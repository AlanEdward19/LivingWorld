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

/// <summary>Sweep genérico de integridade referencial: por reflexão sobre o grafo
/// público de <see cref="WorldState"/>, garante que toda referência a um id aponta para uma
/// entidade que existe. Cobertura cresce sozinha a cada fase nova — um tipo de id novo sem
/// entrada em <see cref="ValidIdResolvers"/> reprova o teste de cobertura
/// (LivingWorld.Tests), nunca passa silenciosamente sem checagem.</summary>
public static class ReferentialIntegritySweep
{
    /// <summary>Como obter o conjunto de ids válidos de cada tipo, dado o mundo. Tipo ainda sem
    /// uso real resolve para conjunto vazio — vazio e sem-referência passa por vacuidade, o
    /// esperado.</summary>
    // CityId resolve contra world.Cities de verdade. O sentinela
    // default(CityId) continua aceito além dos ids reais — nenhum
    // sistema atribui cidade de verdade a Npc/Household ainda (isso entra via
    // CityScenarioLoader), então todo Npc/Household do cenário default carrega o
    // sentinela "ainda sem cidade atribuída", que não é um ponteiro solto — é a ausência
    // documentada de atribuição, distinta de apontar para uma cidade removida/inexistente.
    // BuildingId/LocationId seguem sem uso real (nenhum campo do domínio guarda um valor desses
    // tipos ainda) — vazio por vacuidade.
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
        // NarrativeDocument.Id vive dentro de ChronicleGenerationSystem (chave
        // (local, período)), não em WorldState — nenhum campo do domínio hoje guarda uma
        // referência a NarrativeId, então vazio por vacuidade, mesmo padrão de LocationId.
        [typeof(NarrativeId)] = _ => [],
        [typeof(AnimalId)] = w => w.Fauna.Select(animal => (object)animal.Id).ToHashSet(),
        [typeof(PlantId)] = w => w.Flora.Select(plant => (object)plant.Id).ToHashSet(),
        [typeof(CombatEncounterId)] = w => w.CombatEncounters.Select(e => (object)e.Id).ToHashSet(),
        // Rotas cosméticas vivem em CosmeticDetail (efêmero) — nenhum campo canônico
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
        Visit(world, null, "world", -1, validSets, violations, new HashSet<object>(ReferenceEqualityComparer.Instance));
        return violations;
    }

    /// <summary>Caminho até o nó visitado (<c>world.Npcs[3].Employer</c>). Só vira texto quando há
    /// violação, e só se aloca para nós internos — montar uma string por elemento do mundo inteiro era
    /// o custo dominante do sweep.</summary>
    private sealed class PathNode(PathNode? parent, string? property, int index)
    {
        public string Render() =>
            (parent?.Render() ?? "") + (property is null ? $"[{index}]" : parent is null ? property : "." + property);
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, PropertyInfo[]> PropertiesByType = new();

    private static PropertyInfo[] PropertiesOf(Type type) =>
        PropertiesByType.GetOrAdd(type, t => t
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(prop => prop.GetIndexParameters().Length == 0)
            .ToArray());

    private static void Visit(
        object? value, PathNode? parent, string? property, int index,
        Dictionary<Type, HashSet<object>> validSets, List<string> violations, HashSet<object> visited)
    {
        if (value is null) return;
        var type = value.GetType();

        if (validSets.TryGetValue(type, out var validSet))
        {
            if (!validSet.Contains(value))
                violations.Add($"{new PathNode(parent, property, index).Render()}: {type.Name} '{value}' não existe no mundo");
            return; // Id é folha — não desce nos campos internos dele
        }

        if (type.IsPrimitive || value is string || type.IsEnum) return;

        // Delegates (notificadores de NPC) e objetos de reflexão não carregam estado de domínio:
        // descer neles percorria o grafo de metadados do .NET inteiro (dezenas de milhões de nós).
        if (value is Delegate || value is MemberInfo || value is Type || value is Assembly || value is Module) return;

        var path = new PathNode(parent, property, index);

        if (value is IEnumerable enumerable)
        {
            int i = 0;
            foreach (var item in enumerable)
            {
                Visit(item, path, null, i, validSets, violations, visited);
                i++;
            }
            return;
        }

        // Ciclo só é possível entre referências (classes) — value types não criam ciclo real,
        // e boxed structs comparados por valor dariam falso positivo de "já visitado".
        if (!type.IsValueType && !visited.Add(value)) return;

        foreach (var prop in PropertiesOf(type))
        {
            object? propValue;
            try { propValue = prop.GetValue(value); }
            catch { continue; }
            Visit(propValue, path, prop.Name, -1, validSets, violations, visited);
        }
    }
}
