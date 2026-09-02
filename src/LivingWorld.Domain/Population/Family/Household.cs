using System.Text.Json.Serialization;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Population.Family;

/// <summary>Família como unidade (task 3): residência, membros e chefe. Nascimento entra,
/// morte remove; sem membros, <see cref="IsEmpty"/> sinaliza dissolução para quem gerencia a
/// lista em <c>WorldState</c> — o tipo em si não se autodestrói (não tem acesso à coleção que
/// o contém).</summary>
public sealed class Household
{
    public HouseholdId Id { get; }
    public CellCoord Location { get; private set; }

    private readonly List<NpcId> _members;
    public IReadOnlyList<NpcId> Members => _members;
    public NpcId Head { get; private set; }

    /// <summary>Derivado de <see cref="Members"/> — <see cref="JsonIgnoreAttribute"/> porque é
    /// computado, não estado: entrar no snapshot só duplicaria <c>Members.Count</c> (e um bool
    /// solto quebraria o mutador genérico de teste, que só sabe mexer em long/int/string).</summary>
    [JsonIgnore]
    public bool IsEmpty => _members.Count == 0;

    // Fase 5 (T18): estoque de comida/água da residência — de onde Eat retira antes de restaurar
    // Hunger/Thirst. Sem capacidade declarada (EconomyRules só declara capacidade por
    // (ResourceType, LocationType) de Workplace, T4) — residência não tem teto nesta fase.
    private readonly Dictionary<ResourceType, long> _stock;
    public IReadOnlyDictionary<ResourceType, long> Stock => _stock;

    /// <summary>Cidade onde o household reside (Fase 8, T4, CITY-01). Mutável só por
    /// <see cref="JoinCity"/> — mesmo espírito de <see cref="Npc.City"/>.</summary>
    public CityId City { get; private set; }

    /// <summary>Destino de migração em andamento (Fase 15.1, Stage 4, T11, LWV-04.2) — enquanto
    /// preenchido, <see cref="City"/> continua sendo a origem até todos os membros chegarem.</summary>
    public CityId? PendingRelocationCity { get; private set; }

    public Household(
        HouseholdId id, CellCoord location, NpcId head, IReadOnlyList<NpcId> members,
        IReadOnlyDictionary<ResourceType, long>? stock = null, CityId city = default, CityId? pendingRelocationCity = null)
    {
        if (!members.Contains(head))
            throw new ArgumentException("Head precisa estar entre os Members", nameof(head));
        Id = id;
        Location = location;
        Head = head;
        _members = members.ToList();
        _stock = new Dictionary<ResourceType, long>(stock ?? new Dictionary<ResourceType, long>());
        City = city;
        PendingRelocationCity = pendingRelocationCity;
    }

    /// <summary>Sem capacidade declarada nesta fase — devolve sempre 0 de perda
    /// (<see cref="ResourceStock.Deposit"/> com capacidade ilimitada).</summary>
    public long Deposit(ResourceType resource, long amount) => ResourceStock.Deposit(_stock, resource, amount, long.MaxValue);

    public Result<long> Withdraw(ResourceType resource, long amount) => ResourceStock.Withdraw(_stock, resource, amount);

    public void AddMember(NpcId npc)
    {
        if (!_members.Contains(npc))
            _members.Add(npc);
    }

    /// <summary>Remove o membro; se era o chefe e ainda sobra alguém, promove o próximo por id
    /// (determinístico — nunca "o primeiro da lista" por ordem de inserção acidental).</summary>
    public void RemoveMember(NpcId npc)
    {
        _members.Remove(npc);
        if (Head == npc && _members.Count > 0)
            Head = _members.OrderBy(m => m.Value).First();
    }

    /// <summary>Muda a cidade do household (Fase 8, T4, CITY-01/CITY-07) — mesmo SPEC_DEVIATION de
    /// <see cref="Npc.JoinCity"/>: sem lista de membros a limpar, um único mutador basta.</summary>
    public void JoinCity(CityId city) => City = city;

    /// <summary>Muda a cidade e fixa a residência estável do household no novo assentamento.</summary>
    public void JoinCity(CityId city, CellCoord residence)
    {
        City = city;
        Location = residence;
    }

    /// <summary>Redireciona origem/destino de migração quando uma cidade é absorvida.</summary>
    public void ReplaceCityReference(CityId from, CityId to)
    {
        if (City == from) City = to;
        if (PendingRelocationCity == from) PendingRelocationCity = to;
        if (PendingRelocationCity == City) PendingRelocationCity = null;
    }

    /// <summary>Inicia migração para <paramref name="destination"/> sem mudar <see cref="City"/>
    /// até a chegada (Fase 15.1, Stage 4, T11).</summary>
    public void BeginRelocation(CityId destination) => PendingRelocationCity = destination;

    /// <summary>Conclui migração após chegada de todos os membros vivos.</summary>
    public void CompleteRelocation(CityId destination)
    {
        City = destination;
        PendingRelocationCity = null;
    }

    /// <summary>Conclui migração e atualiza a residência usada por sono, água e decisões futuras.</summary>
    public void CompleteRelocation(CityId destination, CellCoord residence)
    {
        City = destination;
        Location = residence;
        PendingRelocationCity = null;
    }
}
