using System.Runtime.CompilerServices;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Cities;

/// <summary>Hóspede de diária (lodging-and-rent): quem está longe demais de casa para voltar à noite aluga um quarto na
/// hospedagem mais próxima que tenha vaga e que ele possa pagar. A diária é paga ao locador (a oficina do prédio) quando a noite
/// de sono termina, e o preço acompanha local e demanda. Sem estado novo: a ocupação por hóspedes sai de quem está dentro do
/// prédio agora. Cenário sem <c>NightlyRate</c> não paga nada nem custa nada (caminho rápido).</summary>
public static class GuestLodging
{
    private sealed class PriceCache
    {
        public long Hour = long.MinValue;
        public readonly Dictionary<CityId, ValuationContext> Contexts = new();
    }

    private static ConditionalWeakTable<WorldState, PriceCache>? Caches;

    /// <summary>Flag derivada no bootstrap: nenhuma varredura nem cache é alocado na consulta sem diária.</summary>
    public static bool Exists(WorldState world) => world.CityCatalog.HasNightlyLodging;

    public readonly record struct Room(Building Building, Workplace Landlord, long Nightly, CellCoord Cell);

    /// <summary>Quarto que o NPC usaria esta noite, ou <c>null</c> (perto de casa, sem hospedagem, sem vaga ou sem dinheiro).</summary>
    public static Room? FindRoom(WorldState world, Npc npc, CellCoord home)
    {
        long homeDistance = Distance(npc.CurrentLocation, home);
        var recipes = world.CityCatalog.BuildingRecipes;

        Room? best = null;
        long bestDistance = long.MaxValue;
        foreach (var building in world.Buildings)
        {
            if (building.Position is not { } position || !recipes.TryGetValue(building.BuildingTypeId, out var recipe)
                || recipe.Lodging is not { NightlyRate: > 0 } terms) continue;

            long distance = Distance(npc.CurrentLocation, position);
            if (homeDistance <= terms.GuestMinDistance || distance >= homeDistance) continue;
            if (distance > bestDistance || (distance == bestDistance && best is { } current && current.Building.Id.Value < building.Id.Value)) continue;

            var landlord = world.Workplaces.FirstOrDefault(w => w.Building == building.Id);
            if (landlord is null || HasNoFreeUnit(world, building, npc)) continue;

            long nightly = NightlyPrice(world, building, recipe);
            if (npc.Wallet.Amount < nightly) continue;

            best = new Room(building, landlord, nightly, DwellingCapacity.InteriorCellOf(building, position));
            bestDistance = distance;
        }
        return best;
    }

    /// <summary>Cobra a diária do NPC e credita o locador; <c>false</c> se não paga (então dorme sem quarto).</summary>
    public static bool TryCharge(Npc npc, Room room)
    {
        if (!npc.TryDebitWallet(new Money(room.Nightly)).IsSuccess) return false;
        room.Landlord.CreditTreasury(new Money(room.Nightly));
        return true;
    }

    private static bool HasNoFreeUnit(WorldState world, Building building, Npc asking)
    {
        int occupied = world.Households.Count(h => h.Dwelling == building.Id);
        foreach (var other in world.Npcs)
            if (other.Id != asking.Id && other.IsAlive && other.Interior is { } interior && interior.Building == building.Id
                && (other.Household is not { } id || world.FindHousehold(id)?.Dwelling != building.Id))
                occupied++;
        return occupied >= DwellingCapacity.Of(world, building);
    }

    private static long NightlyPrice(WorldState world, Building building, BuildingRecipe recipe)
    {
        var cache = LazyInitializer.EnsureInitialized(ref Caches).GetOrCreateValue(world);
        long hour = world.CurrentDate.TotalHours;
        if (cache.Hour != hour)
        {
            cache.Contexts.Clear();
            cache.Hour = hour;
        }

        if (!cache.Contexts.TryGetValue(building.City, out var context))
        {
            var city = world.FindCity(building.City);
            if (city is null) return recipe.Lodging!.NightlyRate;
            cache.Contexts[building.City] = context = new ValuationContext(world, city);
        }
        return BuildingValuation.NightlyOf(context, building, recipe);
    }

    private static long Distance(CellCoord a, CellCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}
