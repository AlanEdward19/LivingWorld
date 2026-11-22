using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Emergence;

/// <summary>Consumidor causal da tecnologia de mobilidade: o eixo de velocidade (índice 0) das tecnologias que a cidade adotou
/// encurta todo deslocamento da cidade (<see cref="TravelTicks"/>), e com ele a despensa que a casa mantém e o tamanho da ida às compras.</summary>
public static class Mobility
{
    public static int BonusPct(WorldState world, CityId city)
    {
        if (world.Technologies.Count == 0) return 0;
        // AD-167: a conta varria todas as tecnologias em toda viagem, ida ao mercado e pergunta de alcance da despensa; a resposta só
        // muda com uma tecnologia nova ou adotada (o carimbo do mundo), então fica guardada por cidade até ele mudar
        long version = world.TechnologiesVersion;
        var memo = world.PaceBonusMemo;
        if (memo is not null && memo.Version == version && memo.ByCity.TryGetValue(city, out int known)) return known;
        int bonus = ComputeBonusPct(world, city);
        var byCity = memo is not null && memo.Version == version ? new Dictionary<CityId, int>(memo.ByCity) : [];
        byCity[city] = bonus;
        world.PaceBonusMemo = new WorldState.PaceBonusByCity(version, byCity);
        return bonus;
    }

    private static int ComputeBonusPct(WorldState world, CityId city)
    {
        int best = 0;
        foreach (var tech in world.Technologies)
        {
            // AD-176: só a tecnologia de mobilidade encurta o passo; a de calçar muda a rua, não a pessoa
            if (tech.Category != LivingWorld.Domain.Emergence.EmergenceRules.MobilityCategory) continue;
            if (tech.Vector.Axes[0] <= best || !tech.AdoptedBy.Contains(city)) continue;
            best = tech.Vector.Axes[0];
        }
        return best * world.EmergenceRules.PaceBonusPctPerPoint;
    }

    /// <summary>Ponto único da duração de um deslocamento de NPC (trabalho, cama, mercado, visita, migração): custo do mapa
    /// escalado pelo corpo/poder, encurtado pela tecnologia que a cidade do NPC adotou. Local distinto custa ao menos 1 tick.</summary>
    public static long TravelTicks(WorldState world, Npc npc, CellCoord origin, CellCoord destination, double bodyMultiplier) =>
        TravelTicks(world, npc, origin, destination, bodyMultiplier, weather: true);

    /// <summary><paramref name="weather"/> = false: a distância de caminhada sem o chão do dia (lama, neve), para quem só pergunta
    /// "é perto?" (AD-180).</summary>
    public static long TravelTicks(WorldState world, Npc npc, CellCoord origin, CellCoord destination, double bodyMultiplier, bool weather)
    {
        bodyMultiplier *= LoadFactor(world, npc); // AD-179 (item 175): andar carregado é mais lento
        LivingWorld.Simulation.Cities.Streets.StreetRoute? route = null;
        long ticks = world.StreetsOn ? Routed(world, npc.Household, origin, destination, bodyMultiplier, out route)
            : WaterAware(world, npc.Household, origin, destination, bodyMultiplier)
            ?? TravelResolution.TicksBetween(world.Map, origin, destination, bodyMultiplier);
        return ticks == 0 ? 0 : Math.Max(1, Apply(weather ? Weathered(world, origin, destination, ticks, route) : ticks, BonusPct(world, npc.City)));
    }

    /// <summary>AD-187: <c>TravelTicks(..., weather: false) &gt; threshold</c> (a pergunta "é longe?"), sem buscar a rota quando o piso
    /// do custo já passa do limite: o tempo só cresce com o custo, e a água a pé só soma. Com embarcação da casa (remar pode poupar
    /// tempo) ou o piso abaixo do limite, a conta inteira de sempre.</summary>
    public static bool FartherThan(WorldState world, Npc npc, CellCoord origin, CellCoord destination, double bodyMultiplier, long threshold)
    {
        if (world.StreetsOn && origin != destination
            && LivingWorld.Simulation.Cities.Streets.StreetRoutes.CostFloor(world, origin, destination) is { } floor
            && !(world.InventionOn && npc.Household is { } h && world.Invention!.BestCraftSpeed(h.Value, world.InventionRules!) > 0))
        {
            double body = bodyMultiplier * LoadFactor(world, npc);
            long least = Math.Max(1, (long)Math.Ceiling(Math.Max(0, floor) * Math.Max(0.1, body)));
            if (Math.Max(1, Apply(least, BonusPct(world, npc.City))) > threshold) return true;
        }
        return TravelTicks(world, npc, origin, destination, bodyMultiplier, weather: false) > threshold;
    }

    /// <summary>AD-180: lama na parte sem calçamento da rota e neve na rota toda alongam a viagem (o tempo do lugar de saída).</summary>
    /// AD-187: <paramref name="route"/> é a rota já buscada para o tempo da viagem (a mesma do cache): não se procura de novo.
    private static long Weathered(WorldState world, CellCoord origin, CellCoord destination, long ticks,
        LivingWorld.Simulation.Cities.Streets.StreetRoute? route = null)
    {
        if (ticks == 0 || !LivingWorld.Simulation.Climate.WeatherNow.SlowsTravel(world, origin, out var row)) return ticks;
        int soft = route is null ? LivingWorld.Simulation.Cities.Streets.StreetRoutes.SoftPm(world, origin, destination)
            : LivingWorld.Simulation.Cities.Streets.StreetRoutes.SoftPmOf(world, route, origin, destination);
        return Math.Max(1, (long)Math.Ceiling(ticks * LivingWorld.Simulation.Climate.WeatherNow.TravelFactor(world, row, soft) - 1e-9));
    }

    /// <summary>AD-179 (item 175): quanto a carga a pé alonga o passo, com a rua de verdade ligada: 1 + quilos levados ×
    /// <see cref="LivingWorld.Domain.Cities.StreetRules.CarryPmPerKg"/> milésimos, até <see cref="LivingWorld.Domain.Cities.StreetRules.CarryMaxKg"/>
    /// quilos. A carga é a que a pessoa leva (<c>InventionLedger.Carried</c>: comida e água da viagem, espólio) mais o recurso nas
    /// mãos (<see cref="Npc.CarriedQuantity"/>). Sem carga, 1 (o caminho comum, sem custo).</summary>
    public static double LoadFactor(WorldState world, Npc npc)
    {
        if (!world.StreetsOn || world.InventionRules is not { } rules) return 1.0;
        long grams = world.Invention?.CarriedGrams(npc.Id.Value, rules) ?? 0;
        if (npc.CarriedQuantity > 0) grams += npc.CarriedQuantity * rules.UnitGramsOf(npc.CarriedResourceId);
        if (grams <= 0) return 1.0;
        return LoadFactorOfKg(world.StreetRules, grams / 1000.0);
    }

    /// <summary>O fator de passo de uma carga em quilos (o material de obra usa o mesmo).</summary>
    public static double LoadFactorOfKg(LivingWorld.Domain.Cities.StreetRules rules, double kg) =>
        1.0 + Math.Min(Math.Max(0, kg), rules.CarryMaxKg) * rules.CarryPmPerKg / 1000.0;

    /// <summary>Trajeto de compra do domicílio (casa ↔ mercado): o mesmo custo de mapa da memória de trajetos, com a água do
    /// caminho e a embarcação da casa quando a invenção está ligada (AD-141), encurtado pela tecnologia da cidade.</summary>
    public static long HouseholdTicks(WorldState world, LivingWorld.Domain.Population.Family.Household household, CellCoord origin, CellCoord destination) =>
        HouseholdTicks(world, household, origin, destination, world.StreetsOn);

    private static long HouseholdTicks(WorldState world, LivingWorld.Domain.Population.Family.Household household, CellCoord origin,
        CellCoord destination, bool streets)
    {
        LivingWorld.Simulation.Cities.Streets.StreetRoute? route = null;
        long ticks = streets ? Routed(world, household.Id, origin, destination, 1.0, out route)
            : WaterAware(world, household.Id, origin, destination, 1.0)
            ?? LivingWorld.Simulation.Economy.Market.TravelTicksCache.Between(world.Map, origin, destination);
        return Apply(Weathered(world, origin, destination, ticks, route), BonusPct(world, household.City));
    }

    /// <summary>AD-176: ticks da rota real (rede de caminhos, superfície, prédios no caminho), com o vau a pé ou a embarcação da
    /// casa na água da rota quando a invenção está ligada.</summary>
    private static long Routed(WorldState world, HouseholdId? household, CellCoord origin, CellCoord destination, double bodyMultiplier,
        out LivingWorld.Simulation.Cities.Streets.StreetRoute? route)
    {
        route = null;
        if (origin == destination) return 0;
        route = LivingWorld.Simulation.Cities.Streets.StreetRoutes.Get(world, origin, destination);
        double cost = route.Cost;
        int water = route.Water;
        if (water > 0 && world.InventionOn)
        {
            var rules = world.InventionRules!;
            int craft = household is { } h ? world.Invention!.BestCraftSpeed(h.Value, rules) : 0;
            cost += LivingWorld.Simulation.Invention.WaterCrossing.ExtraCost(world.Map, rules, water, craft);
        }
        return Math.Max(1, (long)Math.Ceiling(Math.Max(0, cost) * Math.Max(0.1, bodyMultiplier)));
    }

    /// <summary>Ticks do trajeto com água no caminho (AD-141), ou nulo quando a invenção está desligada ou não há água: aí vale o
    /// custo de antes, byte a byte. A pé, a água custa o vau; com embarcação da casa, remar.</summary>
    private static long? WaterAware(WorldState world, HouseholdId? household, CellCoord origin, CellCoord destination, double bodyMultiplier)
    {
        if (!world.InventionOn || origin == destination) return null;
        int water = LivingWorld.Simulation.Invention.WaterCrossing.CellsBetween(world.Map, origin, destination);
        if (water == 0) return null;
        var rules = world.InventionRules!;
        int craft = household is { } h ? world.Invention!.BestCraftSpeed(h.Value, rules) : 0;
        double cost = LivingWorld.Domain.Geography.Spatial.MovementCost.Between(world.Map, origin, destination)
            + LivingWorld.Simulation.Invention.WaterCrossing.ExtraCost(world.Map, rules, water, craft);
        return Math.Max(1, (long)Math.Ceiling(Math.Max(0, cost) * Math.Max(0.1, bodyMultiplier)));
    }

    public static long Apply(long ticks, int bonusPct) => bonusPct == 0 ? ticks : ticks * 100 / (100 + bonusPct);
}
