using System.Globalization;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Observation;

/// <summary>Onde está a moeda do mundo (AD-140): carteiras dos vivos, caixa das oficinas por tipo, tesouro das cidades. Só lê.</summary>
public static class MoneyTrace
{
    public static string Line(WorldState world)
    {
        long wallets = 0, zeroWallet = 0, adults = 0, deadWallets = 0;
        foreach (var n in world.AliveNpcIndex.Alive)
        {
            wallets += n.Wallet.Amount;
            if (n.AgeYears(world.CurrentDate) >= 15) { adults++; if (n.Wallet.Amount == 0) zeroWallet++; }
        }
        foreach (var n in world.Npcs) if (!n.IsAlive) deadWallets += n.Wallet.Amount;
        var byType = new SortedDictionary<int, (long Cash, int Count, int Emp)>();
        foreach (var w in world.Workplaces)
        {
            var c = byType.GetValueOrDefault(w.LocationType.Id);
            byType[w.LocationType.Id] = (c.Cash + w.Treasury.Amount, c.Count + 1, c.Emp + w.Employees.Count);
        }
        long cities = world.Cities.Sum(c => c.Treasury.Amount);
        var inv = CultureInfo.InvariantCulture;
        return string.Create(inv, $"alive={world.AliveNpcIndex.Alive.Count} wallets={wallets} adultsZero={zeroWallet}/{adults} dead={deadWallets} cities={cities} ") +
            string.Join(" ", byType.Select(kv => string.Create(inv, $"wp{kv.Key}={kv.Value.Cash}/{kv.Value.Count}/{kv.Value.Emp}e")));
    }
}
