using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Observation;

/// <summary>Contagem do ano feita pelo sink de eventos (issue #112 fatia A): nascimentos e mortes de NPC por
/// cidade, mortes por causa e por faixa etária, cidades fundidas. Só lê o mundo (cidade e idade do NPC no
/// instante do evento) e nunca escreve nele nem emite evento: o hash canônico é o mesmo com ou sem ela.
/// Só entram eventos de NPC (<c>NatalitySystem</c> e <c>NpcDeath</c>): fauna e flora usam os mesmos
/// <see cref="WorldEventKind"/> com outro <c>SourceSystem</c>. Entre anos, <see cref="Drain"/> devolve e zera.</summary>
public sealed class YearEventTally(WorldState world, int ageBandYears = 5) : IWorldEventSink
{
    public const int DefaultAgeBandYears = 5;
    public const string BirthSource = "NatalitySystem";
    public const string DeathSource = "NpcDeath";

    private Dictionary<CityId, CityTally> _byCity = [];
    private int _citiesMerged;

    public int AgeBandYears { get; } = ageBandYears > 0 ? ageBandYears : throw new ArgumentOutOfRangeException(nameof(ageBandYears));

    public void Record(WorldEvent evt)
    {
        switch (evt.Kind)
        {
            case WorldEventKind.CityMerged:
                _citiesMerged++;
                return;
            case WorldEventKind.Birth when evt.SourceSystem == BirthSource:
                if (NpcOf(evt.Payload) is { } baby) Of(baby.City).Births++;
                return;
            case WorldEventKind.Death or WorldEventKind.Starvation or WorldEventKind.Dehydration
                or WorldEventKind.Exhaustion or WorldEventKind.Disease or WorldEventKind.MaternalDeath
                or WorldEventKind.Accident or WorldEventKind.OldAge
                when evt.SourceSystem == DeathSource:
                if (NpcOf(evt.Payload) is not { } dead) return;
                var tally = Of(dead.City);
                tally.Deaths++;
                tally.ByCause[CauseOf(evt.Kind)] = tally.ByCause.GetValueOrDefault(CauseOf(evt.Kind)) + 1;
                int band = Math.Max(0, dead.AgeYears(world.CurrentDate)) / AgeBandYears;
                while (tally.DeathsByBand.Count <= band) tally.DeathsByBand.Add(0);
                tally.DeathsByBand[band]++;
                return;
        }
    }

    /// <summary>Causas de morte do relatório: fome/sede (<c>hunger_thirst</c>), doença (<c>disease</c>), idade (<see cref="WorldEventKind.Death"/>: mortalidade por idade e combate) e outras.</summary>
    public static string CauseOf(WorldEventKind kind) => kind switch
    {
        WorldEventKind.Starvation or WorldEventKind.Dehydration => "hunger_thirst",
        WorldEventKind.Disease => "disease",
        WorldEventKind.Death or WorldEventKind.OldAge => "age",
        _ => "other",
    };

    public (IReadOnlyDictionary<CityId, CityTally> ByCity, int CitiesMerged) Drain()
    {
        var drained = (_byCity, _citiesMerged);
        _byCity = [];
        _citiesMerged = 0;
        return drained;
    }

    private CityTally Of(CityId city) => _byCity.TryGetValue(city, out var t) ? t : _byCity[city] = new CityTally();

    private Domain.Population.Npc? NpcOf(string payload)
    {
        int cut = payload.IndexOf('|');
        string first = cut < 0 ? payload : payload[..cut];
        return long.TryParse(first, out long id) ? world.FindNpc(new NpcId(id)) : null;
    }
}

/// <summary>Acumulador mutável de uma cidade no ano. <see cref="DeathsByBand"/> é indexado por idade / largura da faixa.</summary>
public sealed class CityTally
{
    public long Births;
    public long Deaths;
    public Dictionary<string, long> ByCause { get; } = [];
    public List<long> DeathsByBand { get; } = [];
}
