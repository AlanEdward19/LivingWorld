using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Economy.Production;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Skills;

/// <summary>Aprendizado social e criação (AD-132), uma passada por dia sobre os vivos: treino deliberado, escola, o que a
/// criança aprende com os adultos da casa onde vive, observação e tutoria mestre-&gt;aprendiz, todas escaladas pela aptidão
/// da idade (<see cref="SkillLearningRules.Aptitude"/>: bebê não aprende ofício). A casa ensina até uma fração do que os
/// adultos dela sabem; do marco de aprendiz em diante o jovem ganha um mestre na cidade. Uma vez por mês por pessoa
/// (escalonado por id) a habilidade sem uso e a manual da velhice perdem um pouco, e a criança registra quem a está
/// criando; ao virar adulta, a personalidade recebe o ambiente compartilhado desses cuidadores (<see cref="RearingRules"/>).
/// <c>Midshift</c>, mesma posição de <see cref="SkillPracticeSystem"/> (antes de <see cref="ProductionSystem"/>).</summary>
[LivingCapability("SKILLS", Events = [LivingWorld.Domain.History.WorldEventKind.VocationChosen])]
[RunsAfter(typeof(EmploymentSystem))]
public sealed class SkillTeachingSystem : ISimulationSystem
{
    public const string SystemName = "population-skill-teaching";

    /// <summary>Custo nominal de treino deliberado — nenhum cenário declara um valor pra isso; constante de algoritmo mínima
    /// (mesmo espírito de <c>NonNeedBaselineUtility</c> em <see cref="BehaviorDecisionSystem"/>).</summary>
    private const long DeliberateTrainingCost = 1;

    /// <summary>Menor distância de habilidade (fração do teto) para alguém virar mestre de outro.</summary>
    private const double MentorMinGapOfCap = 0.15;

    private readonly SkillsRules _rules;
    private readonly LifeStageRules _lifeStageRules;
    private readonly RearingRules _rearing;
    private readonly SkillType[] _workSkills;
    private readonly List<Personality> _caregivers = [];
    private readonly List<Npc> _caregiverNpcs = [];

    /// <summary>Ganhos do dia da pessoa em processamento, aplicados numa única troca de <see cref="Npc.Skills"/>.</summary>
    private readonly List<(SkillType, double)> _pending = [];

    /// <summary>Mestres que já ensinaram hoje: o ensino treina uma vez por dia, não uma vez por aprendiz.</summary>
    private readonly HashSet<long> _taughtToday = [];

    public SkillTeachingSystem(SkillsRules rules, LifeStageRules lifeStageRules, RearingRules? rearing = null)
    {
        _rules = rules;
        _lifeStageRules = lifeStageRules;
        _rearing = rearing ?? RearingRules.Default;
        _workSkills = rules.WorkSkills();
    }

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Midshift;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!_rules.Enabled) return; // flag de teste desliga o sistema

        var living = world.AliveNpcIndex.Alive; // vivos em ordem de id; aqui ninguém nasce nem morre
        var calendar = world.CurrentDate.Calendar;
        long day = ctx.CurrentTick / calendar.HoursPerDay;
        AssignMentors(world, living);
        AssignApprenticeMasters(world, living);
        _taughtToday.Clear();

        var learning = _rules.Learning;
        for (int i = 0; i < living.Count; i++)
        {
            var npc = living[i];
            if (!npc.IsAlive) continue; // o índice só sabe da morte registrada no mundo
            double age = (double)(world.CurrentDate.TotalHours - npc.BirthDate.TotalHours) / calendar.HoursPerYear;
            var stage = _lifeStageRules.LifeStageOf((int)age);
            if (stage == LifeStage.Child && _rearing.Enabled && Due(day, npc, _rearing.SampleEveryDays))
                SampleRearing(world, npc);
            else if (stage != LifeStage.Child && npc.Rearing is { } record)
                npc.ConcludeRearing(_rearing.Apply(npc.Personality, record, world.FamilyRules.PersonalityHeritability));

            var vocation = _rules.Vocation;
            if (vocation.Enabled)
            {
                // gosto por ofício cedo na vida (ainda com os pais por perto) e escolha do ofício na juventude (AD-136)
                if (npc.Vocation is null && Due(day, npc, learning.MaintenanceEveryDays))
                    npc.SetVocation(VocationMechanic.NewTaste(world, ctx, npc, vocation, chosen: stage != LifeStage.Child));
                if (stage == LifeStage.Child && age >= learning.ApprenticeAgeYears && npc.Vocation is not { Chosen: true })
                    VocationMechanic.Choose(world, ctx, npc, _rules, _lifeStageRules);
            }

            double aptitude = learning.Aptitude(age);
            if (aptitude > 0)
            {
                if (stage == LifeStage.Adult) GainFromDeliberateTraining(world, npc, aptitude);
                if (stage == LifeStage.Child)
                {
                    GainFromSchool(npc, aptitude);
                    GainFromHousehold(world, npc, aptitude);
                }
                GainFromObservation(world, npc, aptitude);
                GainFromTutoring(world, npc, aptitude);
            }
            if (stage != LifeStage.Child && Due(day, npc, learning.MaintenanceEveryDays))
                Maintain(npc, age, calendar.DaysPerMonth * calendar.MonthsPerYear);
            npc.GainSkills(_pending, _rules.Cap);
            _pending.Clear();
        }
    }

    private static bool Due(long day, Npc npc, int everyDays) => (day + npc.Id.Value) % everyDays == 0;

    private LifeStage StageOf(WorldState world, Npc npc) => _lifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate));

    private bool IsCaregiverAge(WorldState world, Npc npc) => StageOf(world, npc) is LifeStage.Adult or LifeStage.Elder;

    /// <summary>Em cada oficina, o empregado mais habilidoso na própria profissão vira mestre dos colegas adultos da mesma
    /// profissão que estão ao menos <see cref="MentorMinGapOfCap"/> do teto abaixo dele e ainda sem mestre. Escolha
    /// determinística: maior habilidade, desempate pelo menor Id.</summary>
    private void AssignMentors(WorldState world, IReadOnlyList<Npc> living)
    {
        Dictionary<(WorkplaceId, int), Npc>? masters = null;
        foreach (var npc in living)
        {
            if (!npc.IsAlive || npc.Employer is not { } employer || npc.Mentor is not null) continue;
            if (!_rules.SkillByProfession.TryGetValue(npc.Profession.Id, out var skill)) continue;
            if (StageOf(world, npc) != LifeStage.Adult) continue;
            masters ??= BestPerWorkplace(world, living);
            if (!masters.TryGetValue((employer, npc.Profession.Id), out var master) || master.Id == npc.Id) continue;
            if (master.Skills.Get(skill) - npc.Skills.Get(skill) < MentorMinGapOfCap * _rules.Cap) continue;
            npc.AssignMentor(master.Id);
        }
    }

    private Dictionary<(WorkplaceId, int), Npc> BestPerWorkplace(WorldState world, IReadOnlyList<Npc> living)
    {
        var best = new Dictionary<(WorkplaceId, int), Npc>();
        foreach (var npc in living)
        {
            if (!npc.IsAlive || npc.Employer is not { } employer) continue;
            if (!_rules.SkillByProfession.TryGetValue(npc.Profession.Id, out var skill)) continue;
            if (StageOf(world, npc) != LifeStage.Adult) continue;
            var key = (employer, npc.Profession.Id);
            if (!best.TryGetValue(key, out var current) || npc.Skills.Get(skill) > current.Skills.Get(skill))
                best[key] = npc; // living vem ordenado por Id: empate fica com o menor Id
        }
        return best;
    }

    /// <summary>Do marco de aprendiz (<see cref="SkillLearningRules.ApprenticeAgeYears"/>) até virar adulto, o jovem sem
    /// mestre vai aprender o ofício que mais sabe (o que trouxe de casa; sem nada, o da própria profissão) com o adulto
    /// empregado mais habilidoso nele na própria cidade, se este estiver ao menos <see cref="MentorMinGapOfCap"/> do teto
    /// acima. O índice por (cidade, habilidade) só é montado no dia em que há aprendiz sem mestre.</summary>
    private void AssignApprenticeMasters(WorldState world, IReadOnlyList<Npc> living)
    {
        Dictionary<(CityId, int), Npc>? masters = null;
        foreach (var youth in living)
        {
            if (!youth.IsAlive || youth.Mentor is not null || StageOf(world, youth) != LifeStage.Child) continue;
            if (youth.AgeYears(world.CurrentDate) < _rules.Learning.ApprenticeAgeYears) continue;
            if (Inclination(youth) is not { } skill) continue;
            masters ??= BestPerCity(world, living);
            if (!masters.TryGetValue((youth.City, skill.Id), out var master)) continue;
            if (master.Skills.Get(skill) - youth.Skills.Get(skill) < MentorMinGapOfCap * _rules.Cap) continue;
            youth.AssignMentor(master.Id);
        }
    }

    private SkillType? Inclination(Npc youth)
    {
        // quem já escolheu o ofício (AD-136) aprende o dele; antes da escolha, o que mais sabe
        if (youth.Vocation is { Chosen: true } && _rules.SkillByProfession.TryGetValue(youth.Profession.Id, out var chosen))
            return chosen;
        SkillType? best = null;
        double bestLevel = 0;
        foreach (var skill in _workSkills)
        {
            double level = youth.Skills.Get(skill);
            if (level > bestLevel) (best, bestLevel) = (skill, level);
        }
        if (best is not null) return best;
        return _rules.SkillByProfession.TryGetValue(youth.Profession.Id, out var own) ? own : null;
    }

    private Dictionary<(CityId, int), Npc> BestPerCity(WorldState world, IReadOnlyList<Npc> living)
    {
        var best = new Dictionary<(CityId, int), Npc>();
        foreach (var npc in living)
        {
            if (!npc.IsAlive || npc.Employer is null || StageOf(world, npc) != LifeStage.Adult) continue;
            if (!_rules.SkillByProfession.TryGetValue(npc.Profession.Id, out var skill)) continue;
            var key = (npc.City, skill.Id);
            if (!best.TryGetValue(key, out var current) || npc.Skills.Get(skill) > current.Skills.Get(skill))
                best[key] = npc;
        }
        return best;
    }

    /// <summary>Adulto dedica tempo livre (Idle) e dinheiro (pago ao Treasury do próprio empregador, sem cunhar nem destruir
    /// dinheiro) pra treinar a habilidade da própria profissão. Sem saldo, sem emprego ou sem profissão mapeada — sem-op.</summary>
    private void GainFromDeliberateTraining(WorldState world, Npc npc, double aptitude)
    {
        if (npc.CurrentAction != ActionType.Idle) return;
        if (!_rules.SkillByProfession.TryGetValue(npc.Profession.Id, out var skillType)) return;
        if (npc.Employer is not { } employerId) return;
        if (world.FindWorkplace(employerId) is not { } workplace) return;
        if (!npc.TryDebitWallet(new Money(DeliberateTrainingCost)).IsSuccess) return;
        workplace.CreditTreasury(new Money(DeliberateTrainingCost));

        double gain = _rules.Gain(npc.Skills.Get(skillType), SkillGainSource.DeliberateTraining, npc.RateGene.Value) * aptitude;
        _pending.Add((skillType, gain));
    }

    /// <summary>Criança com profissão mapeada ganha pela escola, taxa de <see cref="SkillGainSource.School"/> — sem prédio
    /// nem vaga (o cenário que não tem escola declara taxa 0).</summary>
    private void GainFromSchool(Npc npc, double aptitude)
    {
        // sem ofício ainda (AD-136), a escola ensina aquilo para que a criança pende
        SkillType? target = _rules.SkillByProfession.TryGetValue(npc.Profession.Id, out var own) ? own : Inclination(npc);
        if (target is not { } skillType) return;
        double gain = _rules.Gain(npc.Skills.Get(skillType), SkillGainSource.School, npc.RateGene.Value) * aptitude;
        if (gain > 0) _pending.Add((skillType, gain));
    }

    /// <summary>A criança aprende o que os adultos da casa onde vive sabem (pais biológicos, adotivos, avós, tios, quem a
    /// acolheu), taxa de <see cref="SkillGainSource.Parental"/>: para cada ofício, com quem mais sabe dele na casa, até
    /// <see cref="SkillLearningRules.HouseholdCeilingFraction"/> do que essa pessoa sabe. Morar junto é o requisito; o
    /// tempo de convívio é o número de dias em que isso acontece.</summary>
    private void GainFromHousehold(WorldState world, Npc child, double aptitude)
    {
        if (child.Household is not { } householdId || world.FindHousehold(householdId) is not { } home) return;
        foreach (var skill in _workSkills)
        {
            double teacher = 0;
            foreach (var memberId in home.Members)
            {
                if (memberId == child.Id || world.FindNpc(memberId) is not { IsAlive: true } adult) continue;
                double level = adult.Skills.Get(skill);
                if (level > teacher && IsCaregiverAge(world, adult)) teacher = level;
            }
            double ceiling = teacher * _rules.Learning.HouseholdCeilingFraction;
            if (ceiling <= child.Skills.Get(skill)) continue;
            double rate = _rules.BaseRateBySource.GetValueOrDefault(SkillGainSource.Parental);
            double gain = SkillCurve.Gain(child.Skills.Get(skill), ceiling, rate) * child.RateGene.Value * aptitude;
            if (gain > 0) _pending.Add((skill, gain));
        }
    }

    /// <summary>Quem está na mesma célula de alguém trabalhando (que não seja o próprio mestre) ganha a habilidade que o
    /// observado pratica, taxa de <see cref="SkillGainSource.Observation"/>, até o que o observado sabe; no máximo um
    /// observado por dia, o de menor Id.</summary>
    private void GainFromObservation(WorldState world, Npc observer, double aptitude)
    {
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

        // Vendo, só se aprende o que o outro sabe: a curva tem teto na habilidade do observado.
        double known = Math.Min(observed.Skills.Get(skillType), _rules.Cap);
        if (observer.Skills.Get(skillType) >= known) return;
        double rate = _rules.BaseRateBySource.GetValueOrDefault(SkillGainSource.Observation);
        double gain = SkillCurve.Gain(observer.Skills.Get(skillType), known, rate) * observer.RateGene.Value * aptitude;
        if (gain > 0) _pending.Add((skillType, gain));
    }

    /// <summary>Aprendiz (<see cref="Npc.Mentor"/>) ganha a habilidade que o mestre pratica; mestre melhor (no ofício e no
    /// ensino) ensina mais. Mestre morto: o vínculo se encerra sem ganho. Alcançou o mestre: formado.</summary>
    private void GainFromTutoring(WorldState world, Npc apprentice, double aptitude)
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
        _pending.Add((skillType, baseGain * masterFactor * aptitude));

        // Ensinar também treina o ensino: o mestre ganha em TeachingSkill pela taxa de prática, uma vez por dia de ensino.
        if (!_taughtToday.Add(master.Id.Value)) return;
        double teachGain = _rules.Gain(masterTeaching, SkillGainSource.Practice, master.RateGene.Value);
        master.GainSkill(_rules.TeachingSkill, teachGain, _rules.Cap);
    }

    /// <summary>Um período de manutenção (<see cref="SkillLearningRules.MaintenanceEveryDays"/> dias): o ofício que a pessoa
    /// exerce (empregada na profissão que o pratica) não cai por desuso; os outros caem; a habilidade manual cai também com
    /// a velhice.</summary>
    private void Maintain(Npc npc, double age, int daysPerYear)
    {
        var learning = _rules.Learning;
        SkillType? practiced = npc.Employer is not null && _rules.SkillByProfession.TryGetValue(npc.Profession.Id, out var own)
            ? own
            : null;
        foreach (var skill in _workSkills)
        {
            double level = npc.Skills.Get(skill);
            if (level <= 0) continue;
            double loss = level * learning.LossFraction(
                age, practiced == skill, _rules.IsPhysical(skill), learning.MaintenanceEveryDays, daysPerYear);
            if (loss > 0) _pending.Add((skill, -loss));
        }
    }

    /// <summary>Uma amostra da criação: os adultos e idosos vivos da casa onde a criança mora hoje, em ordem de id.</summary>
    private void SampleRearing(WorldState world, Npc child)
    {
        if (child.Household is not { } householdId || world.FindHousehold(householdId) is not { } home) return;
        _caregivers.Clear();
        _caregiverNpcs.Clear();
        int biological = 0;
        foreach (var memberId in home.Members.OrderBy(m => m.Value))
        {
            if (memberId == child.Id || world.FindNpc(memberId) is not { IsAlive: true } adult) continue;
            if (!IsCaregiverAge(world, adult)) continue;
            _caregivers.Add(adult.Personality);
            _caregiverNpcs.Add(adult);
            if (memberId == child.MotherId || memberId == child.FatherId) biological++;
        }
        if (_caregivers.Count == 0) return;
        child.SetRearing((child.Rearing ?? RearingRecord.Empty).Add(_caregivers, biological));
        // AD-142: o que a criança aprende a valorizar puxa para o que os cuidadores valorizam
        if (world.BrainRules is { Enabled: true } brainRules)
            LivingWorld.Simulation.Cognition.BrainMechanic.Rear(brainRules, child, _caregiverNpcs);
    }
}
