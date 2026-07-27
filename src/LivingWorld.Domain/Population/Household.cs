using System.Text.Json.Serialization;

namespace LivingWorld.Domain;

/// <summary>Família como unidade (task 3): residência, membros e chefe. Nascimento entra,
/// morte remove; sem membros, <see cref="IsEmpty"/> sinaliza dissolução para quem gerencia a
/// lista em <c>WorldState</c> — o tipo em si não se autodestrói (não tem acesso à coleção que
/// o contém).</summary>
public sealed class Household
{
    public HouseholdId Id { get; }
    public CellCoord Location { get; }

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

    public Household(
        HouseholdId id, CellCoord location, NpcId head, IReadOnlyList<NpcId> members,
        IReadOnlyDictionary<ResourceType, long>? stock = null)
    {
        if (!members.Contains(head))
            throw new ArgumentException("Head precisa estar entre os Members", nameof(head));
        Id = id;
        Location = location;
        Head = head;
        _members = members.ToList();
        _stock = new Dictionary<ResourceType, long>(stock ?? new Dictionary<ResourceType, long>());
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
}
