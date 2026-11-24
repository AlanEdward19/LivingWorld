using System.Globalization;
using System.Text;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Observation;

/// <summary>Onde está a moeda por década (AD-144): carteiras dos vivos, fazenda, ferraria, outras oficinas e tesouro das
/// cidades; se a ferraria vive (tem gente e fecha a folha); Gini da carteira dos adultos e da riqueza por casa. Só lê.</summary>
public sealed class EconomyTally(WorldState world)
{
    public const int FarmType = 1;
    public const int ForgeType = 2;

    public sealed record Sample(
        int Year, int Alive, long Wallets, long Farm, long Forge, long Other, long Cities, long Owners,
        double GiniAdults, double GiniHouseholds, bool ForgeAlive, int ForgeWorkers, long ForgeIronSold);

    public List<Sample> Samples { get; } = [];

    /// <summary>Chamado no início do ano <paramref name="year"/> (e no fim da rodada); guarda só os anos múltiplos de 10.</summary>
    public void SampleYear(int year)
    {
        if (year % 10 != 0) return;
        long wallets = 0, owners = 0;
        var adults = new List<(double, double)>();
        foreach (var n in world.AliveNpcIndex.Alive)
        {
            wallets += n.Wallet.Amount;
            if (n.AgeYears(world.CurrentDate) >= 15) adults.Add((n.Wallet.Amount, 1));
        }

        var households = new List<(double, double)>();
        foreach (var h in world.Households)
        {
            long sum = 0; int members = 0;
            foreach (var id in h.Members)
                if (world.FindNpc(id) is { IsAlive: true } m) { sum += m.Wallet.Amount; members++; }
            if (members > 0) households.Add((sum, 1));
        }

        long farm = 0, forge = 0, other = 0; bool forgeAlive = false; int forgeWorkers = 0;
        foreach (var w in world.Workplaces)
        {
            if (w.Owner is { } o && world.FindNpc(o) is { IsAlive: true } owner) owners += owner.Wallet.Amount;
            switch (w.LocationType.Id)
            {
                case FarmType: farm += w.Treasury.Amount; break;
                case ForgeType:
                    forge += w.Treasury.Amount;
                    forgeWorkers += w.Employees.Count;
                    forgeAlive |= w.Employees.Count > 0 && !w.PayrollFailed;
                    break;
                default: other += w.Treasury.Amount; break;
            }
        }

        long sold = world.ResourceConsumed.GetValueOrDefault(new LivingWorld.Domain.Geography.ResourceType(4));
        Samples.Add(new Sample(year, world.AliveNpcIndex.Alive.Count, wallets, farm, forge, other,
            world.Cities.Sum(c => c.Treasury.Amount), owners, WealthInequality.Gini(adults), WealthInequality.Gini(households),
            forgeAlive, forgeWorkers, sold));
    }

    /// <summary>Média entre seeds, por década: parte da moeda em cada lugar, Gini, ferraria viva (contagem de seeds).</summary>
    public static string Summarize(IReadOnlyList<EconomyTally> runs)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("moeda por decada (media das seeds; % do total) ano: carteiras fazenda ferraria outras cidade | donos | gini_adultos gini_casas | ferraria_viva ferreiros ferro_consumido");
        var years = runs.SelectMany(r => r.Samples.Select(s => s.Year)).Distinct().OrderBy(y => y);
        foreach (int y in years)
        {
            var s = runs.Select(r => r.Samples.LastOrDefault(x => x.Year == y)).Where(x => x is not null).Select(x => x!).ToList();
            static double Share(long part, Sample x)
            {
                long total = x.Wallets + x.Farm + x.Forge + x.Other + x.Cities;
                return total <= 0 ? 0 : 100.0 * part / total;
            }

            sb.AppendLine(inv,
                $"  {y,3}: {s.Average(x => Share(x.Wallets, x)),5:F1} {s.Average(x => Share(x.Farm, x)),5:F1} {s.Average(x => Share(x.Forge, x)),5:F1} {s.Average(x => Share(x.Other, x)),5:F1} {s.Average(x => Share(x.Cities, x)),5:F1} | {s.Average(x => Share(x.Owners, x)),5:F1} | {s.Average(x => x.GiniAdults):F3} {s.Average(x => x.GiniHouseholds):F3} | {s.Count(x => x.ForgeAlive)}/{s.Count} {s.Average(x => x.ForgeWorkers):F1} {s.Average(x => x.ForgeIronSold):F0} (n={s.Count})");
        }

        return sb.ToString();
    }
}
