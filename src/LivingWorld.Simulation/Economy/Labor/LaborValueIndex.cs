using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy.Labor;

/// <summary>Valor do trabalho de um trabalhador em cada oficina, uma vez por dia: o que a receita rende por
/// trabalhador (saída × preço local, menos o insumo ao preço-piso), mínimo 1. É o que atrai gente para uma oficina:
/// bem escasso → preço sobe → o trabalho dele vale mais. Oficina sem receita vale o mínimo.
/// A saída de comida de plantas não conta como trabalho de oficina (a produção a ignora quando há processo vegetal).</summary>
public sealed class LaborValueIndex
{
    public const double MinimumValue = 1.0;

    private readonly WorldState _world;
    private Dictionary<WorkplaceId, double>? _valueByWorkplace;
    private Dictionary<CityId, Workplace[]>? _byCity;
    private Dictionary<int, int>? _professionByLocationType;

    private LaborValueIndex(WorldState world) => _world = world;

    public double ValueOf(Workplace workplace)
    {
        Build();
        return _valueByWorkplace!.GetValueOrDefault(workplace.Id, MinimumValue);
    }

    /// <summary>Oficinas da cidade em ordem de id (a ordem decide empates); a vaga é conferida por quem usa,
    /// ao vivo, porque o preenchimento muda dentro do tick.</summary>
    /// <summary>Array de propósito: percorrê-lo por índice não aloca enumerador a cada avaliação de NPC.</summary>
    public Workplace[] In(CityId city)
    {
        Build();
        return _byCity!.TryGetValue(city, out var list) ? list : [];
    }

    /// <summary>Menor id de profissão que trabalha num tipo de local, se algum.</summary>
    public int? ProfessionAt(int locationTypeId)
    {
        Build();
        return _professionByLocationType!.TryGetValue(locationTypeId, out var id) ? id : null;
    }

    private sealed class Memo
    {
        public long Day = -1;
        public LaborValueIndex? Index;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<WorldState, Memo> Memos = new();

    /// <summary>O valor do trabalho muda no ritmo dos preços, não da hora: o índice vale o dia inteiro e só é
    /// calculado na primeira consulta (a maioria dos ticks ninguém precisa). A vaga é conferida ao vivo.</summary>
    public static LaborValueIndex BuildForTick(WorldState world)
    {
        var memo = Memos.GetOrCreateValue(world);
        long day = world.CurrentDate.TotalHours / world.CurrentDate.Calendar.HoursPerDay;
        if (memo.Index is null || memo.Day != day)
        {
            memo.Day = day;
            memo.Index = new LaborValueIndex(world);
        }

        return memo.Index;
    }

    private void Build()
    {
        if (_valueByWorkplace is not null) return;
        var world = _world;
        var values = new Dictionary<WorkplaceId, double>();
        var byCityLists = new Dictionary<CityId, List<Workplace>>();
        var rules = world.EconomyRules;
        var catalog = world.EconomyCatalog;
        bool plantsFeedFood = world.ProcessRecipes.Any(item => item.Kind == ProcessKind.Plant);

        foreach (var workplace in world.Workplaces.OrderBy(w => w.Id.Value))
        {
            double value = MinimumValue;
            if (catalog.Recipes.TryGetValue(workplace.LocationType.Id, out var recipe))
            {
                double produced = 0;
                foreach (var (resourceId, perWorker) in recipe.Outputs)
                {
                    if (plantsFeedFood && resourceId == rules.FoodResourceId) continue;
                    produced += perWorker * PriceOf(rules, workplace, resourceId) * SellableShare(rules, workplace, resourceId);
                }

                double inputs = 0;
                foreach (var (resourceId, perWorker) in recipe.Inputs)
                    inputs += perWorker * rules.PriceFloor.GetValueOrDefault(resourceId, 1);

                value = Math.Max(MinimumValue, produced - inputs);
            }

            values[workplace.Id] = value;
            if (!byCityLists.TryGetValue(workplace.City, out var list))
                byCityLists[workplace.City] = list = [];
            list.Add(workplace);
        }

        var professions = new Dictionary<int, int>();
        foreach (var (professionId, typeId) in catalog.LocationTypeByProfession)
            if (!professions.TryGetValue(typeId, out var known) || professionId < known)
                professions[typeId] = professionId;

        _professionByLocationType = professions;
        _byCity = byCityLists.ToDictionary(e => e.Key, e => e.Value.ToArray());
        _valueByWorkplace = values;
    }

    /// <summary>Parte da saída que ainda encontra comprador: com o estoque cheio o que se produz a mais é perda, então o
    /// trabalho vale quase nada. Sem capacidade declarada, a saída sempre vende.</summary>
    private static double SellableShare(EconomyRules rules, Workplace workplace, int resourceId)
    {
        var resource = new ResourceType(resourceId);
        long capacity = rules.CapacityOf(resource, workplace.LocationType);
        if (capacity <= 0 || capacity == long.MaxValue) return 1.0;
        double fill = (double)workplace.Stock.GetValueOrDefault(resource) / capacity;
        return Math.Max(MinimumSellableShare, 1.0 - fill);
    }

    private const double MinimumSellableShare = 0.05;

    private static double PriceOf(EconomyRules rules, Workplace workplace, int resourceId) =>
        workplace.Prices.TryGetValue(new ResourceType(resourceId), out var price)
            ? price
            : rules.PriceFloor.GetValueOrDefault(resourceId, 1);
}
