using System.Text.Json.Serialization;

namespace LivingWorld.Domain;

/// <summary>O indivíduo simulado: identidade, saúde, localização, necessidades (Fase 4),
/// personalidade e profissão. Mutável (mesmo padrão de <c>WorldState</c>): idade nunca é campo
/// que um sistema incrementa, é derivada de <see cref="AgeYears"/> (task 2). Reconstrutível por
/// inteiro a partir de um único construtor público — <c>System.Text.Json</c> usa esse
/// construtor no round-trip do snapshot, então todo campo mutável precisa estar nele.</summary>
public sealed class Npc
{
    [JsonIgnore]
    public Action<NpcId>? CanonicalMutationNotifier { get; set; }
    private void TouchCanonical() => CanonicalMutationNotifier?.Invoke(Id);

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

    // Fase 4 (task 6) / Fase 9 (PERF-09): necessidades lazy — canônicas no snapshot.
    public LazyNeed HungerNeed { get; private set; }
    public LazyNeed ThirstNeed { get; private set; }
    public LazyNeed SleepNeed { get; private set; }
    public LazyNeed SocialNeed { get; private set; }
    public Personality Personality { get; private set; }
    public ProfessionType Profession { get; private set; }
    public CellCoord CurrentLocation { get; private set; }
    public ActionType? CurrentAction { get; private set; }
    public long ActionStartedAtTick { get; private set; }
    public long? HungerZeroSinceTick { get; private set; }
    public bool IsGhost { get; private set; }

    /// <summary>Nulo enquanto <see cref="Household"/> existir (NEEDS-16). <see cref="LeaveHousehold"/>
    /// grava o timestamp quando o household deixa de existir; <see cref="JoinHousehold"/> limpa.</summary>
    public WorldDate? HomelessSince { get; private set; }

    // Fase 5 (T5): saldo pessoal e vínculo empregatício.
    public Money Wallet { get; private set; }
    public WorkplaceId? Employer { get; private set; }

    // Fase 6 (T7): habilidade, gene de taxa (imutável após nascimento; Personality só tem a
    // exceção explícita de autoria) e vínculo de tutoria mestre->aprendiz.
    public SkillSet Skills { get; private set; }
    public RateGene RateGene { get; }
    public NpcId? Mentor { get; private set; }

    // Fase 7 (T7): genética/ambiente (imutáveis após nascimento, herdados por HeredityService),
    // cônjuge e cortejo em andamento.
    public double Vitality { get; }
    public double Upbringing { get; }
    public NpcId? Spouse { get; private set; }
    public NpcId? CourtingWith { get; private set; }

    /// <summary>Cidade onde o NPC vive (Fase 8, T4, CITY-01) — nunca "sem cidade"
    /// (CITY-09: todo NPC vivo tem exatamente uma). Mutável só por <see cref="JoinCity"/>.</summary>
    public CityId City { get; private set; }

    // SPEC_DEVIATION (Fase 8, T9): design.md não declara este campo, mas a elegibilidade de
    // desmaterialização por ociosidade (CityRules.MaterializationIdleTicksBeforeEligible) exige
    // saber há quanto tempo o NPC está materializado — sem isso não há "ocioso" mensurável.
    // Null para todo NPC que nunca passou pelo pool agregado (seed inicial/nascimento): esses
    // nunca expiram por ociosidade (mesmo espírito de HomelessSince nulo = "nunca ficou sem
    // teto"). Só MaterializationSystem grava.

    /// <summary>Tick em que este NPC foi materializado a partir do <see
    /// cref="AggregatePopulationPool"/> da cidade (Fase 8, T9, CITY-05).</summary>
    public long? MaterializedAtTick { get; private set; }

    /// <summary>Ocupação de interior (Fase 15.1, T47) — <c>null</c> quando o NPC está em
    /// escopo World/City. Nunca substitui <see cref="CurrentLocation"/>/<see cref="City"/>: a
    /// localização global continua a mesma independente de estar dentro de um prédio.</summary>
    public InteriorOccupancy? Interior { get; private set; }

    /// <summary>Derivado de <see cref="DeathDate"/> — <see cref="JsonIgnoreAttribute"/> pelo
    /// mesmo motivo de <see cref="Household.IsEmpty"/>: computado, e um bool solto no snapshot
    /// quebraria o mutador genérico de teste.</summary>
    [JsonIgnore]
    public bool IsAlive => DeathDate is null;

    [JsonConstructor]
    public Npc(
        NpcId id, string name, Sex sex, WorldDate birthDate, CultureId culture, CellCoord birthLocation,
        NpcId? motherId, NpcId? fatherId, HouseholdId? household, int health,
        Personality personality, ProfessionType profession, CellCoord currentLocation,
        LazyNeed hungerNeed, LazyNeed thirstNeed, LazyNeed sleepNeed, LazyNeed socialNeed,
        ActionType? currentAction = null, long actionStartedAtTick = 0,
        long? hungerZeroSinceTick = null, WorldDate? homelessSince = null,
        WorldDate? pregnantUntil = null, WorldDate? deathDate = null,
        Money wallet = default, WorkplaceId? employer = null,
        SkillSet? skills = null, RateGene? rateGene = null, NpcId? mentor = null,
        double vitality = 50.0, double upbringing = 50.0, NpcId? spouse = null, NpcId? courtingWith = null,
        CityId city = default, long? materializedAtTick = null, InteriorOccupancy? interior = null,
        int carriedResourceId = 0, long carriedQuantity = 0, long carryCapacity = DefaultCarryCapacity,
        bool isGhost = false)
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
        HungerNeed = hungerNeed;
        ThirstNeed = thirstNeed;
        SleepNeed = sleepNeed;
        SocialNeed = socialNeed;
        CurrentLocation = currentLocation;
        CurrentAction = currentAction;
        ActionStartedAtTick = actionStartedAtTick;
        HungerZeroSinceTick = hungerZeroSinceTick;
        IsGhost = isGhost;
        HomelessSince = homelessSince;
        PregnantUntil = pregnantUntil;
        DeathDate = deathDate;
        Wallet = wallet;
        Employer = employer;
        Skills = skills ?? SkillSet.Empty;
        RateGene = rateGene ?? new RateGene(1.0);
        Mentor = mentor;
        Vitality = Math.Clamp(vitality, 0.0, 100.0);
        Upbringing = Math.Clamp(upbringing, 0.0, 100.0);
        Spouse = spouse;
        CourtingWith = courtingWith;
        City = city;
        MaterializedAtTick = materializedAtTick;
        Interior = interior;
        CarriedResourceId = carriedResourceId;
        CarriedQuantity = carriedQuantity;
        CarryCapacity = carryCapacity > 0 ? carryCapacity : DefaultCarryCapacity;
    }

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
        double vitality = 50.0, double upbringing = 50.0, NpcId? spouse = null, NpcId? courtingWith = null,
        CityId city = default, long? materializedAtTick = null, InteriorOccupancy? interior = null,
        int carriedResourceId = 0, long carriedQuantity = 0, long carryCapacity = DefaultCarryCapacity,
        bool isGhost = false)
        : this(
            id, name, sex, birthDate, culture, birthLocation, motherId, fatherId, household, health,
            personality, profession, currentLocation,
            LazyNeed.Initial(hunger, 0, 0), LazyNeed.Initial(thirst, 0, 0), LazyNeed.Initial(sleep, 0, 0), LazyNeed.Initial(social, 0, 0),
            currentAction, actionStartedAtTick, hungerZeroSinceTick, homelessSince, pregnantUntil, deathDate,
            wallet, employer, skills, rateGene, mentor, vitality, upbringing, spouse, courtingWith, city, materializedAtTick, interior,
            carriedResourceId, carriedQuantity, carryCapacity, isGhost)
    {
    }

    /// <summary>Recurso carregado em trânsito (Fase 15.1, Stage 4, T15). Zero significa as mãos
    /// vazias — água/comida só entram no estoque alvo depois da entrega.</summary>
    public int CarriedResourceId { get; private set; }
    public long CarriedQuantity { get; private set; }

    /// <summary>Teto base de carga (Fase 16.1). Poder <c>attribute.strength</c> multiplica na simulação, sem gravar o produto no NPC.</summary>
    public const long DefaultCarryCapacity = 10;
    public long CarryCapacity { get; private set; }

    public bool IsCarrying => CarriedQuantity > 0;

    public Result<Unit> PickUp(ResourceType resource, long quantity) =>
        PickUp(resource, quantity, CarryCapacity);

    public Result<Unit> PickUp(ResourceType resource, long quantity, long capacity)
    {
        if (quantity <= 0) return Result<Unit>.Fail("CarriedQuantity: deve ser > 0");
        if (IsCarrying) return Result<Unit>.Fail("carry: NPC já transporta um recurso");
        if (quantity > capacity) return Result<Unit>.Fail("carry: excede capacidade");
        CarriedResourceId = resource.Id;
        CarriedQuantity = quantity;
        TouchCanonical();
        return Result<Unit>.Ok(Unit.Value);
    }

    public Result<Unit> GiveCarriedTo(Household household)
    {
        if (!IsCarrying) return Result<Unit>.Fail("carry: nada em trânsito");
        household.Deposit(new ResourceType(CarriedResourceId), CarriedQuantity);
        CarriedResourceId = 0;
        CarriedQuantity = 0;
        TouchCanonical();
        return Result<Unit>.Ok(Unit.Value);
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
        TouchCanonical();
    }

    public void BecomeGhost()
    {
        IsGhost = true;
        TouchCanonical();
    }

    public void JoinHousehold(HouseholdId household)
    {
        Household = household;
        HomelessSince = null;
        TouchCanonical();
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
        TouchCanonical();
    }

    public void ConfigureNeedDecay(NeedsRules rules, long tick)
    {
        HungerNeed = HungerNeed.WithDecayRate(rules.HungerDecayPerHour, tick);
        ThirstNeed = ThirstNeed.WithDecayRate(rules.ThirstDecayPerHour, tick);
        SleepNeed = SleepNeed.WithDecayRate(rules.SleepDecayPerHour, tick);
        SocialNeed = SocialNeed.WithDecayRate(rules.SocialDecayPerHour, tick);
        TouchCanonical();
    }

    public int HungerAt(long tick) => NeedAsInt(HungerNeed, tick);
    public int ThirstAt(long tick) => NeedAsInt(ThirstNeed, tick);
    public int SleepAt(long tick) => NeedAsInt(SleepNeed, tick);
    public int SocialAt(long tick) => NeedAsInt(SocialNeed, tick);

    /// <summary>Valor materializado no tick da última escrita (compat de testes legados).</summary>
    [JsonIgnore] public int Hunger => NeedAsInt(HungerNeed, HungerNeed.TickOfLastEvent);
    [JsonIgnore] public int Thirst => NeedAsInt(ThirstNeed, ThirstNeed.TickOfLastEvent);
    [JsonIgnore] public int Sleep => NeedAsInt(SleepNeed, SleepNeed.TickOfLastEvent);
    [JsonIgnore] public int Social => NeedAsInt(SocialNeed, SocialNeed.TickOfLastEvent);

    private static int NeedAsInt(LazyNeed need, long tick) =>
        (int)Math.Round(need.ValueAt(tick), MidpointRounding.AwayFromZero);

    public void SetHealth(int health)
    {
        Health = Math.Clamp(health, 0, 100);
        TouchCanonical();
    }

    public void SetHunger(int hunger, long tick = 0)
    {
        HungerNeed = HungerNeed.WithValue(hunger, tick);
        TouchCanonical();
    }

    public void SetThirst(int thirst, long tick = 0)
    {
        ThirstNeed = ThirstNeed.WithValue(thirst, tick);
        TouchCanonical();
    }

    public void SetSleep(int sleep, long tick = 0)
    {
        SleepNeed = SleepNeed.WithValue(sleep, tick);
        TouchCanonical();
    }

    public void SetSocial(int social, long tick = 0)
    {
        SocialNeed = SocialNeed.WithValue(social, tick);
        TouchCanonical();
    }

    /// <summary>Atualiza o local corrente do NPC (task 6). <paramref name="tick"/> é aceito para
    /// manter a assinatura pedida pelo design — nenhum sistema desta task consome esse valor
    /// ainda (deslocamento com custo é T11/T14).</summary>
    public void MoveTo(CellCoord destination, long tick)
    {
        CurrentLocation = destination;
        TouchCanonical();
    }

    public void SetCurrentAction(ActionType action, long tick)
    {
        CurrentAction = action;
        ActionStartedAtTick = tick;
        TouchCanonical();
    }

    /// <summary>Intervenção explícita de autoria. A personalidade continua mudando somente como
    /// conjunto completo e já validado; sistemas ordinários nunca chamam este método.</summary>
    public void RewritePersonality(Personality personality)
    {
        Personality = personality;
        TouchCanonical();
    }

    public void MarkHungerZeroSince(long tick)
    {
        HungerZeroSinceTick = tick;
        TouchCanonical();
    }

    public void ClearHungerZeroSince()
    {
        HungerZeroSinceTick = null;
        TouchCanonical();
    }

    /// <summary>NEEDS-05: objetivo ativo e inspecionável — necessidade zerada dispara sempre
    /// (NEEDS-02, independente do limiar configurado); necessidade cujo déficit (100 − valor)
    /// supera <see cref="NeedsRules.UrgencyThreshold"/> também é urgente.</summary>
    public bool HasUrgentNeed(NeedsRules rules, long tick = 0) =>
        IsUrgent(HungerAt(tick), rules) || IsUrgent(ThirstAt(tick), rules)
        || IsUrgent(SleepAt(tick), rules) || IsUrgent(SocialAt(tick), rules);

    private static bool IsUrgent(int need, NeedsRules rules) => need == 0 || 100 - need > rules.UrgencyThreshold;

    public void BecomePregnant(WorldDate dueDate)
    {
        PregnantUntil = dueDate;
        TouchCanonical();
    }

    public void ClearPregnancy()
    {
        PregnantUntil = null;
        TouchCanonical();
    }

    public void CreditWallet(Money amount)
    {
        Wallet += amount;
        TouchCanonical();
    }

    /// <summary>Delega a <see cref="Money.TryDebit"/> — nunca deixa <see cref="Wallet"/>
    /// negativo (mesma garantia usada por <see cref="Workplace.TryDebitTreasury"/>).</summary>
    public Result<Money> TryDebitWallet(Money amount)
    {
        var result = Wallet.TryDebit(amount);
        if (result.IsSuccess)
        {
            Wallet = result.Value;
            TouchCanonical();
        }
        return result;
    }

    /// <summary>Vínculo empregatício (ECON-18) — espelha <see cref="JoinHousehold"/>.</summary>
    public void Hire(WorkplaceId workplace)
    {
        Employer = workplace;
        TouchCanonical();
    }

    /// <summary>Desliga o vínculo (ECON-19) — espelha <see cref="LeaveHousehold"/>.</summary>
    public void Fire()
    {
        Employer = null;
        TouchCanonical();
    }

    /// <summary>Troca de profissão (SKILL-14) — muda apenas <see cref="Profession"/>; nunca
    /// toca <see cref="Skills"/>. Estagnação da habilidade antiga é ausência de ganho (ela só
    /// para de subir por prática porque deixa de ser a profissão corrente), não um reset nem um
    /// campo novo de "profissão antiga" (Tech Decision do design).</summary>
    public void SwitchProfession(ProfessionType newProfession)
    {
        Profession = newProfession;
        TouchCanonical();
    }

    /// <summary>Aplica ganho de habilidade (SKILL-01/03..08) — delega o clamp de teto a <see
    /// cref="SkillSet.WithGain"/>; único ponto que reatribui <see cref="Skills"/> fora do
    /// construtor (mesmo padrão de mutador dedicado de <see cref="SetHunger"/> etc).</summary>
    public void GainSkill(SkillType type, double delta, double cap)
    {
        Skills = Skills.WithGain(type, delta, cap);
        TouchCanonical();
    }

    /// <summary>Vínculo de tutoria mestre->aprendiz (SKILL-08) — espelha <see cref="JoinHousehold"/>.</summary>
    public void AssignMentor(NpcId mentor)
    {
        Mentor = mentor;
        TouchCanonical();
    }

    /// <summary>Encerra o vínculo de tutoria — espelha <see cref="LeaveHousehold"/> (Edge Case:
    /// mestre morto no meio da tutoria, sem ponteiro solto).</summary>
    public void ClearMentor()
    {
        Mentor = null;
        TouchCanonical();
    }

    /// <summary>Casamento (Fase 7, T7, FAM-12) — seta só o próprio lado do vínculo; quem chama
    /// (<c>MarriageSystem.Marry</c>) é responsável por chamar duas vezes, uma para cada cônjuge.
    /// Nunca há mutador de "divorciar" (AD-060): viuvez é lida (<see cref="Spouse"/> aponta a
    /// alguém com <c>IsAlive == false</c>), nunca limpa automaticamente — mesmo espírito de
    /// <see cref="MotherId"/>/<see cref="FatherId"/> (AD-031, referência histórica válida).</summary>
    public void Marry(NpcId spouse)
    {
        Spouse = spouse;
        TouchCanonical();
    }

    /// <summary>Início de cortejo (Fase 7, T7) — espelha <see cref="AssignMentor"/>.</summary>
    public void StartCourtship(NpcId partner)
    {
        CourtingWith = partner;
        TouchCanonical();
    }

    /// <summary>Fim de cortejo, com ou sem sucesso — espelha <see cref="ClearMentor"/>.</summary>
    public void EndCourtship()
    {
        CourtingWith = null;
        TouchCanonical();
    }

    // SPEC_DEVIATION: design.md fala em JoinCity/LeaveCity espelhando JoinHousehold/LeaveHousehold.
    // Household mantém uma lista de membros (RemoveMember tem o que limpar); City não guarda
    // nenhuma lista de NPCs — a população é sempre derivada filtrando WorldState.Npcs por City
    // (CityPopulationQuery, T8). Não há estado de "saída" para limpar, então um único mutador
    // basta: MigrationSystem chama JoinCity(destino) no mesmo tick em que decide migrar (CITY-07),
    // nunca deixando o NPC num tick intermediário sem cidade (CityId nunca é nulo).

    /// <summary>Muda a cidade do NPC (Fase 8, T4, CITY-01/CITY-07) — espelha <see cref="JoinHousehold"/>.</summary>
    public void JoinCity(CityId city)
    {
        City = city;
        TouchCanonical();
    }

    /// <summary>Registra o tick de materialização (Fase 8, T9, CITY-05) — só
    /// <c>MaterializationSystem</c> chama, na criação a partir do pool agregado.</summary>
    public void MarkMaterialized(long tick)
    {
        MaterializedAtTick = tick;
        TouchCanonical();
    }

    /// <summary>Entra num prédio (Fase 15.1, T47) — nunca toca <see cref="CurrentLocation"/>/<see
    /// cref="City"/> (localização global preservada). Chamável de fora ou de dentro de outro
    /// prédio (troca direta, sem passar por <see cref="ExitBuilding"/> — mesmo espírito de <see
    /// cref="JoinCity"/>, nunca um tick intermediário "sem escopo").</summary>
    public void EnterBuilding(BuildingId building, FloorLevel floor, CellCoord localCell)
    {
        Interior = new InteriorOccupancy(building, floor, localCell);
        TouchCanonical();
    }

    /// <summary>Move dentro do mesmo prédio, mesmo andar (Fase 15.1, T47) — exige estar dentro
    /// de um prédio (exclusividade de escopo: não existe "mover" sem "estar dentro").</summary>
    public void MoveWithinBuilding(CellCoord localCell)
    {
        if (Interior is not { } current)
            throw new InvalidOperationException($"NPC {Id} não está dentro de um prédio");
        Interior = current with { LocalCell = localCell };
        TouchCanonical();
    }

    /// <summary>Troca de andar dentro do mesmo prédio (Fase 15.1, T47) — exige estar dentro de
    /// um prédio; navegação em si é <see cref="FloorNavigator"/> (reversível), aqui só grava o
    /// resultado.</summary>
    public void ChangeFloor(FloorLevel floor, CellCoord localCell)
    {
        if (Interior is not { } current)
            throw new InvalidOperationException($"NPC {Id} não está dentro de um prédio");
        Interior = current with { Floor = floor, LocalCell = localCell };
        TouchCanonical();
    }

    /// <summary>Sai do prédio, volta a escopo World/City (Fase 15.1, T47) — <see
    /// cref="CurrentLocation"/>/<see cref="City"/> continuam intocados: nunca dependeram do
    /// interior pra existir.</summary>
    public void ExitBuilding()
    {
        Interior = null;
        TouchCanonical();
    }
}
