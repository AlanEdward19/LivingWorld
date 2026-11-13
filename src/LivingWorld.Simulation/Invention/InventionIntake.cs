using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Invention;

/// <summary>O que a invenção ouve no instante em que acontece, sem varredura (AD-141): a chegada de um trajeto que cruzou água
/// (a pé vira frustração vivida; na embarcação da casa entra na conta do que ela poupou) e a morte (o saber de quem morreu se vai;
/// o último que sabia leva o design junto, se não sobrou instância). Mesmo padrão de <c>LifeEventIntake</c>.</summary>
public static class InventionIntake
{
    private const string DeathSource = "NpcDeath";

    public static void OnTravelArrived(WorldState world, Npc npc, CellCoord origin, CellCoord destination, long tick)
    {
        int water = WaterCrossing.CellsBetween(world.Map, origin, destination);
        if (water == 0) return;
        var rules = world.InventionRules!;
        var ledger = world.Invention!;
        int craft = npc.Household is { } h ? ledger.BestCraftSpeed(h.Value, rules) : 0;
        var (foot, byCraft) = WaterCrossing.Hours(world, water, craft);
        if (craft > 0 && byCraft < foot)
        {
            world.InventionTally.CraftWaterTrips++;
            world.InventionTally.HoursSavedX10 += (long)Math.Round(10 * (foot - byCraft));
            return;
        }

        world.InventionTally.FootWaterTrips++;
        if (craft > 0 || npc.AgeYears(world.CurrentDate) < rules.MinInventorAgeYears) return; // com embarcação em casa, andar foi escolha
        int minutes = (int)Math.Round(foot * 60);
        if (minutes <= 0) return;
        int index = ledger.MindIndex(npc.Id.Value);
        var mind = index >= 0 ? ledger.Minds[index] : new InventionMind(npc.Id.Value, 0, 0, 0, 0, Domain.Invention.FailureMode.None, null, 0);
        ledger.PutMind(mind with { FrustrationMinutes = (int)Math.Min(int.MaxValue / 2, (long)mind.FrustrationMinutes + minutes) });
    }

    public static void OnEvent(WorldState world, WorldEventKind kind, string payload, string sourceSystem, long tick)
    {
        if (sourceSystem != DeathSource || kind is not (WorldEventKind.Death or WorldEventKind.Starvation or WorldEventKind.Dehydration
            or WorldEventKind.Exhaustion or WorldEventKind.Disease or WorldEventKind.MaternalDeath)) return;
        int bar = payload.IndexOf('|');
        if (!long.TryParse(bar < 0 ? payload : payload[..bar], System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out long dead)) return;
        Forget(world, dead, tick);
    }

    /// <summary>Quem morreu deixa de saber; um design sem ninguém que saiba e sem instância vira lápide.</summary>
    internal static void Forget(WorldState world, long npcId, long tick)
    {
        var ledger = world.Invention!;
        int mind = ledger.MindIndex(npcId);
        if (mind >= 0) ledger.Minds.RemoveAt(mind);
        var forgotten = new List<int>();
        for (int i = ledger.Known.Count - 1; i >= 0; i--)
            if (ledger.Known[i].NpcId == npcId) { forgotten.Add(ledger.Known[i].DesignId); ledger.Known.RemoveAt(i); }
        if (forgotten.Count == 0) return;
        ledger.Touch();
        forgotten.Sort();
        foreach (int design in forgotten) BuryIfLost(world, design, tick);
    }

    /// <summary>Lápide quando ninguém vivo sabe e nenhuma instância sobrou (decisão 5 do ADR-0045).</summary>
    internal static void BuryIfLost(WorldState world, int designId, long tick)
    {
        var ledger = world.Invention!;
        foreach (var k in ledger.Known)
            if (k.DesignId == designId && world.FindNpc(new NpcId(k.NpcId)) is { IsAlive: true }) return;
        foreach (var item in ledger.Items) if (item.DesignId == designId) return;
        if (ledger.FindDesign(designId) is not { } design) return;
        ledger.Designs.Remove(design);
        ledger.Known.RemoveAll(k => k.DesignId == designId);
        ledger.Tombstones.Add(new Domain.Invention.DesignTombstone(design.Id, design.InventorId, design.Tick, tick, design.Parts.Count));
        ledger.Touch();
        world.InventionTally.Lost++;
    }
}
