using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Cities.Layout;

/// <summary>O perfil de quem constrói a casa (AD-173), lido do mundo no momento da obra: ofício (alguém da casa trabalha
/// numa oficina, não na lavoura), quantos moram e o dinheiro por pessoa comparado à mediana das casas da cidade.</summary>
internal static class LotProfiles
{
    public static LotProfile ForHousehold(WorldState world, CityId city, IReadOnlyList<Npc> members)
    {
        long coins = 0;
        bool crafts = false;
        foreach (var member in members)
        {
            coins += member.Wallet.Amount;
            if (IsCraftProfession(world, city, member.Profession.Id)) crafts = true;
        }
        int count = Math.Max(1, members.Count);
        return new LotProfile(true, crafts, members.Count, coins / (double)count, MedianMeansPerHead(world, city));
    }

    /// <summary>O ofício leva a uma oficina da cidade que não é lavoura (sem roça na célula do local de trabalho).</summary>
    public static bool IsCraftProfession(WorldState world, CityId city, int professionId)
    {
        if (!world.EconomyCatalog.LocationTypeByProfession.TryGetValue(professionId, out int locationType)) return false;
        foreach (var workplace in world.Workplaces)
            if (workplace.City == city && workplace.LocationType.Id == locationType)
                return world.FindCropAt(workplace.Location) is null;
        return false;
    }

    /// <summary>Mediana do dinheiro por pessoa das casas da cidade (0 sem casas ou sem dinheiro: aí ninguém é pobre nem rico).</summary>
    public static double MedianMeansPerHead(WorldState world, CityId city)
    {
        var perHead = new List<double>();
        foreach (var household in world.Households)
        {
            if (household.City != city || household.IsEmpty) continue;
            long coins = 0;
            int living = 0;
            foreach (var id in household.Members)
                if (world.FindNpc(id) is { IsAlive: true } npc)
                {
                    coins += npc.Wallet.Amount;
                    living++;
                }
            if (living > 0) perHead.Add(coins / (double)living);
        }
        if (perHead.Count == 0) return 0;
        perHead.Sort();
        return perHead[perHead.Count / 2];
    }
}
