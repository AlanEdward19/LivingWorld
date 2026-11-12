using LivingWorld.Domain.Economy;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
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
        if (rules.Bonds is { } bonds)
        {
            TickWithBonds(world, ctx, rules, bonds, now);
            return;
        }

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

    // --- Vínculo por convivência real (AD-128) ---

    private readonly List<Npc> _people = [];
    private readonly List<RelationshipKey> _forgotten = [];

    private void TickWithBonds(WorldState world, TickContext ctx, FamilyRules rules, SocialBondRules bonds, long now)
    {
        long day = now / world.CurrentDate.Calendar.HoursPerDay;

        // Casa: grupo pequeno, todo mundo convive com todo mundo todo dia.
        _households.Clear();
        _households.AddRange(world.Households);
        _households.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
        foreach (var household in _households)
        {
            CollectAlive(world, household.Members, at: null);
            var people = _people;
            for (int i = 0; i < people.Count; i++)
                for (int j = 0; j < people.Count; j++)
                    if (i != j) Bond(world, rules, bonds, now, people[i], people[j], sameHousehold: true);
        }

        // Trabalho: cada um cruza com alguns colegas no dia e passa o tempo com os poucos de quem mais gosta.
        _workplaces.Clear();
        _workplaces.AddRange(world.Workplaces);
        _workplaces.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
        foreach (var workplace in _workplaces)
        {
            CollectAlive(world, workplace.Employees, at: workplace.Location);
            MeetCoworkers(world, ctx, rules, bonds, now, day, _people);
        }

        // Namoro: quem corteja procura o outro todo dia; é no cortejo que o vínculo do casal se faz.
        foreach (var suitor in world.AliveNpcIndex.Alive)
            if (suitor.CourtingWith is { } courtedId && suitor.Id.Value < courtedId.Value
                && world.FindNpc(courtedId) is { IsAlive: true } courted && courted.CourtingWith == suitor.Id)
            {
                Bond(world, rules, bonds, now, suitor, courted, sameHousehold: false);
                Bond(world, rules, bonds, now, courted, suitor, sameHousehold: false);
            }

        if (day % bonds.FadeEveryDays == 0)
            FadeAndForget(world, rules, bonds, now);
    }

    private void CollectAlive(WorldState world, IReadOnlyList<NpcId> ids, LivingWorld.Domain.Geography.CellCoord? at)
    {
        _people.Clear();
        foreach (var id in ids)
            if (world.FindNpc(id) is { IsAlive: true } npc && (at is null || npc.CurrentLocation == at.Value))
                _people.Add(npc);
        _people.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
    }

    private static void MeetCoworkers(
        WorldState world, TickContext ctx, FamilyRules rules, SocialBondRules bonds, long now, long day, List<Npc> present)
    {
        int others = present.Count - 1;
        if (others <= 0 || bonds.WorkContactsPerDay == 0) return;

        // Turma: numa oficina grande cada um trabalha ao lado dos mesmos poucos (vizinhos na ordem estável por Id), e é
        // entre eles que cruza com alguém no dia. Sem isso, entre mil colegas ninguém reencontra ninguém.
        int crew = Math.Min(bonds.WorkCrewSize, others);
        int encounters = Math.Min(bonds.WorkEncountersPerDay, crew);
        int contacts = Math.Min(bonds.WorkContactsPerDay, encounters);
        Span<int> met = stackalloc int[encounters];
        Span<double> appeal = stackalloc double[encounters];

        for (int i = 0; i < present.Count; i++)
        {
            var self = present[i];
            var rng = ctx.StreamFor("work-contact", unchecked(self.Id.Value * 1_000_003L + day));

            // Com quem cruzou hoje: sorteio sem repetição dentro da turma (turma pequena = todos).
            int count = 0;
            int first = i - crew / 2;
            if (encounters == crew)
            {
                for (int step = 0; count < crew; step++)
                {
                    int j = Wrap(first + step, present.Count);
                    if (j != i) met[count++] = j;
                }
            }
            else
            {
                while (count < encounters)
                {
                    int j = Wrap(first + Math.Min((int)(rng.NextDouble() * (crew + 1)), crew), present.Count);
                    if (j == i || met[..count].Contains(j)) continue;
                    met[count++] = j;
                }
            }

            // Atração do dia: o afeto que já existe, a afinidade de temperamento e um pouco de acaso.
            for (int k = 0; k < count; k++)
            {
                var other = present[met[k]];
                double affection = world.Relationships.TryGetValue(new RelationshipKey(self.Id, other.Id), out var known) ? known.Affection : 0.0;
                appeal[k] = affection + 50.0 * SocialBondRules.Likability(self.Personality, other.Personality) + 20.0 * rng.NextDouble();
            }

            for (int c = 0; c < contacts; c++)
            {
                int best = -1;
                for (int k = 0; k < count; k++)
                    if (appeal[k] >= 0 && (best < 0 || appeal[k] > appeal[best])) best = k;
                if (best < 0) break;

                var other = present[met[best]];
                appeal[best] = -1;
                Bond(world, rules, bonds, now, self, other, sameHousehold: false);
                Bond(world, rules, bonds, now, other, self, sameHousehold: false);
            }
        }
    }

    private static int Wrap(int index, int count) => ((index % count) + count) % count;

    internal static void Bond(
        WorldState world, FamilyRules rules, SocialBondRules bonds, long now, Npc from, Npc to, bool sameHousehold,
        RelationshipEventType type = RelationshipEventType.Cohabitation)
    {
        var relationship = world.GetOrCreateRelationship(new RelationshipKey(from.Id, to.Id), now);
        double trustCeiling = bonds.TrustCeiling(to.Personality);
        double affectionCeiling = bonds.AffectionCeiling(from.Personality, to.Personality, sameHousehold);
        // mágoa com o cônjuge baixa o que a convivência consegue refazer (AD-136)
        if (from.Spouse == to.Id && from.Union is { Grievance: > 0 } union)
            (affectionCeiling, trustCeiling) = UnionSystem.MaritalCeilings(affectionCeiling, trustCeiling, union.Grievance);
        relationship.ApplyBondingEvent(type, rules, trustCeiling, affectionCeiling);
        relationship.MarkContact(now);
    }

    private void FadeAndForget(WorldState world, FamilyRules rules, SocialBondRules bonds, long now)
    {
        long lossThresholdHours = (long)rules.ContactLossThresholdDays * world.CurrentDate.Calendar.HoursPerDay;
        _forgotten.Clear();
        // Cada par só lê e escreve a si mesmo: a ordem de iteração não chega ao estado. As remoções saem ordenadas.
        foreach (var (key, relationship) in world.Relationships)
        {
            if (now - relationship.LastContactTick <= lossThresholdHours) continue;

            bool kin = IsKin(world.FindNpc(key.From), world.FindNpc(key.To));
            relationship.Fade(rules, bonds.FadeEveryDays, kin ? bonds.KinBondFloor : 0.0);
            if (!kin && relationship.IsFaded(rules.NeutralAxisValue)) _forgotten.Add(key);
        }

        _forgotten.Sort((a, b) => a.From.Value != b.From.Value ? a.From.Value.CompareTo(b.From.Value) : a.To.Value.CompareTo(b.To.Value));
        foreach (var key in _forgotten) world.RemoveRelationship(key);
    }

    /// <summary>Pais, filhos, cônjuge e irmãos (mesma mãe ou mesmo pai): vínculo que o tempo sem contato não apaga.</summary>
    internal static bool IsKin(Npc? a, Npc? b)
    {
        if (a is null || b is null) return false;
        if (a.MotherId == b.Id || a.FatherId == b.Id || b.MotherId == a.Id || b.FatherId == a.Id) return true;
        if (a.Spouse == b.Id || b.Spouse == a.Id) return true;
        return (a.MotherId is { } mother && b.MotherId == mother) || (a.FatherId is { } father && b.FatherId == father);
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
