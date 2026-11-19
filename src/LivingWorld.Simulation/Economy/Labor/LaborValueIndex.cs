using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy.Labor;

/// <summary>Valor do trabalho de um trabalhador em cada oficina, uma vez por dia: o que a receita rende por
/// trabalhador (saída × preço local, menos o insumo ao preço-piso), mínimo 1; na oficina por encomenda, só a encomenda que a
/// lotação atual não cobre, sem piso (AD-144). É o que atrai gente para uma oficina:
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

    /// <param name="self">Quem avalia o próprio emprego: a mão dele não conta contra ele (o que ele faz é o valor dele).</param>
    public double ValueOf(Workplace workplace, Npc? self = null)
    {
        Build();
        double value = _valueByWorkplace!.GetValueOrDefault(workplace.Id, MinimumValue);
        // ferramenta por encomenda (AD-144): a parte dela é calculada ao vivo, com a lotação do momento, para que a mesma vaga
        // não pareça boa a todos no mesmo dia
        // sem encomenda a cobrir, mais uma mão ali não vale nada (sem o piso): senão o ofício do artesão o prendia à forja ociosa
        if (_toolOutputs is not null && _toolOutputs.TryGetValue(workplace.Id, out var outputs))
            foreach (var (resourceId, perWorker, price) in outputs)
                value += perWorker * price * MarginalOrderedShare(_world, workplace, resourceId, perWorker, self);
        // AD-191: a lavoura vale o grão que a mão acrescenta (quem já está lá: a parte dele; quem chega: a mão a mais)
        if (_fieldValue is not null && _fieldValue.TryGetValue(workplace.Id, out var field))
            value += self is not null && workplace.Employees.Contains(self.Id) ? field.Stay : field.Join;
        return value;
    }

    private Dictionary<WorkplaceId, List<(int ResourceId, long PerWorker, double Price)>>? _toolOutputs;
    private Dictionary<WorkplaceId, (double Stay, double Join)>? _fieldValue;

    /// <summary>AD-191 (<see cref="AgricultureRules.FieldLaborValue"/>): o valor de um dia de mão na lavoura, ao preço da comida da
    /// venda da fazenda. Na ceifa, o grão em pé que cada mão corta num dia (até <see cref="AgricultureRules.ReapPerWorkerDay"/>);
    /// fora dela, o que a mão acrescenta à colheita do ano (o produto marginal de <see cref="AgricultureRules.ExpectedHarvest"/>, que
    /// cai com a terra cheia) dividido pelos dias do ano. Antes a comida da lavoura não contava (só a água do poço, quase sempre
    /// encalhada) e a fazenda valia o mínimo: qualquer encomenda da forja ou fazenda que pagasse um pouco mais tirava o lavrador da
    /// terra, e as fazendas da borda da metrópole chegavam à ceifa com um quarto das mãos.</summary>
    internal static (double Stay, double Join)? FieldLaborValue(WorldState world, Workplace farm, double foodPrice)
    {
        var ag = world.AgricultureRules;
        if (!ag.Enabled || !ag.FieldLaborValue || farm.LocationType.Id != LivingWorld.Simulation.Economy.Production.CropSystem.DefaultFarmLocationTypeId) return null;
        if (world.FindCropAt(farm.Location) is not { } field) return null;
        int hands = LivingWorld.Simulation.Economy.Production.ToolMechanic.WorkingHands(world, farm);
        if (field.Status == CropStatus.Mature)
        {
            double Reap(int h) => Math.Min(ag.ReapPerWorkerDay, field.Standing / (double)Math.Max(1, h)) * foodPrice;
            return (Reap(hands), Reap(hands + 1));
        }
        long arable = field.ArableSeed > 0 ? field.ArableSeed : long.MaxValue / 4;
        double days = world.Calendar.DaysPerMonth * world.Calendar.MonthsPerYear;
        double Marginal(int h) => (ag.ExpectedHarvest(arable, h + 1) - ag.ExpectedHarvest(arable, h)) / days * foodPrice;
        return (Marginal(Math.Max(0, hands - 1)), Marginal(hands));
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
        bool plantsFeedFood = LivingWorld.Simulation.Economy.Production.CropSystem.FieldsFeedFood(world);

        foreach (var workplace in world.Workplaces.OrderBy(w => w.Id.Value))
        {
            double value = MinimumValue;
            if (catalog.Recipes.TryGetValue(workplace.LocationType.Id, out var recipe))
            {
                double produced = 0;
                foreach (var (resourceId, perWorker) in recipe.Outputs)
                {
                    if (plantsFeedFood && resourceId == rules.FoodResourceId) continue;
                    if (catalog.IsTool(resourceId))
                    {
                        _toolOutputs ??= [];
                        if (!_toolOutputs.TryGetValue(workplace.Id, out var tools)) _toolOutputs[workplace.Id] = tools = [];
                        tools.Add((resourceId, perWorker, PriceOf(rules, workplace, resourceId)));
                        continue;
                    }

                    produced += perWorker * PriceOf(rules, workplace, resourceId) * SellableShare(rules, workplace, resourceId);
                }

                double inputs = 0;
                foreach (var (resourceId, perWorker) in recipe.Inputs)
                    inputs += perWorker * rules.PriceFloor.GetValueOrDefault(resourceId, 1);

                value = _toolOutputs?.ContainsKey(workplace.Id) == true ? Math.Max(0, produced - inputs) : Math.Max(MinimumValue, produced - inputs);
            }

            if (plantsFeedFood && FieldLaborValue(world, workplace, PriceOf(rules, workplace, rules.FoodResourceId)) is { } field)
                (_fieldValue ??= [])[workplace.Id] = field;
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

    /// <summary>Ferramenta se faz por encomenda (AD-144): mais um artesão vale só a parte da encomenda diária da cidade que quem
    /// já está lá não dá conta de fazer (produto marginal). Um ferreiro faz num dia o desgaste de semanas da vila: o segundo
    /// quase nada acrescenta. Sem isso o preço de encomenda fazia a forja valer 20× a lavoura e a vila ia toda para a forja.</summary>
    private static double MarginalOrderedShare(WorldState world, Workplace workplace, int resourceId, long perWorker, Npc? self)
    {
        if (perWorker <= 0) return 0;
        double dailyOrders = (double)LivingWorld.Simulation.Economy.Production.ToolMechanic.OrderBook(world, workplace.City, resourceId)
            / world.CurrentDate.Calendar.DaysPerMonth;
        int hands = LivingWorld.Simulation.Economy.Production.ToolMechanic.WorkingHands(world, workplace);
        // histerese (AD-144): quem já está lá vale o que ele mesmo faz; sem isso o ferreiro único se via sem valor, saía em
        // 30 dias e outro entrava no lugar, e a forja vivia de aprendizes de passagem
        if (self is not null && workplace.Employees.Contains(self.Id) && hands > 0) hands--;
        return Math.Clamp((dailyOrders - hands * perWorker) / perWorker, 0, 1);
    }

    private static double PriceOf(EconomyRules rules, Workplace workplace, int resourceId) =>
        workplace.Prices.TryGetValue(new ResourceType(resourceId), out var price)
            ? price
            : rules.PriceFloor.GetValueOrDefault(resourceId, 1);
}
