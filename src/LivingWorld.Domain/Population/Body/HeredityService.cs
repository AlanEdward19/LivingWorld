using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Population.Body;

/// <summary>Funções puras de hereditariedade da Fase 7 (T6, sem estado, mesmo molde de
/// <see cref="SkillCurve"/>): <c>Vitality</c> genético herdado dos pais + mutação, e
/// <c>Upbringing</c> ambiental derivado da riqueza do household — origens distintas por
/// construção (FAM-18/19/20), nunca o mesmo canal.</summary>
public static class HeredityService
{
    private const double MidpointValue = 50.0;
    private const double InitialSpread = 30.0;

    /// <summary>Sorteia <c>Vitality</c> de um NPC sem pais conhecidos (população seed, Edge
    /// Case) — mesmo padrão de <see cref="RateGene.RollInitial"/>, distribuição em torno do
    /// meio da escala <c>[0,100]</c>, sempre clampada a essa faixa.</summary>
    public static double RollInitialVitality(WorldRng rng) => RollInitial(rng);

    /// <summary>Sorteia <c>Upbringing</c> de um NPC sem household de concepção conhecido
    /// (população seed, Edge Case) — mesma distribuição de <see cref="RollInitialVitality"/>,
    /// canal independente (stream próprio do chamador garante que não correlaciona com
    /// <c>Vitality</c> do mesmo NPC).</summary>
    public static double RollInitialUpbringing(WorldRng rng) => RollInitial(rng);

    private static double RollInitial(WorldRng rng)
    {
        double value = MidpointValue + (rng.NextDouble() * 2 - 1) * InitialSpread;
        return Math.Clamp(value, 0.0, 100.0);
    }

    /// <summary><c>vitalidadeFilho = mãe*pesoMãe + pai*pesoPai + mutação</c> (FAM-18), clampado a
    /// <c>[0,100]</c> — nunca produz valor fora da faixa mesmo com mutação extrema (mesma
    /// garantia de <see cref="RateGene.Inherit"/>). O stream de <paramref name="rng"/> já deve
    /// vir semeado por uma chave que inclui o <c>NpcId</c> do filho — responsabilidade de quem
    /// chama (mesmo padrão de <c>RateGene.Inherit</c>/<c>rategene-{babyId}</c>).</summary>
    public static double InheritVitality(double motherVitality, double fatherVitality, FamilyRules rules, WorldRng rng)
    {
        double blended = motherVitality * rules.VitalityMotherWeight + fatherVitality * rules.VitalityFatherWeight;
        double mutation = (rng.NextDouble() * 2 - 1) * rules.VitalityMutationStdDev;
        return Math.Clamp(blended + mutation, 0.0, 100.0);
    }

    /// <summary>Deriva <c>Upbringing</c> puramente da riqueza (soma do <see cref="Household.Stock"/>)
    /// do household na concepção (FAM-19/20) — <b>nunca lê <c>Vitality</c>/genes dos pais</b>,
    /// canal ambiental inteiramente independente do genético por construção (prova estrutural:
    /// a assinatura não aceita nenhum parâmetro de <c>Vitality</c>/gene).
    ///
    /// SPEC_DEVIATION: o design descreve a riqueza como "Stock valorizado a preço de mercado +
    /// Wallet dos membros"; <see cref="Household"/> (camada Domain) não expõe preço de mercado
    /// nem os <c>Wallet</c> dos <c>Npc</c> membros (esses vivem em <c>WorldState</c>/Simulation).
    /// Nesta task (T6, Domain puro) a riqueza é a soma bruta das unidades em
    /// <see cref="Household.Stock"/>, ponderada por <see cref="FamilyRules.UpbringingWealthWeight"/>
    /// — suficiente para provar o canal independente (FAM-19/20) e a divergência por riqueza
    /// diferente. A valoração de mercado + Wallet fica para quem integrar o wiring completo
    /// (Fase 7, T17/NatalitySystem, fora do escopo deste grupo de tasks).
    /// Reason: Household não tem acesso a WorldState/preços nesta camada; capturar a riqueza
    /// "rica" precisaria de dados só disponíveis na camada Simulation.</summary>
    public static double DeriveUpbringing(Household conceptionHousehold, FamilyRules rules)
    {
        double totalStock = conceptionHousehold.Stock.Values.Sum();
        return DeriveUpbringingFromConceptionStock(totalStock, rules);
    }

    /// <summary>Deriva <c>Upbringing</c> a partir da riqueza capturada na concepção (Fase 7,
    /// T17) — mesmo cálculo de <see cref="DeriveUpbringing"/>, sem reler o household no
    /// nascimento.</summary>
    public static double DeriveUpbringingFromConceptionStock(double totalStock, FamilyRules rules)
    {
        double upbringing = totalStock * rules.UpbringingWealthWeight;
        return Math.Clamp(upbringing, 0.0, 100.0);
    }
}
