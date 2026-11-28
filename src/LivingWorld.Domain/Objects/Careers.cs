using System.Text.Json.Serialization;

namespace LivingWorld.Domain.Objects;

/// <summary>#263: carreiras e contas da casa como dado do cenário (catálogo <c>careers</c>). O motor só conhece os mecanismos (turno, desempenho, promoção,
/// pagamento, conta que vence); nome do cargo, nível, salário, horário, dias, o que pesa no desempenho e o valor de cada conta são do dado.
/// Nulo no <see cref="ObjectCatalog"/> = cenário sem carreira (fora do snapshot e do hash).</summary>
public sealed record CareerCatalog(CareerRules Rules, IReadOnlyList<CareerDef> Careers, IReadOnlyList<BillDef> Bills)
{
    public CareerDef? Find(string id) => Careers.FirstOrDefault(c => c.Id == id);

    public BillDef? FindBill(string id) => Bills.FirstOrDefault(b => b.Id == id);
}

/// <summary>Regras gerais: dias da semana, aviso antes do turno, tolerância para sair atrasado, faltas seguidas até a demissão, a hora do dia em que as contas
/// são emitidas e vencem, quantos avisos o livro guarda e o intervalo de validade de uma busca de emprego (horas) e os nomes dos dias da semana do cenário (<c>DayNames</c>, só para ler). <c>StopKinds</c> (#267): os tipos de aviso que pedem tempo normal (hora do trabalho, conta vencendo, alguém chegando); o cliente desce a velocidade ao vê-los.</summary>
public sealed record CareerRules(
    int WeekDays = 7, int NoticeLeadHours = 1, int GraceHours = 1, int AbsencesToFire = 3, int BillHour = 8, int NoticeKeep = 40, int SearchValidHours = 24,
    double IdleSearchPerDay = 0.15, IReadOnlyList<string>? DayNames = null, IReadOnlyList<string>? StopKinds = null);

/// <summary>Uma carreira: o <c>EmployerType</c> é o tipo de local (<c>LocationType</c>) dos empregadores que contratam nela, o corpo sai por <c>CommuteMin</c> minutos
/// de ida e de volta (fora do mapa, como no Sims), a habilidade (<c>SkillId</c>) entra no desempenho e cresce com o trabalho (<see cref="CareerPerformance"/>).</summary>
public sealed record CareerDef(
    string Id, string Name, int EmployerType, int CommuteMin, IReadOnlyList<CareerLevel> Levels, CareerPerformance Performance, CareerSearch Search,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? SkillId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SkillName = null);

/// <summary>Um nível da carreira: cargo, salário por hora trabalhada, o horário (<c>StartHour</c> e <c>Hours</c>), os dias da semana, a habilidade mínima para entrar nele,
/// o desempenho que promove (<c>PromoteAt</c> por <c>PromoteAfter</c> turnos seguidos) ou rebaixa (<c>DemoteBelow</c> por <c>DemoteAfter</c>) e o que o turno dá ao corpo
/// por hora (refeição do trabalho, café).</summary>
public sealed record CareerLevel(
    string Id, string Title, long WagePerHour, int StartHour, int Hours, IReadOnlyList<int> Days, double MinSkill, double PromoteAt, int PromoteAfter,
    double DemoteBelow, int DemoteAfter, IReadOnlyList<UseEffect> AwayEffects);

/// <summary>O que pesa no desempenho do turno (0–100): o nível de cada necessidade ao sair (pesos por id, de sempre ou do catálogo), a habilidade
/// (<c>Skill</c>, fração do peso) e o <c>Practice</c> de habilidade que cada hora de trabalho rende.</summary>
public sealed record CareerPerformance(IReadOnlyDictionary<string, double> Needs, double Skill, double Practice);

/// <summary>Procurar emprego: a chance de ser contratado é <c>BaseChance + PerSkill × habilidade</c> (0–1), sorteada pelo RNG semeado do mundo.</summary>
public sealed record CareerSearch(double BaseChance, double PerSkill);

/// <summary>Uma conta da casa: <c>Fixed</c> a cada <c>EveryDays</c> dias (IPTU, aluguel) mais o uso medido (<c>MeterUses</c>: ids de uso ou de <c>toggle</c> do catálogo
/// de objetos; <c>PerHour</c> por hora de uso, de qualquer morador). Vence em <c>DueDays</c>; atrasada, cobra <c>LateFeeRate</c> (fração) uma vez e, depois de <c>CutAfterDays</c>,
/// corta o serviço (<c>Blocks</c>: os usos que deixam de funcionar) até pagar. O pagamento vai ao tesouro da cidade (moeda só muda de mão).</summary>
public sealed record BillDef(
    string Id, string Name, long Fixed, int EveryDays, int DueDays, IReadOnlyList<string> MeterUses, double PerHour, double LateFeeRate, int CutAfterDays,
    IReadOnlyList<string> Blocks, int FirstIssueDay = 1);
