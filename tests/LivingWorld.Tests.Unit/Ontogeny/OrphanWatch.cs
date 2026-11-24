using LivingWorld.Domain.History;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Family;

namespace LivingWorld.Tests.Unit.Ontogeny;

/// <summary>Observação do acolhimento de órfãos (<see cref="OntogenyDebtsTests"/>), igual nos dois braços: mortes de crianças
/// por privação (sink) e meses-criança <b>seguidos</b> sem adulto no domicílio com adulto disponível na cidade (fim de cada mês).
/// O braço com acolhimento (regras padrão) lê da corrida compartilhada <see cref="Population.DefaultVillageDecade"/>.</summary>
internal sealed class OrphanWatch
{
    public const int Months = 6 * 12;

    private HashSet<NpcId> uncoveredBefore = [];

    public int Deaths { get; private set; }
    public int OrphanMonths { get; private set; }

    /// <summary>Falso depois do último mês observado: mortes posteriores (corrida compartilhada segue até o ano 10) não contam.</summary>
    public bool Active { get; set; } = true;

    public IWorldEventSink Sink(WorldState world) => new DeathSink(this, world);

    public void AfterMonth(WorldState world)
    {
        var now = world.CurrentDate;
        var rules = world.LifeStageRules;
        var uncoveredNow = new HashSet<NpcId>();
        foreach (var kid in world.Npcs)
        {
            if (!kid.IsAlive || rules.LifeStageOf(kid.AgeYears(now)) != LifeStage.Child) continue;
            bool covered = kid.Household is { } id && world.FindHousehold(id) is { } home && HouseholdRedistribution.HasLivingAdultOrElder(world, home, rules, now);
            if (covered) continue;
            bool adultAvailable = world.Npcs.Any(n => n.IsAlive && n.Household is not null && n.City == kid.City && rules.LifeStageOf(n.AgeYears(now)) is LifeStage.Adult or LifeStage.Elder);
            if (!adultAvailable) continue;
            uncoveredNow.Add(kid.Id);
            if (uncoveredBefore.Contains(kid.Id)) OrphanMonths++;
        }
        uncoveredBefore = uncoveredNow;
    }

    private sealed class DeathSink(OrphanWatch watch, WorldState world) : IWorldEventSink
    {
        public void Record(WorldEvent e)
        {
            if (!watch.Active || e.Kind is not (WorldEventKind.Starvation or WorldEventKind.Dehydration or WorldEventKind.Exhaustion)) return;
            if (world.FindNpc(new NpcId(long.Parse(e.Payload.Split('|')[0]))) is { } npc && npc.AgeYears(world.CurrentDate) < 14) watch.Deaths++;
        }
    }
}
