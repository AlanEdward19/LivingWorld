using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Escolhe a ação do tick de cada NPC vivo (Fase 4 — NEEDS-05..16): rotina diária por
/// padrão, utility AI só quando alguma necessidade supera o limiar de urgência do cenário, com
/// bônus de continuidade (histerese), conclusão de ação ao atingir a duração máxima declarada, e
/// deslocamento com custo real quando a ação vencedora exige um local diferente do atual (hoje,
/// só <c>Sleep</c> tem exigência de local — dormir em <see cref="Household"/>.Location; sem-teto
/// dorme onde está, NEEDS-15).</summary>
public sealed class BehaviorDecisionSystem : ISimulationSystem
{
    public const string SystemName = "behavior-decision";

    /// <summary>Utilidade base de ações não ligadas a uma das 4 necessidades modeladas (Work,
    /// Travel, Idle) — parte do modelo de decisão em si (mesmo status que a fórmula de
    /// <see cref="PersonalityWeighting"/>: constante de algoritmo, não dado de cenário). Mesma
    /// escala 0-100 do déficit de necessidade, para que personalidade dispute em pé de igualdade
    /// quando nada mais está em jogo.</summary>
    private const double NonNeedBaselineUtility = 50.0;

    /// <summary>Cache de <c>Enum.GetValues&lt;ActionType&gt;()</c> — o método aloca um array novo
    /// a cada chamada (cópia defensiva do cache interno do runtime); chamado por NPC por tick
    /// Hourly, isso era a maior fonte de alocação do sistema em população grande. Mesma ordem
    /// ascendente do valor binário (Eat=0..Idle=5) que o desempate por <c>ActionId</c> exige.</summary>
    private static readonly ActionType[] AllActions = Enum.GetValues<ActionType>();

    /// <summary>Fase 6, T11 (SKILL-13/14) — <c>null</c> (default) desliga a escolha/troca de
    /// profissão inteira, preservando o comportamento da Fase 4 byte-a-byte (nenhum cenário
    /// existente injeta habilidade ainda, wiring fica pra T12, mesmo padrão de <see
    /// cref="Economy.ProductionSystem"/>).</summary>
    private readonly SkillsRules? _skillsRules;

    public BehaviorDecisionSystem(SkillsRules? skillsRules = null) => _skillsRules = skillsRules;

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        var rules = world.NeedsRules;
        var catalog = world.ActionCatalog;
        long now = ctx.CurrentTick;

        foreach (var npc in world.Npcs)
        {
            if (!npc.IsAlive) continue;

            EvaluateProfessionSwitch(world, npc);

            bool justCompleted = TryCompleteAction(world, npc, rules, catalog, now);
            var continuityAction = justCompleted ? null : npc.CurrentAction;

            var stage = world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate));
            var routineAction = catalog.RoutineOf(NoneIfSentinel(npc.Profession), stage, world.CurrentDate.Hour);

            var candidate = npc.HasUrgentNeed(rules)
                ? SelectByUtility(world, npc, rules, continuityAction)
                : routineAction;

            // NEEDS-14: ação vencedora que exige outro local (hoje, só Sleep) vira Travel até lá;
            // reavaliado em loop com teto MaxActionSelectionSteps (NEEDS-09).
            var chosen = ResolveWithStepCap(npc.Id.Value, candidate, c => RefineForLocation(world, npc, c), rules.MaxActionSelectionSteps);

            if (justCompleted || chosen != npc.CurrentAction)
                npc.SetCurrentAction(chosen, now);
        }
    }

    /// <summary>NEEDS-13/14: ao atingir a duração máxima declarada (<see cref="ActionCatalog.MaxDurationHours"/>)
    /// aplica o efeito da ação e libera o NPC pra nova seleção. <c>Travel</c> rumo ao local de
    /// sono (<see cref="RefineForLocation"/>) é exceção: sua duração não é a do catálogo, é
    /// <see cref="TravelResolution.TicksBetween"/> até o destino — chegar move o NPC
    /// (<see cref="Npc.MoveTo"/>) e libera pra nova seleção, sem aplicar efeito de Sleep no tick
    /// de chegada (NEEDS-14: ação de destino nunca executa no tick em que decidiu ir).</summary>
    private static bool TryCompleteAction(WorldState world, Npc npc, NeedsRules rules, ActionCatalog catalog, long now)
    {
        if (npc.CurrentAction is not { } action) return false;

        if (action == ActionType.Travel && TravelDestinationOf(world, npc) is { } destination && destination != npc.CurrentLocation)
        {
            long ticksNeeded = TravelResolution.TicksBetween(world.Map, npc.CurrentLocation, destination);
            if (now - npc.ActionStartedAtTick < ticksNeeded) return false; // ainda em trânsito

            npc.MoveTo(destination, now);
            return true;
        }

        if (now - npc.ActionStartedAtTick < catalog.MaxDurationHours[action]) return false;

        ApplyActionEffect(world, npc, rules, action);
        return true;
    }

    /// <summary>Eat retira comida/água do estoque do <see cref="Household"/> antes de restaurar
    /// (Fase 5, T18/ECON-16/17) — sem economia habilitada (<see cref="EconomyRules.Enabled"/>
    /// falso, comportamento da Fase 4 preservado) ou sem <see cref="Household"/>/estoque
    /// suficiente, a necessidade correspondente não é saciada, sem exceção. Socialize restaura o
    /// mínimo social. Sleep restaura sono pra 100, ou, sem-teto (<see cref="Npc.HomelessSince"/>
    /// não nulo), pra <see cref="NeedsRules.HomelessSleepEfficiency"/> × 100 (NEEDS-15) — nunca
    /// lança exceção por falta de residência. Work/Travel/Idle/Buy não restauram necessidade
    /// nenhuma, só concluem e liberam o NPC.</summary>
    private static void ApplyActionEffect(WorldState world, Npc npc, NeedsRules rules, ActionType action)
    {
        switch (action)
        {
            case ActionType.Eat:
                ApplyEat(world, npc);
                break;
            case ActionType.Sleep:
                npc.SetSleep(npc.HomelessSince is null ? 100 : (int)(100 * rules.HomelessSleepEfficiency));
                break;
            case ActionType.Socialize:
                npc.SetSocial(100);
                break;
            case ActionType.Buy:
                ApplyBuy(world, npc);
                break;
        }
    }

    /// <summary>ECON-16/17: sem economia habilitada, restaura ambas (comportamento da Fase 4,
    /// sem estoque nenhum envolvido). Com economia, cada necessidade só é saciada se o
    /// <see cref="Household"/> tiver o recurso correspondente — sem residência (sem-teto) ou sem
    /// estoque, a necessidade fica como estava, sem exceção.</summary>
    private static void ApplyEat(WorldState world, Npc npc)
    {
        var econ = world.EconomyRules;
        if (!econ.Enabled)
        {
            npc.SetHunger(100);
            npc.SetThirst(100);
            return;
        }

        if (npc.Household is not { } householdId || world.FindHousehold(householdId) is not { } household) return;

        var food = new ResourceType(econ.FoodResourceId);
        if (household.Withdraw(food, 1).IsSuccess)
        {
            npc.SetHunger(100);
            world.RecordResourceConsumed(food, 1); // ECON-15: destruição real (Buy só transfere estoque, não conta aqui)
        }

        var water = new ResourceType(econ.WaterResourceId);
        if (household.Withdraw(water, 1).IsSuccess)
        {
            npc.SetThirst(100);
            world.RecordResourceConsumed(water, 1);
        }
    }

    /// <summary>Fase 5, T19 (ECON-09/16/17): compra 1 unidade de comida e 1 de água do mercado
    /// mais próximo — computa cada transação sobre <see cref="TransactionContext"/> imutável
    /// (design.md Tech Decisions) e só então comita nos objetos reais (<see cref="Npc.Wallet"/>,
    /// <see cref="Workplace.Treasury"/>/estoque, <see cref="Household"/>.Stock), na mesma
    /// passada que <see cref="MarketTransaction.Execute"/> já validou. Falha de saldo/estoque em
    /// qualquer um dos dois recursos não afeta o outro — cada compra é sua própria transação.</summary>
    private static void ApplyBuy(WorldState world, Npc npc)
    {
        if (!world.EconomyRules.Enabled) return;
        var market = NearestMarket(world, npc.CurrentLocation);
        if (market is null) return;
        if (npc.Household is not { } householdId || world.FindHousehold(householdId) is not { } household) return;

        var econ = world.EconomyRules;
        foreach (var resourceId in new[] { econ.FoodResourceId, econ.WaterResourceId })
        {
            var resource = new ResourceType(resourceId);
            if (!market.Prices.TryGetValue(resource, out var unitPrice) || unitPrice <= 0) continue;

            // Compra em lote (não 1 unidade por vez) — uma viagem ao mercado abastece a
            // despensa por vários dias, não só a próxima refeição, reduzindo a frequência de
            // viagens que a população inteira precisaria fazer pra se sustentar.
            const long quantity = 10;
            var ctx = new TransactionContext(
                npc.Wallet, market.Treasury, market.Stock.GetValueOrDefault(resource),
                household.Stock.GetValueOrDefault(resource), resource, unitPrice, quantity);

            if (!MarketTransaction.Execute(ctx).IsSuccess) continue; // saldo ou estoque insuficiente — sem efeito

            var price = new Money(unitPrice * quantity);
            npc.TryDebitWallet(price);
            market.Withdraw(resource, quantity);
            market.CreditTreasury(price);
            household.Deposit(resource, quantity);
        }
    }

    /// <summary>NEEDS-14 (Sleep) + Fase 5 T19 (Buy): se a ação candidata exige outro local (Sleep
    /// → residência; Buy → mercado mais próximo), a ação efetiva do tick vira <c>Travel</c> —
    /// sem-teto/sem mercado (destino <c>null</c>) nunca precisa viajar.</summary>
    private static ActionType RefineForLocation(WorldState world, Npc npc, ActionType candidate) => candidate switch
    {
        ActionType.Sleep when SleepDestinationOf(world, npc) is { } dest && dest != npc.CurrentLocation => ActionType.Travel,
        ActionType.Buy when BuyDestinationOf(world, npc) is { } dest && dest != npc.CurrentLocation => ActionType.Travel,
        _ => candidate,
    };

    /// <summary>Local onde o NPC dorme de fato: <see cref="Household"/>.Location se tiver
    /// residência; <c>null</c> se sem-teto (dorme onde está, sem viagem — Tech Decision:
    /// Residence reusa <see cref="Npc.Household"/>, nenhum campo novo).</summary>
    private static CellCoord? SleepDestinationOf(WorldState world, Npc npc) =>
        npc.Household is { } householdId ? world.FindHousehold(householdId)?.Location : null;

    /// <summary>Local do mercado (<see cref="Workplace"/> com <see cref="LocationType"/> em
    /// <see cref="EconomyCatalog.MarketLocationTypeIds"/>) mais próximo do NPC; <c>null</c> se
    /// nenhum mercado existe no cenário.</summary>
    private static CellCoord? BuyDestinationOf(WorldState world, Npc npc) => NearestMarket(world, npc.CurrentLocation)?.Location;

    /// <summary>SPEC_DEVIATION: durante o trânsito (<c>ActionType.Travel</c> em andamento),
    /// <see cref="TryCompleteAction"/> só sabe re-derivar o destino a partir do estado atual do
    /// NPC (não guarda "por que" começou a viajar) — checa Sleep primeiro, depois Buy. As duas
    /// únicas ações com exigência de local nesta fase raramente coincidem no mesmo NPC no mesmo
    /// tick (uma é rotina noturna, a outra é urgência de fome/sede); se um dia coincidirem, Sleep
    /// tem prioridade — mesmo compromisso que o design.md aceitou pra não introduzir um campo
    /// novo só pra lembrar o propósito da viagem.</summary>
    private static CellCoord? TravelDestinationOf(WorldState world, Npc npc) =>
        SleepDestinationOf(world, npc) is { } sleepDest && sleepDest != npc.CurrentLocation
            ? sleepDest
            : BuyDestinationOf(world, npc);

    private static Workplace? NearestMarket(WorldState world, CellCoord from) =>
        world.Workplaces
            .Where(w => world.EconomyCatalog.MarketLocationTypeIds.Contains(w.LocationType.Id))
            .OrderBy(w => TravelResolution.TicksBetween(world.Map, from, w.Location))
            .ThenBy(w => w.Id.Value)
            .FirstOrDefault();

    /// <summary>Ordena as 6 ações candidatas por nota (<c>utilidadeBase × pesoPersonalidade</c>,
    /// NEEDS-06); empate exato desempata por menor <c>ActionId</c> — <c>Enum.GetValues</c> já
    /// devolve os valores em ordem ascendente do valor binário subjacente (Eat=0..Idle=5), então
    /// a primeira ocorrência de uma nota máxima é sempre a de menor id. NEEDS-12 (histerese): a
    /// ação corrente ganha <see cref="NeedsRules.ContinuityBonus"/> antes da comparação quando
    /// <see cref="NeedsRules.HysteresisEnabled"/> — só troca se algum desafiante superar essa
    /// nota efetiva.</summary>
    private static ActionType SelectByUtility(WorldState world, Npc npc, NeedsRules rules, ActionType? continuityAction)
    {
        var best = ActionType.Eat;
        double bestScore = double.NegativeInfinity;

        foreach (var action in AllActions)
        {
            double score = UtilityBaseOf(world, action, npc) * PersonalityWeighting.WeightOf(npc.Personality, action);
            if (rules.HysteresisEnabled && continuityAction == action)
                score += rules.ContinuityBonus;

            if (score > bestScore)
            {
                bestScore = score;
                best = action;
            }
        }

        return best;
    }

    /// <summary>Eat cobre fome e sede (a fase não tem ação de "beber" dedicada — catálogo
    /// fechado em 6 ações — então o déficit mais urgente entre as duas rege a nota de Eat).
    /// Work/Travel/Idle usam a mesma utilidade base fixa (<see cref="NonNeedBaselineUtility"/>):
    /// nenhuma necessidade modelada os direciona, só personalidade os distingue entre si. Buy
    /// (Fase 5, T19) cresce com o déficit de fome/sede do próprio NPC, mas só quando o
    /// <see cref="Household"/> de fato não tem o recurso em estoque — comprar sem necessidade real
    /// (despensa cheia) não é mais atrativo que a rotina.</summary>
    private static double UtilityBaseOf(WorldState world, ActionType action, Npc npc) => action switch
    {
        ActionType.Eat => EatUtilityOf(world, npc),
        ActionType.Sleep => Deficit(npc.Sleep),
        ActionType.Socialize => Deficit(npc.Social),
        ActionType.Buy => BuyUtilityOf(world, npc),
        ActionType.Work or ActionType.Travel or ActionType.Idle => NonNeedBaselineUtility,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "ActionType desconhecido"),
    };

    /// <summary>Sem economia habilitada (ou sem <see cref="Household"/>, sem-teto), Eat sempre
    /// compete pelo déficit puro — comportamento da Fase 4. Com economia e despensa vazia dos
    /// dois recursos, Eat não teria nada pra restaurar mesmo vencendo a disputa — cai pro
    /// baseline reduzido, senão empataria sempre com <see cref="BuyUtilityOf"/> (mesma fórmula) e
    /// venceria por ser o primeiro valor do enum (<see cref="SelectByUtility"/> só troca em
    /// empate estrito), deixando o NPC preso "tentando comer" pra sempre sem nunca ir comprar.</summary>
    private static double EatUtilityOf(WorldState world, Npc npc)
    {
        double deficit = Math.Max(Deficit(npc.Hunger), Deficit(npc.Thirst));
        if (!world.EconomyRules.Enabled) return deficit;
        if (npc.Household is not { } householdId || world.FindHousehold(householdId) is not { } household) return deficit;

        var econ = world.EconomyRules;
        bool hasFood = household.Stock.GetValueOrDefault(new ResourceType(econ.FoodResourceId)) >= 1;
        bool hasWater = household.Stock.GetValueOrDefault(new ResourceType(econ.WaterResourceId)) >= 1;
        return hasFood || hasWater ? deficit : NonNeedBaselineUtility / 2;
    }

    private static double BuyUtilityOf(WorldState world, Npc npc)
    {
        if (!world.EconomyRules.Enabled) return NonNeedBaselineUtility;
        if (npc.Household is not { } householdId || world.FindHousehold(householdId) is not { } household)
            return NonNeedBaselineUtility; // sem-teto: sem despensa pra repor, T19 não cobre compra sem-teto

        var econ = world.EconomyRules;
        bool needsFood = household.Stock.GetValueOrDefault(new ResourceType(econ.FoodResourceId)) < 1;
        bool needsWater = household.Stock.GetValueOrDefault(new ResourceType(econ.WaterResourceId)) < 1;
        if (!needsFood && !needsWater) return NonNeedBaselineUtility;

        return Math.Max(needsFood ? Deficit(npc.Hunger) : 0, needsWater ? Deficit(npc.Thirst) : 0);
    }

    private static int Deficit(int need) => 100 - need;

    /// <summary><see cref="ProfessionType.None"/> (catálogo vazio no sorteio, task 7) não é uma
    /// profissão de rotina válida — resolve como "any" (<c>null</c>), mesma convenção de
    /// <see cref="ActionCatalog.RoutineOf"/> para slot sem profissão específica.</summary>
    private static ProfessionType? NoneIfSentinel(ProfessionType profession) =>
        profession == ProfessionType.None ? null : profession;

    /// <summary>SKILL-13/14: pontua candidatas a profissão (domínio: as profissões com
    /// <see cref="Workplace"/> declarado, <see cref="EconomyCatalog.LocationTypeByProfession"/> —
    /// mesmo universo que <see cref="EmploymentSystem"/> de fato contrata) por habilidade atual
    /// do NPC na profissão candidata, traço de personalidade (mesmo padrão de <see
    /// cref="PersonalityWeighting"/>) e vagas abertas — todos multiplicativos, nenhum trava
    /// (design.md). Troca só ocorre pra Adulto e quando a candidata vencedora difere da
    /// profissão corrente; <see cref="Npc.SwitchProfession"/> preserva a habilidade antiga por
    /// conta própria (T7), este método não zera nada.</summary>
    private void EvaluateProfessionSwitch(WorldState world, Npc npc)
    {
        if (_skillsRules is not { } rules) return;
        if (world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) != LifeStage.Adult) return;

        var catalog = world.EconomyCatalog;
        if (catalog.LocationTypeByProfession.Count == 0) return;

        ProfessionType? best = null;
        double bestScore = double.NegativeInfinity;
        foreach (var professionId in catalog.LocationTypeByProfession.Keys.OrderBy(id => id))
        {
            double score = ProfessionScoreOf(world, npc, rules, catalog, professionId);
            if (score > bestScore)
            {
                bestScore = score;
                best = new ProfessionType(professionId);
            }
        }

        if (best is { } candidate && candidate != npc.Profession)
            npc.SwitchProfession(candidate);
    }

    private static double ProfessionScoreOf(
        WorldState world, Npc npc, SkillsRules rules, EconomyCatalog catalog, int professionId)
    {
        double skillWeight = rules.SkillByProfession.TryGetValue(professionId, out var skillType)
            ? 1.0 + npc.Skills.Get(skillType) / rules.Cap
            : 1.0;
        double personalityWeight = PersonalityWeighting.WeightOf(npc.Personality, ActionType.Work);
        double vacancyWeight = VacancyWeightOf(world, catalog, professionId);

        return skillWeight * personalityWeight * vacancyWeight;
    }

    /// <summary>1.0 (neutro) sem nenhum <see cref="Workplace"/> declarado pra esta profissão;
    /// senão <c>1 + vagasAbertas/vagasTotais</c> — mais vaga livre, mais atrativa (nunca um
    /// trava: profissão lotada ainda pontua 1.0, nunca menos).</summary>
    private static double VacancyWeightOf(WorldState world, EconomyCatalog catalog, int professionId)
    {
        if (!catalog.LocationTypeByProfession.TryGetValue(professionId, out var locationTypeId)) return 1.0;

        var workplaces = world.Workplaces.Where(w => w.LocationType.Id == locationTypeId).ToList();
        int totalSlots = workplaces.Sum(w => w.MaxVacancies);
        if (totalSlots == 0) return 1.0;

        int openSlots = workplaces.Sum(w => w.MaxVacancies - w.Employees.Count);
        return 1.0 + (double)openSlots / totalSlots;
    }

    /// <summary>NEEDS-09: reavalia <paramref name="initial"/> por <paramref name="refine"/> até
    /// convergir (mesmo valor duas vezes seguidas) ou estourar <paramref name="maxSteps"/> —
    /// mesmo padrão de teto do <see cref="WorldClock"/> (rules/simulation-determinism.md).
    /// Estourar aborta nomeando o NPC e as ações que ficaram empatadas em ciclo, reusando
    /// <see cref="TickBudgetExceededException"/> (mesma convenção de nomear o culpado, aqui o
    /// NPC em vez do sistema).</summary>
    internal static ActionType ResolveWithStepCap(long npcId, ActionType initial, Func<ActionType, ActionType> refine, int maxSteps)
    {
        var current = initial;
        for (int step = 0; step < maxSteps; step++)
        {
            var next = refine(current);
            if (next == current) return current;
            current = next;
        }

        var final = refine(current);
        throw new TickBudgetExceededException(
            $"npc {npcId}: seleção de ação não convergiu em {maxSteps} passos — ações empatadas em ciclo ({current} <-> {final})",
            maxSteps);
    }
}
