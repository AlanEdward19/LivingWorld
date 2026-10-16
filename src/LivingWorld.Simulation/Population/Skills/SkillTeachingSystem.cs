using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Economy.Production;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Skills;

/// <summary>As 5 fontes "sociais" de ganho de habilidade: treino
/// deliberado, escola, aprendizado parental, observação, tutoria mestre-&gt;aprendiz. Um único
/// <c>Tick</c> com 5 métodos privados coesos — design.md justifica não splitar em 5 classes
/// (multiplicaria a passada Daily sobre a população sem ganho real). <c>Daily</c>, mesma posição
/// de <see cref="SkillPracticeSystem"/> na ordem (roda logo depois, ainda antes de <see
/// cref="ProductionSystem"/>).
///
/// SPEC_DEVIATION: nem spec.md nem design.md fixam o gatilho exato de DeliberateTraining/School
/// — não há <see cref="ActionType"/> novo nem prédio de escola modelado (Out
/// of Scope). Gatilho mínimo e determinístico escolhido aqui: DeliberateTraining = adulto em
/// tempo livre (<see cref="ActionType.Idle"/>) com saldo suficiente pro custo nominal (paga pela
/// própria profissão); School = criança com profissão mapeada, sem custo nem teto de vaga (Out
/// of Scope já cobre "sem prédio nem capacidade").</summary>
[LivingCapability("SKILLS")]
[RunsAfter(typeof(EmploymentSystem))]
public sealed class SkillTeachingSystem : ISimulationSystem
{
    public const string SystemName = "population-skill-teaching";

    /// <summary>Custo nominal de treino deliberado — nenhum cenário desta fase declara um valor
    /// pra isso; constante de algoritmo mínima (mesmo espírito de <c>NonNeedBaselineUtility</c>
    /// em <see cref="BehaviorDecisionSystem"/>).</summary>
    private const long DeliberateTrainingCost = 1;

    private readonly SkillsRules _rules;
    private readonly LifeStageRules _lifeStageRules;

    public SkillTeachingSystem(SkillsRules rules, LifeStageRules lifeStageRules)
    {
        _rules = rules;
        _lifeStageRules = lifeStageRules;
    }

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Midshift;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!_rules.Enabled) return; // flag de teste desliga o sistema

        var living = world.Npcs.Where(n => n.IsAlive).OrderBy(n => n.Id.Value).ToList();
        AssignMentors(world, living);

        foreach (var npc in living)
        {
            GainFromDeliberateTraining(world, npc);
            GainFromSchool(world, npc);
            GainFromParental(world, npc);
            GainFromObservation(world, npc);
            GainFromTutoring(world, npc);
        }
    }

    /// <summary>Menor distância de habilidade (fração do teto) para um colega virar mestre de outro.</summary>
    private const double MentorMinGapOfCap = 0.15;

    /// <summary>Em cada oficina, o empregado mais habilidoso na própria profissão vira mestre dos colegas da
    /// mesma profissão que estão ao menos <see cref="MentorMinGapOfCap"/> do teto abaixo dele e ainda sem
    /// mestre. Uma passada pela população; só adultos empregados participam. Escolha determinística: maior
    /// habilidade, desempate pelo menor Id.</summary>
    private void AssignMentors(WorldState world, List<Npc> living)
    {
        Dictionary<(WorkplaceId, int), Npc>? masters = null;
        foreach (var npc in living)
        {
            if (npc.Employer is not { } employer || npc.Mentor is not null) continue;
            if (!_rules.SkillByProfession.TryGetValue(npc.Profession.Id, out var skill)) continue;
            if (StageOf(world, npc) != LifeStage.Adult) continue;
            masters ??= BestPerWorkplace(world, living);
            if (!masters.TryGetValue((employer, npc.Profession.Id), out var master) || master.Id == npc.Id) continue;
            if (master.Skills.Get(skill) - npc.Skills.Get(skill) < MentorMinGapOfCap * _rules.Cap) continue;
            npc.AssignMentor(master.Id);
        }
    }

    private Dictionary<(WorkplaceId, int), Npc> BestPerWorkplace(WorldState world, List<Npc> living)
    {
        var best = new Dictionary<(WorkplaceId, int), Npc>();
        foreach (var npc in living)
        {
            if (npc.Employer is not { } employer) continue;
            if (!_rules.SkillByProfession.TryGetValue(npc.Profession.Id, out var skill)) continue;
            if (StageOf(world, npc) != LifeStage.Adult) continue;
            var key = (employer, npc.Profession.Id);
            if (!best.TryGetValue(key, out var current) || npc.Skills.Get(skill) > current.Skills.Get(skill))
                best[key] = npc; // living vem ordenado por Id: empate fica com o menor Id
        }
        return best;
    }

    private LifeStage StageOf(WorldState world, Npc npc) => _lifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate));

    /// <summary>Adulto dedica tempo livre (Idle) e dinheiro (saldo debitado) pra
    /// treinar a habilidade da própria profissão, taxa própria de <see
    /// cref="SkillGainSource.DeliberateTraining"/>. Sem saldo ou sem profissão mapeada —
    /// sem-op.</summary>
    private void GainFromDeliberateTraining(WorldState world, Npc npc)
    {
        if (StageOf(world, npc) != LifeStage.Adult) return;
        if (npc.CurrentAction != ActionType.Idle) return;
        if (!_rules.SkillByProfession.TryGetValue(npc.Profession.Id, out var skillType)) return;
        // Sem professor/instituição modelada (deferido) — o custo do
        // treino vai pro Treasury do próprio empregador (sentido inverso de WagePaymentSystem:
        // lá Treasury paga Wallet, aqui Wallet paga Treasury), nunca cunha/destrói dinheiro
        // e nunca some em silêncio. Sem emprego, sem pra onde o custo ir — sem-op,
        // mesmo padrão de "sem trabalhador presente, produção 0" do ProductionSystem.
        if (npc.Employer is not { } employerId) return;
        if (world.FindWorkplace(employerId) is not { } workplace) return;
        if (!npc.TryDebitWallet(new Money(DeliberateTrainingCost)).IsSuccess) return;
        workplace.CreditTreasury(new Money(DeliberateTrainingCost));

        double gain = _rules.Gain(npc.Skills.Get(skillType), SkillGainSource.DeliberateTraining, npc.RateGene.Value);
        npc.GainSkill(skillType, gain, _rules.Cap);
    }

    /// <summary>Criança com profissão mapeada ganha habilidade por escola, taxa
    /// própria de <see cref="SkillGainSource.School"/> — sem prédio nem vaga limitada (Out of
    /// Scope), então o único requisito é a própria criança existir e ter mapeamento.</summary>
    private void GainFromSchool(WorldState world, Npc npc)
    {
        if (StageOf(world, npc) != LifeStage.Child) return;
        if (!_rules.SkillByProfession.TryGetValue(npc.Profession.Id, out var skillType)) return;

        double gain = _rules.Gain(npc.Skills.Get(skillType), SkillGainSource.School, npc.RateGene.Value);
        npc.GainSkill(skillType, gain, _rules.Cap);
    }

    /// <summary>Criança que convive (mesmo Household) com um dos pais empregado
    /// (pratica a profissão de fato) ganha a habilidade correspondente, taxa própria de <see
    /// cref="SkillGainSource.Parental"/>. Mãe e pai são checados independentemente — os dois
    /// podem contribuir no mesmo tick, mesmo requisito ("um dos pais") não exclui o outro.</summary>
    private void GainFromParental(WorldState world, Npc npc)
    {
        if (StageOf(world, npc) != LifeStage.Child) return;
        if (npc.Household is not { } childHousehold) return;

        GainFromParent(world, npc, npc.MotherId, childHousehold);
        GainFromParent(world, npc, npc.FatherId, childHousehold);
    }

    private void GainFromParent(WorldState world, Npc child, NpcId? parentId, HouseholdId childHousehold)
    {
        if (parentId is not { } id) return;
        if (world.FindNpc(id) is not { IsAlive: true } parent) return;
        if (parent.Household != childHousehold) return; // "convive" — mesmo household
        if (parent.Employer is null) return; // "pratica uma profissão" — precisa estar de fato empregado
        if (!_rules.SkillByProfession.TryGetValue(parent.Profession.Id, out var skillType)) return;

        double gain = _rules.Gain(child.Skills.Get(skillType), SkillGainSource.Parental, child.RateGene.Value);
        child.GainSkill(skillType, gain, _rules.Cap);
    }

    /// <summary>NPC fisicamente próximo (mesmo <see cref="Npc.CurrentLocation"/>) de
    /// outro NPC trabalhando, que não seja o próprio mestre (isso é tutoria, não observação),
    /// ganha a habilidade que o observado pratica, taxa própria de <see
    /// cref="SkillGainSource.Observation"/> (menor que <see cref="SkillGainSource.Tutoring"/> por
    /// configuração de cenário). Ganha de no máximo um observado por tick (o primeiro em ordem
    /// determinística), não soma todos os presentes.
    /// <c>ponytail:</c> varredura O(n²) por local — aceitável na escala atual da população;
    /// revisitar (indexar por <see cref="Npc.CurrentLocation"/>) se a população crescer o
    /// bastante pra pesar no perfil.</summary>
    private void GainFromObservation(WorldState world, Npc observer)
    {
        // Só quem está na mesma célula importa: o índice espacial evita varrer a população inteira por
        // observador. O primeiro por Id (a ordem original da varredura) é o de menor Id entre os candidatos.
        Npc? observed = null;
        foreach (var other in world.AliveNpcIndex.At(observer.CurrentLocation))
        {
            if (other.Id == observer.Id || other.Id == observer.Mentor || other.CurrentAction != ActionType.Work)
                continue;
            if (observed is null || other.Id.Value < observed.Id.Value)
                observed = other;
        }

        if (observed is null) return;
        if (!_rules.SkillByProfession.TryGetValue(observed.Profession.Id, out var skillType)) return;

        double gain = _rules.Gain(observer.Skills.Get(skillType), SkillGainSource.Observation, observer.RateGene.Value);
        observer.GainSkill(skillType, gain, _rules.Cap);
    }

    /// <summary>Aprendiz (<see cref="Npc.Mentor"/> aponta pro mestre) ganha a
    /// habilidade que o mestre pratica; a taxa depende de <c>min(habilidade do mestre, cap)</c> e
    /// da habilidade declarada em <see cref="SkillsRules.TeachingSkill"/> do mestre — mestre
    /// melhor (nas duas dimensões) produz ganho maior, mesma seed. Mestre morto ou
    /// removido: <see cref="Npc.ClearMentor"/> é chamado no próprio tick, sem exceção (Edge Case
    /// da spec) e sem ganho nesse tick.</summary>
    private void GainFromTutoring(WorldState world, Npc apprentice)
    {
        if (apprentice.Mentor is not { } mentorId) return;

        var master = world.FindNpc(mentorId);
        if (master is not { IsAlive: true })
        {
            apprentice.ClearMentor();
            return;
        }

        if (!_rules.SkillByProfession.TryGetValue(master.Profession.Id, out var skillType)) return;

        double masterSkill = Math.Min(master.Skills.Get(skillType), _rules.Cap);
        if (apprentice.Skills.Get(skillType) >= masterSkill)
        {
            apprentice.ClearMentor(); // alcançou o mestre: formado
            return;
        }

        double masterTeaching = master.Skills.Get(_rules.TeachingSkill);
        double masterFactor = (masterSkill / _rules.Cap) * (1.0 + masterTeaching / _rules.Cap);

        double baseGain = _rules.Gain(apprentice.Skills.Get(skillType), SkillGainSource.Tutoring, apprentice.RateGene.Value);
        apprentice.GainSkill(skillType, baseGain * masterFactor, _rules.Cap);

        // Ensinar também treina o ensino: o mestre ganha em TeachingSkill pela taxa de prática.
        double teachGain = _rules.Gain(masterTeaching, SkillGainSource.Practice, master.RateGene.Value);
        master.GainSkill(_rules.TeachingSkill, teachGain, _rules.Cap);
    }
}
