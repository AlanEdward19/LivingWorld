using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Economy.Production;

// Parte de CropSystem — o ano agrícola (AD-135). Roda uma vez por dia (Midshift), uma passada pelas fazendas.
public sealed partial class CropSystem
{
    /// <summary>O alimento da fazenda vem dos campos (ano agrícola ou plantio por processo), não da receita diária.</summary>
    public static bool FieldsFeedFood(WorldState world) =>
        world.AgricultureRules.Enabled || world.ProcessRecipes.Any(item => item.Kind == ProcessKind.Plant);

    private static void TickSeasonal(WorldState world, TickContext ctx, SkillsRules? skills)
    {
        var ag = world.AgricultureRules;
        var econ = world.EconomyRules;
        var food = new ResourceType(econ.FoodResourceId);
        long tick = ctx.CurrentTick;
        int month = world.CurrentDate.Month;
        bool monthStart = world.CurrentDate.Day == 0;
        var farms = Farms(world);
        if (farms.Count == 0) return;

        var vacancyIndex = VacancyIndex.BuildForTick(world);
        var temperature = EnvironmentTemperatureMechanic.FieldAt(world, tick);
        var residents = ResidentsByCity(world);
        var farmsPerCity = new Dictionary<CityId, int>();
        foreach (var (farm, _) in farms) farmsPerCity[farm.City] = farmsPerCity.GetValueOrDefault(farm.City) + 1;

        foreach (var (farm, field) in farms)
        {
            float t = temperature.At(farm.Location);
            var workers = vacancyIndex.PresentWorkersAt(farm);
            bool canWorkField = t >= ag.MinFieldWorkTemperature;

            // a lavoura que não amadureceu até a hora de semear de novo se perde (ano frio demais)
            if (field.Status == CropStatus.Growing && monthStart && month == ag.FailMonth && field.LastSeedSown > 0)
                field.LayFallow(); // a semente já saiu do estoque ao semear; a lavoura em pé nunca foi produzida

            if (field.Status == CropStatus.Fallow && ag.IsSowingMonth(month) && canWorkField && workers.Count > 0)
                field.StartSeason();

            if (field.Status == CropStatus.Growing)
            {
                if (ag.IsSowingMonth(month) && canWorkField && workers.Count > 0)
                    Sow(world, ag, farm, field, farms, workers, skills, residents, farmsPerCity);
                else if (!ag.IsSowingMonth(month) && field.LastSeedSown == 0)
                    field.LayFallow(); // sem semente nenhuma o campo fica em pousio até o próximo outono

                if (field.LastSeedSown > 0)
                    field.Develop(Math.Max(0, t - ag.BaseTemperature));

                if (field.LastSeedSown > 0 && field.Development >= ag.DevelopmentToMaturity && month >= ag.EarliestRipeningMonth && month < ag.FailMonth)
                {
                    double climate = world.ClimateFactorAt(farm.Location, tick) / 1000.0;
                    field.Ripen(tick, (long)Math.Floor(field.EffectiveSeedMilli / 1000.0 * ag.YieldPerSeed * climate));
                }
            }
            else if (field.Status == CropStatus.Mature)
                Harvest(world, ctx, ag, farm, field, workers, food, residents, farmsPerCity);

            StockShop(world, econ, ag, farm, field, farms, residents, food);
            if (monthStart)
                SpoilStores(ctx, ag, farm, field, food);
        }
    }

    /// <summary>Fazendas em ordem de id, cada uma com o seu campo (criado em pousio na primeira vez).</summary>
    private static List<(Workplace Farm, CropBatch Field)> Farms(WorldState world)
    {
        var list = new List<(Workplace, CropBatch)>();
        foreach (var workplace in world.Workplaces.OrderBy(item => item.Id.Value))
        {
            if (workplace.LocationType.Id != DefaultFarmLocationTypeId) continue;
            list.Add((workplace, FieldOf(world, workplace)));
        }

        return list;
    }

    private static CropBatch FieldOf(WorldState world, Workplace farm)
    {
        if (world.FindCropAt(farm.Location) is { } existing) return existing;
        long now = world.CurrentDate.TotalHours;
        var created = CropBatch.Create(
            world.NextCropBatchIdAndAdvance(), world.EconomyRules.FoodResourceId, farm.Location, now, now + 1, waterRequired: 0).Value!;
        created.LayFallow();
        world.AddCropBatch(created);
        return created;
    }

    /// <summary>Semente que o campo quer: o bastante para as bocas do mundo repartidas entre as fazendas (povoado sem fazenda
    /// compra da vila), até a terra que os empregados da fazenda dão conta de lavrar.</summary>
    internal static long SeedTarget(AgricultureRules ag, Workplace farm, Dictionary<CityId, long> residents, Dictionary<CityId, int> farmsPerCity) =>
        Math.Min(
            residents.Values.Sum() / Math.Max(1, farmsPerCity.Values.Sum()) * ag.SeedPerResident,
            Math.Max(1, farm.Employees.Count) * ag.MaxSeedPerWorker);

    private static void Sow(
        WorldState world, AgricultureRules ag, Workplace farm, CropBatch field, List<(Workplace Farm, CropBatch Field)> farms,
        IReadOnlyList<Npc> workers, SkillsRules? skills, Dictionary<CityId, long> residents, Dictionary<CityId, int> farmsPerCity)
    {
        // sem relha de ferro o arado de pau lavra menos terra por dia (AD-144)
        long sowCapacity = (long)Math.Floor(workers.Count * ag.SowPerWorkerDay * ToolMechanic.OutputFactor(world, farm, workers.Count));
        long wanted = Math.Min(SeedTarget(ag, farm, residents, farmsPerCity) - field.LastSeedSown, sowCapacity);
        if (wanted <= 0) return;

        // semente guardada primeiro, depois o grão do próprio celeiro, depois o que sobra nas outras fazendas da cidade
        long seed = field.TakeSeed(wanted);
        seed += field.TakeBarn(wanted - seed);
        foreach (var (other, otherField) in farms)
        {
            if (seed >= wanted) break;
            if (other.Id == farm.Id || other.City != farm.City) continue;
            long spare = Math.Max(0, otherField.SeedReserve - Math.Max(0, SeedTarget(ag, other, residents, farmsPerCity) - otherField.LastSeedSown));
            seed += otherField.TakeSeed(Math.Min(spare, wanted - seed));
            seed += otherField.TakeBarn(wanted - seed);
        }

        if (seed <= 0) return;
        double skill = 1.0 + ag.SkillYieldWeight * AverageSkill(workers, skills);
        field.Sow(seed, (long)Math.Round(seed * 1000.0 * skill));
        world.RecordResourceConsumed(new ResourceType(world.EconomyRules.FoodResourceId), seed); // a semente sai do estoque para a terra
    }

    /// <summary>Habilidade média (0–1) dos lavradores presentes no ofício da profissão deles; sem regras de habilidade, 0.</summary>
    private static double AverageSkill(IReadOnlyList<Npc> workers, SkillsRules? skills)
    {
        if (skills is null) return 0;
        double sum = 0;
        int mapped = 0;
        foreach (var npc in workers)
        {
            if (!skills.SkillByProfession.TryGetValue(npc.Profession.Id, out var skillType)) continue;
            sum += npc.Skills.Get(skillType);
            mapped++;
        }

        return mapped == 0 ? 0 : sum / mapped / skills.Cap;
    }

    private static void Harvest(
        WorldState world, TickContext ctx, AgricultureRules ag, Workplace farm, CropBatch field, IReadOnlyList<Npc> workers,
        ResourceType food, Dictionary<CityId, long> residents, Dictionary<CityId, int> farmsPerCity)
    {
        long tick = ctx.CurrentTick;
        long shed = RoundStochastic(field.Standing * ag.ShatterLossPerDay, ctx, "shatter", farm.Id.Value, tick);
        field.LoseStanding(shed); // grão em pé ainda não é produção (só o ceifado conta): não entra como perda de estoque

        double labor = 0;
        foreach (var npc in workers)
            labor += AttributeMechanic.StrengthMultiplier(world, npc) * BodyMechanic.WorkCapacityMultiplier(world, npc);
        labor *= ToolMechanic.OutputFactor(world, farm, workers.Count); // sem foice e sega a ceifa anda devagar (AD-144)
        long reaped = field.Reap((long)Math.Floor(labor * ag.ReapPerWorkerDay));
        if (reaped > 0)
        {
            world.RecordResourceProduced(food, reaped);
            long toSeed = Math.Min(reaped, Math.Max(0, SeedTarget(ag, farm, residents, farmsPerCity) - field.SeedReserve));
            field.StoreSeed(toSeed);
            long toHomes = ShareWithReapers(world, ag, workers, food, (long)Math.Floor((reaped - toSeed) * ag.ReaperShare));
            field.StoreBarn(reaped - toSeed - toHomes);
        }

        long hoursPerDay = world.CurrentDate.Calendar.HoursPerDay;
        if (field.Standing <= 0 || tick - field.MatureAtTick >= ag.MaxHarvestDays * hoursPerDay)
            field.LayFallow();
    }

    /// <summary>Parte em espécie de quem ceifou (AD-135): a casa de cada ceifeiro presente guarda grão para os meses seguintes,
    /// até <see cref="AgricultureRules.HomeStorePerMember"/> por morador, dentro da parte do dia. É a tulha da família
    /// camponesa; o resto vai ao celeiro da fazenda e à venda. Devolve o que foi para as casas.</summary>
    private static long ShareWithReapers(WorldState world, AgricultureRules ag, IReadOnlyList<Npc> workers, ResourceType food, long share)
    {
        if (share <= 0 || ag.HomeStorePerMember <= 0) return 0;
        long given = 0;
        var seen = new HashSet<HouseholdId>();
        foreach (var npc in workers.OrderBy(n => n.Id.Value))
        {
            if (given >= share) break;
            if (npc.Household is not { } id || !seen.Add(id) || world.FindHousehold(id) is not { } home) continue;
            long room = home.Members.Count * ag.HomeStorePerMember - home.Stock.GetValueOrDefault(food);
            long give = Math.Min(room, share - given);
            if (give <= 0) continue;
            home.Deposit(food, give);
            given += give;
        }

        return given;
    }

    /// <summary>A venda da fazenda é abastecida do celeiro (o próprio e, faltando, o das outras fazendas da cidade) até alguns
    /// dias de consumo de quem mora na cidade, dentro da capacidade do estoque.</summary>
    private static void StockShop(
        WorldState world, EconomyRules econ, AgricultureRules ag, Workplace farm, CropBatch field,
        List<(Workplace Farm, CropBatch Field)> farms, Dictionary<CityId, long> residents, ResourceType food)
    {
        long capacity = econ.CapacityOf(food, farm.LocationType);
        long target = Math.Min(capacity, ag.ShopStockDaysPerPerson * Math.Max(1, residents.GetValueOrDefault(farm.City)));
        long missing = target - farm.Stock.GetValueOrDefault(food);
        // racionamento: o celeiro solta por dia no máximo o que dura até a próxima colheita; em ano normal não aperta, na
        // carestia todos comem menos em vez de comer tudo e morrer juntos semanas depois
        long cityBarn = 0;
        foreach (var (other, otherField) in farms)
            if (other.City == farm.City) cityBarn += otherField.Barn;
        missing = Math.Min(missing, Math.Max(1, cityBarn / DaysToNextHarvest(world, ag, field)));
        if (missing <= 0) return;

        long taken = field.TakeBarn(missing);
        foreach (var (other, otherField) in farms)
        {
            if (taken >= missing) break;
            if (other.Id == farm.Id || other.City != farm.City) continue;
            taken += otherField.TakeBarn(missing - taken);
        }

        // celeiro vazio: a fome come a semente do ano que vem (o que só acontece entre a colheita e a semeadura)
        if (taken < missing)
            taken += field.TakeSeed(missing - taken);

        if (taken > 0)
            farm.Deposit(food, taken, econ);
    }

    /// <summary>Dias até a próxima colheita esperada: o meio do mês esperado da colheita; com a lavoura madura, ou ainda em pé
    /// depois do mês esperado (atrasada), meio mês. A lavoura que só pode amadurecer (mês do dia longo) mas ainda não chegou ao
    /// mês esperado conta até o meio dele (AD-140): antes contava meio mês e soltava o celeiro três vezes mais rápido em julho,
    /// esvaziando-o antes da ceifa de agosto.</summary>
    internal static long DaysToNextHarvest(WorldState world, AgricultureRules ag, CropBatch field)
    {
        var cal = world.CurrentDate.Calendar;
        int month = world.CurrentDate.Month;
        bool standing = field.Status == CropStatus.Growing && field.LastSeedSown > 0 && month < ag.FailMonth;
        if (field.Status == CropStatus.Mature || (standing && month >= Math.Max(ag.EarliestRipeningMonth, ag.ExpectedHarvestMonth)))
            return cal.DaysPerMonth / 2;
        int months = (ag.ExpectedHarvestMonth - month + cal.MonthsPerYear) % cal.MonthsPerYear;
        if (months == 0) months = cal.MonthsPerYear;
        return Math.Max(1, months * (long)cal.DaysPerMonth - world.CurrentDate.Day + cal.DaysPerMonth / 2);
    }

    /// <summary>Ratos, mofo e caruncho: perda mensal do celeiro e da semente equivalente à perda anual da regra.</summary>
    private static void SpoilStores(TickContext ctx, AgricultureRules ag, Workplace farm, CropBatch field, ResourceType food)
    {
        if (ag.BarnLossPerYear <= 0) return;
        double monthly = MonthlyRate(ag.BarnLossPerYear);
        long tick = ctx.CurrentTick;
        LogLost(ctx, farm, food, field.TakeBarn(RoundStochastic(field.Barn * monthly, ctx, "barn-loss", farm.Id.Value, tick)));
        LogLost(ctx, farm, food, field.TakeSeed(RoundStochastic(field.SeedReserve * monthly, ctx, "seed-loss", farm.Id.Value, tick)));
    }

    /// <summary>Taxa mensal que, composta por 12 meses, dá a perda anual.</summary>
    internal static double MonthlyRate(double annual) =>
        annual >= 1 ? 1 : 1 - DeterministicMath.Exp(DeterministicMath.Log(1 - annual) / 12.0);

    /// <summary>Arredonda para baixo e soma 1 com probabilidade igual à fração: sem viés, e estoque pequeno também perde.</summary>
    internal static long RoundStochastic(double value, TickContext ctx, string purpose, long id, long tick)
    {
        if (value <= 0) return 0;
        long whole = (long)Math.Floor(value);
        double fraction = value - whole;
        if (fraction <= 0) return whole;
        return whole + (ctx.StreamFor(purpose, unchecked(id * 1_000_003L + tick)).NextDouble() < fraction ? 1 : 0);
    }

    private static Dictionary<CityId, long> ResidentsByCity(WorldState world)
    {
        var byCity = new Dictionary<CityId, long>();
        foreach (var household in world.Households)
            byCity[household.City] = byCity.GetValueOrDefault(household.City) + household.Members.Count;
        return byCity;
    }

    private static void LogLost(TickContext ctx, Workplace farm, ResourceType food, long lost)
    {
        if (lost > 0)
            ctx.LogEvent(WorldEventKind.ResourceLost, $"{farm.Id.Value}|{food.Id}|{lost}", sourceSystem: SystemName);
    }

    /// <summary>Estado inicial declarado do mundo novo (AD-135), como a despensa inicial: o trigo foi semeado no outono anterior
    /// pelos lavradores que a vila tem, e o celeiro guarda o bastante para chegar à colheita. Fazenda construída depois nasce em
    /// pousio e semeia com semente das outras fazendas da cidade.</summary>
    public static void SeedFirstAgriculturalYear(WorldState world)
    {
        var ag = world.AgricultureRules;
        if (!ag.Enabled || !world.EconomyRules.Enabled) return;
        var farmsByCity = world.Workplaces.Where(w => w.LocationType.Id == DefaultFarmLocationTypeId)
            .OrderBy(w => w.Id.Value).GroupBy(w => w.City).ToList();
        foreach (var group in farmsByCity)
        {
            long residents = world.Npcs.Count(npc => npc.IsAlive && npc.City == group.Key);
            int count = group.Count();
            foreach (var farm in group)
            {
                var field = FieldOf(world, farm);
                if (field.Status != CropStatus.Fallow || field.Barn > 0) continue;
                long seed = residents / count * ag.SeedPerResident;
                field.StartSeason();
                field.Sow(seed, seed * 1000);
                field.Develop(ag.InitialDevelopment);
                field.StoreBarn(residents / count * ag.InitialProvisionPerPerson);
            }
        }
    }
}
