using LivingWorld.Domain.Cognition;

namespace LivingWorld.Simulation.Behavior.Decision;

/// <summary>Deriva oportunidades conhecidas a partir do snapshot de decisão — nunca inventa
/// opções que o NPC não conhece via beliefs/memórias/relações/powers já no contexto.</summary>
public static class OpportunityModel
{
    public const string FoodAtMarket = "FoodAtMarket";
    public const string NearbyJob = "NearbyJob";
    public const string PotentialPartner = "PotentialPartner";
    public const string ExtraordinaryCapability = "ExtraordinaryCapability";

    private const int PartnerAffectionMin = 55;
    private const int PartnerTrustMin = 50;

    /// <summary>Função pura: só emite oportunidades ancoradas em conhecimento já presente
    /// no <see cref="DecisionContext"/> (beliefs, memories, relationships, PowerOpportunities).</summary>
    public static IReadOnlyList<Opportunity> DeriveOpportunities(DecisionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var result = new List<Opportunity>(4);

        if (TryFoodAtMarket(ctx, out var food))
            result.Add(food);

        if (TryNearbyJob(ctx, out var job))
            result.Add(job);

        if (TryPotentialPartner(ctx, out var partner))
            result.Add(partner);

        if (ctx.PowerOpportunities.Count > 0)
        {
            var top = ctx.PowerOpportunities[0];
            result.Add(new Opportunity(
                ExtraordinaryCapability,
                Attractiveness: 50 + Math.Max(0, 30 - (double)top.EstimatedCost * 10 - top.EstimatedRisk * 20),
                Detail: top.MechanicToken));
        }

        return result;
    }

    private static bool TryFoodAtMarket(DecisionContext ctx, out Opportunity opportunity)
    {
        if (HasKnownSignal(ctx, "market", "mercado", "vendor", "vendedor", "feira", "bazaar")
            && HasKnownSignal(ctx, "food", "comida", "trigo", "grain", "bread", "estoque", "stock", "scarcity", "escassez", "fome", "hunger"))
        {
            // Conhece mercado E sinal de comida/estoque — atractividade sobe se hunger alto.
            double attract = 40 + Deficit(ctx.Needs.Hunger) * 0.4;
            opportunity = new Opportunity(FoodAtMarket, Math.Clamp(attract, 0, 100), Detail: "known-market");
            return true;
        }

        // Só "food at market" se o NPC tem crença/memória explícita de mercado com comida.
        if (HasKnownSignal(ctx, "food at market", "comida no mercado", "market has food", "mercado tem comida", "market stock"))
        {
            opportunity = new Opportunity(FoodAtMarket, 50 + Deficit(ctx.Needs.Hunger) * 0.3, Detail: "known-market");
            return true;
        }

        opportunity = null!;
        return false;
    }

    private static bool TryNearbyJob(DecisionContext ctx, out Opportunity opportunity)
    {
        if (HasKnownSignal(ctx, "job", "emprego", "vacancy", "vaga", "work available", "trabalho disponível", "hiring", "contrata"))
        {
            double attract = 35 + ctx.Personality.Ambition * 0.4;
            opportunity = new Opportunity(NearbyJob, Math.Clamp(attract, 0, 100), Detail: "known-job");
            return true;
        }

        opportunity = null!;
        return false;
    }

    private static bool TryPotentialPartner(DecisionContext ctx, out Opportunity opportunity)
    {
        RelationshipFact? best = null;
        int bestScore = 0;
        foreach (var rel in ctx.KnownRelationships)
        {
            if (rel.Affection < PartnerAffectionMin && rel.Trust < PartnerTrustMin)
                continue;
            int score = Math.Max(rel.Affection, rel.Trust);
            if (score > bestScore)
            {
                bestScore = score;
                best = rel;
            }
        }

        if (best is not { } partner)
        {
            opportunity = null!;
            return false;
        }

        opportunity = new Opportunity(
            PotentialPartner,
            Attractiveness: bestScore,
            Detail: partner.With.Value.ToString());
        return true;
    }

    private static bool HasKnownSignal(DecisionContext ctx, params string[] tokens)
    {
        foreach (var belief in ctx.RelevantBeliefs)
        {
            if (ContainsAny(belief, tokens))
                return true;
        }

        foreach (var memory in ctx.RelevantMemories)
        {
            if (ContainsAny(memory.Content, tokens))
                return true;
        }

        return false;
    }

    private static bool ContainsAny(string text, params string[] tokens)
    {
        foreach (var token in tokens)
        {
            if (text.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static int Deficit(int need) => Math.Max(0, 100 - need);
}
