using System.Text.Json.Serialization;

namespace LivingWorld.Domain;

/// <summary>O indivíduo simulado (task 1): identidade, saúde e localização — sem necessidades
/// nem profissão (Fase 4/5). Mutável (mesmo padrão de <c>WorldState</c>): idade nunca é campo
/// que um sistema incrementa, é derivada de <see cref="AgeYears"/> (task 2). Reconstrutível por
/// inteiro a partir de um único construtor público — <c>System.Text.Json</c> usa esse
/// construtor no round-trip do snapshot, então todo campo mutável precisa estar nele.</summary>
public sealed class Npc
{
    public NpcId Id { get; }
    public string Name { get; }
    public Sex Sex { get; }
    public WorldDate BirthDate { get; }
    public CultureId Culture { get; }
    public CellCoord BirthLocation { get; }
    public NpcId? MotherId { get; }
    public NpcId? FatherId { get; }

    public HouseholdId? Household { get; private set; }
    public int Health { get; private set; }
    public WorldDate? PregnantUntil { get; private set; }
    public WorldDate? DeathDate { get; private set; }

    /// <summary>Derivado de <see cref="DeathDate"/> — <see cref="JsonIgnoreAttribute"/> pelo
    /// mesmo motivo de <see cref="Household.IsEmpty"/>: computado, e um bool solto no snapshot
    /// quebraria o mutador genérico de teste.</summary>
    [JsonIgnore]
    public bool IsAlive => DeathDate is null;

    public Npc(
        NpcId id, string name, Sex sex, WorldDate birthDate, CultureId culture, CellCoord birthLocation,
        NpcId? motherId, NpcId? fatherId, HouseholdId? household, int health,
        WorldDate? pregnantUntil = null, WorldDate? deathDate = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name não pode ser vazio", nameof(name));
        if (health is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(health), health, "Health deve estar em [0,100]");
        if (deathDate is { } d && d < birthDate)
            throw new ArgumentOutOfRangeException(nameof(deathDate), deathDate, "DeathDate não pode ser anterior a BirthDate");

        Id = id;
        Name = name;
        Sex = sex;
        BirthDate = birthDate;
        Culture = culture;
        BirthLocation = birthLocation;
        MotherId = motherId;
        FatherId = fatherId;
        Household = household;
        Health = health;
        PregnantUntil = pregnantUntil;
        DeathDate = deathDate;
    }

    /// <summary>Idade derivada de <paramref name="now"/> — nunca incrementada por sistema
    /// nenhum (task 2/critério "idade responde ao relógio"). Congela na morte.</summary>
    public int AgeYears(WorldDate now)
    {
        var end = DeathDate ?? now;
        long hours = end.TotalHours - BirthDate.TotalHours;
        return (int)(hours / BirthDate.Calendar.HoursPerYear);
    }

    public void Die(WorldDate deathDate)
    {
        if (!IsAlive)
            throw new InvalidOperationException($"NPC {Id} já está morto");
        if (deathDate < BirthDate)
            throw new ArgumentOutOfRangeException(nameof(deathDate), deathDate, "DeathDate não pode ser anterior a BirthDate");
        DeathDate = deathDate;
    }

    public void JoinHousehold(HouseholdId household) => Household = household;

    /// <summary>Limpa a referência quando o household deixa de existir (dissolvido) — nunca
    /// deixa <see cref="Household"/> apontando para um id removido do mundo (sweep referencial,
    /// task 12). Enquanto o household ainda existir, a referência do NPC morto permanece: é
    /// residência histórica válida, não ponteiro solto.</summary>
    public void LeaveHousehold() => Household = null;

    public void SetHealth(int health) => Health = Math.Clamp(health, 0, 100);

    public void BecomePregnant(WorldDate dueDate) => PregnantUntil = dueDate;

    public void ClearPregnancy() => PregnantUntil = null;
}
