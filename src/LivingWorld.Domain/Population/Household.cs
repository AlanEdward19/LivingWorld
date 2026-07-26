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

    public Household(HouseholdId id, CellCoord location, NpcId head, IReadOnlyList<NpcId> members)
    {
        if (!members.Contains(head))
            throw new ArgumentException("Head precisa estar entre os Members", nameof(head));
        Id = id;
        Location = location;
        Head = head;
        _members = members.ToList();
    }

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
