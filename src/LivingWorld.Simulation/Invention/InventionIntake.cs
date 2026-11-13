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
        var best = npc.Household is { } h ? ledger.BestCraft(h.Value, rules) : default;
        int craft = best.SpeedMmS;
        var (foot, byCraft) = WaterCrossing.Hours(world, water, craft);
        if (craft > 0 && byCraft < foot)
        {
            world.InventionTally.CraftWaterTrips++;
            world.InventionTally.HoursSavedX10 += (long)Math.Round(10 * (foot - byCraft));
            Sighted(world, npc, destination, best.DesignId);
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

    /// <summary>A peça chega com alguém (AD-143): quem está perto da chegada, é de outra casa da mesma cidade e já tem o problema da
    /// água (uma mente) vê a peça em uso e guarda o design para copiar. Evento, não varredura: só os baldes de células em volta.</summary>
    internal static void Sighted(WorldState world, Npc rower, CellCoord at, int designId)
    {
        if (designId <= 0) return;
        var rules = world.InventionRules!;
        var ledger = world.Invention!;
        if (ledger.Minds.Count == 0) return;
        int r = rules.SightRadius, looked = 0;
        for (int dy = -r; dy <= r && looked < rules.MaxWatchersPerSighting; dy++)
            for (int dx = -r; dx <= r && looked < rules.MaxWatchersPerSighting; dx++)
            {
                var bucket = world.AliveNpcIndex.At(new CellCoord(at.X + dx, at.Y + dy));
                for (int i = 0; i < bucket.Count && looked < rules.MaxWatchersPerSighting; i++)
                {
                    var other = bucket[i];
                    if (other.Household == rower.Household || other.City != rower.City) continue;
                    looked++;
                    int index = ledger.MindIndex(other.Id.Value);
                    if (index < 0 || ledger.Minds[index].SeenDesignId != 0) continue; // guarda a primeira que viu até copiá-la
                    ledger.PutMind(ledger.Minds[index] with { SeenDesignId = designId });
                    world.InventionTally.Sightings++;
                }
            }
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
        var city = world.FindNpc(new NpcId(design.InventorId))?.City ?? default;
        int accounts = Accounts(world, design.Id);
        ledger.Tombstones.Add(new Domain.Invention.DesignTombstone(design.Id, design.InventorId, design.Tick, tick, design.Parts.Count, city, accounts));
        ledger.Touch();
        world.InventionTally.Lost++;
        if (accounts > 0) world.InventionTally.TombstonesWithAccounts++;
    }

    /// <summary>Fatos da história que falam do design (funcionou, alguém aprendeu, alguém comprou) e os relatos que nasceram deles:
    /// os que ainda apontam para ele quando se perde (AD-143). Sem história ligada, nenhum.</summary>
    internal static int Accounts(WorldState world, int designId)
    {
        if (world.Facts.Count == 0) return 0;
        var facts = new HashSet<FactId>();
        foreach (var fact in world.Facts)
            if (DesignIdOf(fact.Kind, fact.Payload) == designId) facts.Add(fact.Id);
        if (facts.Count == 0) return 0;
        int reports = 0;
        foreach (var report in world.Reports) if (facts.Contains(report.OriginFactId)) reports++;
        return facts.Count + reports;
    }

    /// <summary>O design de que um fato fala, pelo campo do payload do evento; 0 = nenhum.</summary>
    public static int DesignIdOf(WorldEventKind kind, string payload)
    {
        int field = kind switch
        {
            WorldEventKind.DesignPrototyped => 1,
            WorldEventKind.DesignLearned or WorldEventKind.DesignSold => 2,
            _ => -1,
        };
        if (field < 0) return 0;
        var parts = payload.Split('|');
        return parts.Length > field && int.TryParse(parts[field], System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out int id) ? id : 0;
    }
}
