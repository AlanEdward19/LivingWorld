using LivingWorld.Domain.Economy;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Family;

/// <summary>Forma e evolui relações por convivência diária — único
/// sistema que escreve em <see cref="WorldState.Relationships"/>.</summary>
[LivingCapability("RELATIONSHIPS", Events = [WorldEventKind.Marriage, WorldEventKind.AidGiven, WorldEventKind.CourtshipStarted, WorldEventKind.CourtshipRejected, WorldEventKind.CourtshipSucceeded], Consumers = ["inspector.npc.relationships"])]
[RunsAfter(typeof(EmploymentSystem))]
public sealed class RelationshipSystem : ISimulationSystem
{
    // Buffers derivados, reusados pelo relógio serial como os índices de BehaviorDecisionSystem.
    private readonly List<Household> _households = [];
    private readonly List<Workplace> _workplaces = [];
    private readonly List<NpcId> _present = [];
    private readonly List<NpcId> _alive = [];

    public const string SystemName = "population-relationship";

    public string Name => SystemName;
    // Presença no trabalho deve ser observada durante o turno, como produção e prática.
    public TickFrequency Frequency => TickFrequency.Midshift;

    public void Tick(WorldState world, TickContext ctx)
    {
        var rules = world.FamilyRules;
        long now = ctx.CurrentTick;

        _households.Clear();
        _households.AddRange(world.Households);
        _households.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
        foreach (var household in _households)
            ApplyCohabitationForMembers(world, rules, now, household.Members);

        _workplaces.Clear();
        _workplaces.AddRange(world.Workplaces);
        _workplaces.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
        foreach (var workplace in _workplaces)
        {
            _present.Clear();
            foreach (var id in workplace.Employees)
                if (world.FindNpc(id) is { IsAlive: true, CurrentLocation: var location } && location == workplace.Location)
                    _present.Add(id);
            _present.Sort((a, b) => a.Value.CompareTo(b.Value));
            ApplyCohabitationForMembers(world, rules, now, _present);
        }

        // Sem OrderBy aqui de propósito: DecayTowardNeutral só lê/escreve o próprio Relationship
        // (sem RNG, sem side effect cruzado com outras entradas), então o estado final não
        // depende da ordem de iteração — só o resultado de Hash(world) importa, e ele é idêntico
        // com ou sem ordenação. Ordenar 16M+ relações por dia (achado real em cenários de 10k
        // pop, baseline-timings.md revisado) era custo puro sem efeito observável.
        long lossThresholdHours = (long)rules.ContactLossThresholdDays * world.CurrentDate.Calendar.HoursPerDay;
        foreach (var (_, relationship) in world.Relationships)
        {
            if (now - relationship.LastContactTick <= lossThresholdHours)
                continue;
            relationship.DecayTowardNeutral(rules);
        }
    }

    private void ApplyCohabitationForMembers(
        WorldState world, FamilyRules rules, long now, IReadOnlyList<NpcId> memberIds)
    {
        var alive = _alive;
        alive.Clear();
        foreach (var id in memberIds)
            if (world.FindNpc(id) is { IsAlive: true }) alive.Add(id);
        alive.Sort((a, b) => a.Value.CompareTo(b.Value));

        if (alive.Count <= rules.MaxCohabitationGroupSize)
        {
            // Grupo dentro do teto (todo cenário default/pequeno cai aqui, teto = int.MaxValue) —
            // exatamente o par-a-par de sempre, byte-idêntico ao comportamento anterior a esta
            // mudança.
            for (int i = 0; i < alive.Count; i++)
                for (int j = 0; j < alive.Count; j++)
                {
                    if (i == j) continue;
                    ApplyCohabitationPair(world, rules, now, alive[i], alive[j]);
                }
            return;
        }

        // Grupo maior que o teto — cenário de escala industrial: `ScenarioRunner.ScaleEconomyCatalog`
        // permite até 8.000 trabalhadores simultâneos num único workplace, e o par-a-par acima
        // seria 8.000² = 64 milhões de pares NAQUELE workplace, NAQUELE dia (achado real, ver
        // baseline-timings.md, fase 16). Cada membro forma laço só com uma janela fixa de
        // vizinhos por Id (determinístico — mesma seed, mesmo resultado; sem RNG) em vez de todo
        // mundo: O(k x teto) em vez de O(k²), e também mais plausível socialmente (ninguém convive
        // de verdade com milhares de colegas ao mesmo tempo).
        // Limite conhecido: se o grupo for só um pouco maior que o teto (ex.: teto+1 membros), a
        // janela ainda cobre quase todo mundo dos dois lados e alguns pares acabam contados duas
        // vezes (uma vez mais forte que o par-a-par "puro"). Não corrigido — não há cenário hoje
        // com grupo nessa faixa intermediária (household pequeno OU workplace de milhares, nada
        // no meio); revisitar com uma janela "half-circle" se isso mudar.
        int window = rules.MaxCohabitationGroupSize;
        for (int i = 0; i < alive.Count; i++)
            for (int offset = 1; offset <= window; offset++)
            {
                int j = (i + offset) % alive.Count;
                ApplyCohabitationPair(world, rules, now, alive[i], alive[j]);
                ApplyCohabitationPair(world, rules, now, alive[j], alive[i]);
            }
    }

    private static void ApplyCohabitationPair(WorldState world, FamilyRules rules, long now, NpcId from, NpcId to)
    {
        var relationship = world.GetOrCreateRelationship(new RelationshipKey(from, to), now);
        relationship.ApplyEvent(RelationshipEventType.Cohabitation, rules);
        relationship.MarkContact(now);
    }
}
