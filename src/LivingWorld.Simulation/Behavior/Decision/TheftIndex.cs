using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Behavior.Decision;

/// <summary>Índice do furto, montado uma vez por tick só com o furto ligado: NPCs vivos por célula (em ordem de id) e guardas
/// por cidade. Troca a varredura de toda a população por NPC desesperado por uma busca na célula. Vale o início do tick: quem
/// entra na célula durante o tick só aparece no seguinte, e quem sai ou morre é filtrado ao vivo.</summary>
internal sealed class TheftIndex
{
    private readonly Dictionary<CellCoord, List<Npc>> _byCell = [];
    private readonly Dictionary<CityId, int> _guards = [];

    private TheftIndex(WorldState world, int guardProfessionId)
    {
        foreach (var npc in world.Npcs)
        {
            if (!npc.IsAlive) continue;
            if (!_byCell.TryGetValue(npc.CurrentLocation, out var cell)) _byCell[npc.CurrentLocation] = cell = [];
            cell.Add(npc);
            if (guardProfessionId >= 0 && npc.Profession.Id == guardProfessionId)
                _guards[npc.City] = _guards.GetValueOrDefault(npc.City) + 1;
        }
    }

    /// <summary>Nulo com o furto desligado: o caminho padrão paga uma checagem de bool.</summary>
    public static TheftIndex? BuildForTick(WorldState world) =>
        world.TheftRules.Enabled ? new TheftIndex(world, world.TheftRules.GuardProfessionId) : null;

    public int GuardsIn(CityId city) => _guards.GetValueOrDefault(city);

    /// <summary>Quem tem mais moeda na célula do ladrão, fora da própria casa (empate: menor id).</summary>
    public Npc? FindVictim(Npc thief)
    {
        Npc? best = null;
        if (!_byCell.TryGetValue(thief.CurrentLocation, out var cell)) return null;
        foreach (var other in cell)
        {
            if (!other.IsAlive || other.Id == thief.Id || other.CurrentLocation != thief.CurrentLocation) continue;
            if (other.Wallet.Amount < 1) continue;
            if (thief.Household is { } home && other.Household == home) continue;
            if (best is null || other.Wallet.Amount > best.Wallet.Amount
                || (other.Wallet.Amount == best.Wallet.Amount && other.Id.Value < best.Id.Value))
                best = other;
        }
        return best;
    }
}
