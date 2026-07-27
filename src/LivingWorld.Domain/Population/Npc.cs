using System.Text.Json.Serialization;

namespace LivingWorld.Domain;

/// <summary>O indivíduo simulado: identidade, saúde, localização, necessidades (Fase 4),
/// personalidade e profissão. Mutável (mesmo padrão de <c>WorldState</c>): idade nunca é campo
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

    // Fase 4 (task 6): necessidades, personalidade, profissão, localização e ação corrente.
    public int Hunger { get; private set; }
    public int Thirst { get; private set; }
    public int Sleep { get; private set; }
    public int Social { get; private set; }
    public Personality Personality { get; }
    public ProfessionType Profession { get; private set; }
    public CellCoord CurrentLocation { get; private set; }
    public ActionType? CurrentAction { get; private set; }
    public long ActionStartedAtTick { get; private set; }
    public long? HungerZeroSinceTick { get; private set; }

    /// <summary>Nulo enquanto <see cref="Household"/> existir (NEEDS-16). <see cref="LeaveHousehold"/>
    /// grava o timestamp quando o household deixa de existir; <see cref="JoinHousehold"/> limpa.</summary>
    public WorldDate? HomelessSince { get; private set; }

    // Fase 5 (T5): saldo pessoal e vínculo empregatício.
    public Money Wallet { get; private set; }
    public WorkplaceId? Employer { get; private set; }

    // Fase 6 (T7): habilidade, gene de taxa (imutável após nascimento, mesmo padrão de
    // Personality) e vínculo de tutoria mestre->aprendiz.
    public SkillSet Skills { get; private set; }
    public RateGene RateGene { get; }
    public NpcId? Mentor { get; private set; }

    // Fase 7 (T7): genética/ambiente (imutáveis após nascimento, herdados por HeredityService),
    // cônjuge e cortejo em andamento.
    public double Vitality { get; }
    public double Upbringing { get; }
    public NpcId? Spouse { get; private set; }
    public NpcId? CourtingWith { get; private set; }

    /// <summary>Derivado de <see cref="DeathDate"/> — <see cref="JsonIgnoreAttribute"/> pelo
    /// mesmo motivo de <see cref="Household.IsEmpty"/>: computado, e um bool solto no snapshot
    /// quebraria o mutador genérico de teste.</summary>
    [JsonIgnore]
    public bool IsAlive => DeathDate is null;

    public Npc(
        NpcId id, string name, Sex sex, WorldDate birthDate, CultureId culture, CellCoord birthLocation,
        NpcId? motherId, NpcId? fatherId, HouseholdId? household, int health,
        Personality personality, ProfessionType profession, CellCoord currentLocation,
        int hunger = 100, int thirst = 100, int sleep = 100, int social = 100,
        ActionType? currentAction = null, long actionStartedAtTick = 0,
        long? hungerZeroSinceTick = null, WorldDate? homelessSince = null,
        WorldDate? pregnantUntil = null, WorldDate? deathDate = null,
        Money wallet = default, WorkplaceId? employer = null,
        SkillSet? skills = null, RateGene? rateGene = null, NpcId? mentor = null,
        double vitality = 50.0, double upbringing = 50.0, NpcId? spouse = null, NpcId? courtingWith = null)
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
        Personality = personality;
        Profession = profession;
        Hunger = Math.Clamp(hunger, 0, 100);
        Thirst = Math.Clamp(thirst, 0, 100);
        Sleep = Math.Clamp(sleep, 0, 100);
        Social = Math.Clamp(social, 0, 100);
        CurrentLocation = currentLocation;
        CurrentAction = currentAction;
        ActionStartedAtTick = actionStartedAtTick;
        HungerZeroSinceTick = hungerZeroSinceTick;
        HomelessSince = homelessSince;
        PregnantUntil = pregnantUntil;
        DeathDate = deathDate;
        Wallet = wallet;
        Employer = employer;
        Skills = skills ?? SkillSet.Initial(0);
        RateGene = rateGene ?? new RateGene(1.0);
        Mentor = mentor;
        Vitality = Math.Clamp(vitality, 0.0, 100.0);
        Upbringing = Math.Clamp(upbringing, 0.0, 100.0);
        Spouse = spouse;
        CourtingWith = courtingWith;
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

    public void JoinHousehold(HouseholdId household)
    {
        Household = household;
        HomelessSince = null;
    }

    /// <summary>Limpa a referência quando o household deixa de existir (dissolvido) — nunca
    /// deixa <see cref="Household"/> apontando para um id removido do mundo (sweep referencial,
    /// task 12). Enquanto o household ainda existir, a referência do NPC morto permanece: é
    /// residência histórica válida, não ponteiro solto. Marca <see cref="HomelessSince"/>
    /// (NEEDS-16) no momento exato em que a referência é limpa.</summary>
    public void LeaveHousehold(WorldDate now)
    {
        Household = null;
        HomelessSince = now;
    }

    public void SetHealth(int health) => Health = Math.Clamp(health, 0, 100);

    public void SetHunger(int hunger) => Hunger = Math.Clamp(hunger, 0, 100);

    public void SetThirst(int thirst) => Thirst = Math.Clamp(thirst, 0, 100);

    public void SetSleep(int sleep) => Sleep = Math.Clamp(sleep, 0, 100);

    public void SetSocial(int social) => Social = Math.Clamp(social, 0, 100);

    /// <summary>Atualiza o local corrente do NPC (task 6). <paramref name="tick"/> é aceito para
    /// manter a assinatura pedida pelo design — nenhum sistema desta task consome esse valor
    /// ainda (deslocamento com custo é T11/T14).</summary>
    public void MoveTo(CellCoord destination, long tick) => CurrentLocation = destination;

    public void SetCurrentAction(ActionType action, long tick)
    {
        CurrentAction = action;
        ActionStartedAtTick = tick;
    }

    public void MarkHungerZeroSince(long tick) => HungerZeroSinceTick = tick;

    public void ClearHungerZeroSince() => HungerZeroSinceTick = null;

    /// <summary>NEEDS-05: objetivo ativo e inspecionável — necessidade zerada dispara sempre
    /// (NEEDS-02, independente do limiar configurado); necessidade cujo déficit (100 − valor)
    /// supera <see cref="NeedsRules.UrgencyThreshold"/> também é urgente.</summary>
    public bool HasUrgentNeed(NeedsRules rules) =>
        IsUrgent(Hunger, rules) || IsUrgent(Thirst, rules) || IsUrgent(Sleep, rules) || IsUrgent(Social, rules);

    private static bool IsUrgent(int need, NeedsRules rules) => need == 0 || 100 - need > rules.UrgencyThreshold;

    public void BecomePregnant(WorldDate dueDate) => PregnantUntil = dueDate;

    public void ClearPregnancy() => PregnantUntil = null;

    public void CreditWallet(Money amount) => Wallet += amount;

    /// <summary>Delega a <see cref="Money.TryDebit"/> — nunca deixa <see cref="Wallet"/>
    /// negativo (mesma garantia usada por <see cref="Workplace.TryDebitTreasury"/>).</summary>
    public Result<Money> TryDebitWallet(Money amount)
    {
        var result = Wallet.TryDebit(amount);
        if (result.IsSuccess)
            Wallet = result.Value;
        return result;
    }

    /// <summary>Vínculo empregatício (ECON-18) — espelha <see cref="JoinHousehold"/>.</summary>
    public void Hire(WorkplaceId workplace) => Employer = workplace;

    /// <summary>Desliga o vínculo (ECON-19) — espelha <see cref="LeaveHousehold"/>.</summary>
    public void Fire() => Employer = null;

    /// <summary>Troca de profissão (SKILL-14) — muda apenas <see cref="Profession"/>; nunca
    /// toca <see cref="Skills"/>. Estagnação da habilidade antiga é ausência de ganho (ela só
    /// para de subir por prática porque deixa de ser a profissão corrente), não um reset nem um
    /// campo novo de "profissão antiga" (Tech Decision do design).</summary>
    public void SwitchProfession(ProfessionType newProfession) => Profession = newProfession;

    /// <summary>Aplica ganho de habilidade (SKILL-01/03..08) — delega o clamp de teto a <see
    /// cref="SkillSet.WithGain"/>; único ponto que reatribui <see cref="Skills"/> fora do
    /// construtor (mesmo padrão de mutador dedicado de <see cref="SetHunger"/> etc).</summary>
    public void GainSkill(SkillType type, double delta, double cap) => Skills = Skills.WithGain(type, delta, cap);

    /// <summary>Vínculo de tutoria mestre->aprendiz (SKILL-08) — espelha <see cref="JoinHousehold"/>.</summary>
    public void AssignMentor(NpcId mentor) => Mentor = mentor;

    /// <summary>Encerra o vínculo de tutoria — espelha <see cref="LeaveHousehold"/> (Edge Case:
    /// mestre morto no meio da tutoria, sem ponteiro solto).</summary>
    public void ClearMentor() => Mentor = null;

    /// <summary>Casamento (Fase 7, T7, FAM-12) — seta só o próprio lado do vínculo; quem chama
    /// (<c>MarriageSystem.Marry</c>) é responsável por chamar duas vezes, uma para cada cônjuge.
    /// Nunca há mutador de "divorciar" (AD-060): viuvez é lida (<see cref="Spouse"/> aponta a
    /// alguém com <c>IsAlive == false</c>), nunca limpa automaticamente — mesmo espírito de
    /// <see cref="MotherId"/>/<see cref="FatherId"/> (AD-031, referência histórica válida).</summary>
    public void Marry(NpcId spouse) => Spouse = spouse;

    /// <summary>Início de cortejo (Fase 7, T7) — espelha <see cref="AssignMentor"/>.</summary>
    public void StartCourtship(NpcId partner) => CourtingWith = partner;

    /// <summary>Fim de cortejo, com ou sem sucesso — espelha <see cref="ClearMentor"/>.</summary>
    public void EndCourtship() => CourtingWith = null;
}
