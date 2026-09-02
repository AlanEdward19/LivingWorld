using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Production;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Skills;

/// <summary>As 5 fontes "sociais" de ganho de habilidade (Fase 6, T9, SKILL-04..08): treino
/// deliberado, escola, aprendizado parental, observação, tutoria mestre-&gt;aprendiz. Um único
/// <c>Tick</c> com 5 métodos privados coesos — design.md justifica não splitar em 5 classes
/// (multiplicaria a passada Daily sobre a população sem ganho real). <c>Daily</c>, mesma posição
/// de <see cref="SkillPracticeSystem"/> na ordem (roda logo depois, ainda antes de <see
/// cref="ProductionSystem"/>).
///
/// SPEC_DEVIATION: nem spec.md nem design.md fixam o gatilho exato de DeliberateTraining/School
/// — Fase 6 explicitamente não introduz <see cref="ActionType"/> novo nem prédio de escola (Out
/// of Scope). Gatilho mínimo e determinístico escolhido aqui: DeliberateTraining = adulto em
/// tempo livre (<see cref="ActionType.Idle"/>) com saldo suficiente pro custo nominal (paga pela
/// própria profissão); School = criança com profissão mapeada, sem custo nem teto de vaga (Out
/// of Scope já cobre "sem prédio nem capacidade" nesta fase).</summary>
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
    public TickFrequency Frequency => TickFrequency.Daily;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!_rules.Enabled) return; // SKILL-01: flag de teste desliga o sistema (T19)

        var living = world.Npcs.Where(n => n.IsAlive).OrderBy(n => n.Id.Value).ToList();

        foreach (var npc in living)
        {
            GainFromDeliberateTraining(world, npc);
            GainFromSchool(world, npc);
            GainFromParental(world, npc);
            GainFromObservation(world, living, npc);
            GainFromTutoring(world, npc);
        }
    }

    private LifeStage StageOf(WorldState world, Npc npc) => _lifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate));

    /// <summary>SKILL-04: adulto dedica tempo livre (Idle) e dinheiro (saldo debitado) pra
    /// treinar a habilidade da própria profissão, taxa própria de <see
    /// cref="SkillGainSource.DeliberateTraining"/>. Sem saldo ou sem profissão mapeada —
    /// sem-op.</summary>
    private void GainFromDeliberateTraining(WorldState world, Npc npc)
    {
        if (StageOf(world, npc) != LifeStage.Adult) return;
        if (npc.CurrentAction != ActionType.Idle) return;
        if (!_rules.SkillByProfession.TryGetValue(npc.Profession.Id, out var skillType)) return;
        // Sem professor/instituição modelada nesta fase (Assunção A5, deferida) — o custo do
        // treino vai pro Treasury do próprio empregador (sentido inverso de WagePaymentSystem:
        // lá Treasury paga Wallet, aqui Wallet paga Treasury), nunca cunha/destrói dinheiro
        // (ECON-26/27) e nunca some em silêncio. Sem emprego, sem pra onde o custo ir — sem-op,
        // mesmo padrão de "sem trabalhador presente, produção 0" do ProductionSystem.
        if (npc.Employer is not { } employerId) return;
        if (world.FindWorkplace(employerId) is not { } workplace) return;
        if (!npc.TryDebitWallet(new Money(DeliberateTrainingCost)).IsSuccess) return;
        workplace.CreditTreasury(new Money(DeliberateTrainingCost));

        double gain = _rules.Gain(npc.Skills.Get(skillType), SkillGainSource.DeliberateTraining, npc.RateGene.Value);
        npc.GainSkill(skillType, gain, _rules.Cap);
    }

    /// <summary>SKILL-05: criança com profissão mapeada ganha habilidade por escola, taxa
    /// própria de <see cref="SkillGainSource.School"/> — sem prédio nem vaga limitada (Out of
    /// Scope da fase), então o único requisito é a própria criança existir e ter mapeamento.</summary>
    private void GainFromSchool(WorldState world, Npc npc)
    {
        if (StageOf(world, npc) != LifeStage.Child) return;
        if (!_rules.SkillByProfession.TryGetValue(npc.Profession.Id, out var skillType)) return;

        double gain = _rules.Gain(npc.Skills.Get(skillType), SkillGainSource.School, npc.RateGene.Value);
        npc.GainSkill(skillType, gain, _rules.Cap);
    }

    /// <summary>SKILL-06: criança que convive (mesmo Household) com um dos pais empregado
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

    /// <summary>SKILL-07: NPC fisicamente próximo (mesmo <see cref="Npc.CurrentLocation"/>) de
    /// outro NPC trabalhando, que não seja o próprio mestre (isso é tutoria, não observação),
    /// ganha a habilidade que o observado pratica, taxa própria de <see
    /// cref="SkillGainSource.Observation"/> (menor que <see cref="SkillGainSource.Tutoring"/> por
    /// configuração de cenário). Ganha de no máximo um observado por tick (o primeiro em ordem
    /// determinística), não soma todos os presentes.
    /// <c>ponytail:</c> varredura O(n²) por local — aceitável na escala atual da população;
    /// revisitar (indexar por <see cref="Npc.CurrentLocation"/>) se a população crescer o
    /// bastante pra pesar no perfil.</summary>
    private void GainFromObservation(WorldState world, IReadOnlyList<Npc> living, Npc observer)
    {
        var observed = living.FirstOrDefault(other =>
            other.Id != observer.Id &&
            other.Id != observer.Mentor &&
            other.CurrentAction == ActionType.Work &&
            other.CurrentLocation == observer.CurrentLocation);
        if (observed is null) return;
        if (!_rules.SkillByProfession.TryGetValue(observed.Profession.Id, out var skillType)) return;

        double gain = _rules.Gain(observer.Skills.Get(skillType), SkillGainSource.Observation, observer.RateGene.Value);
        observer.GainSkill(skillType, gain, _rules.Cap);
    }

    /// <summary>SKILL-08: aprendiz (<see cref="Npc.Mentor"/> aponta pro mestre) ganha a
    /// habilidade que o mestre pratica; a taxa depende de <c>min(habilidade do mestre, cap)</c> e
    /// da habilidade declarada em <see cref="SkillsRules.TeachingSkill"/> do mestre — mestre
    /// melhor (nas duas dimensões) produz ganho maior, mesma seed (SKILL-16). Mestre morto ou
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
        double masterTeaching = master.Skills.Get(_rules.TeachingSkill);
        double masterFactor = (masterSkill / _rules.Cap) * (1.0 + masterTeaching / _rules.Cap);

        double baseGain = _rules.Gain(apprentice.Skills.Get(skillType), SkillGainSource.Tutoring, apprentice.RateGene.Value);
        apprentice.GainSkill(skillType, baseGain * masterFactor, _rules.Cap);
    }
}
