using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Deriva pressões ativas a partir do snapshot de decisão já construído.</summary>
public static class PressureModel
{
    public const string AcquireFood = "AcquireFood";
    public const string EarnIncome = "EarnIncome";
    public const string ProtectHousehold = "ProtectHousehold";
    public const string SeekRest = "SeekRest";
    public const string SeekSocial = "SeekSocial";

    /// <summary>Função pura: needs/household/relações/ameaças/personalidade → lista de
    /// <see cref="Pressure"/>. Intensidade 0..100; fatores nomeados para Decision Trace.</summary>
    public static IReadOnlyList<Pressure> DerivePressures(DecisionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var result = new List<Pressure>(5);

        int hungerDeficit = Deficit(ctx.Needs.Hunger);
        if (hungerDeficit > 0)
            result.Add(new Pressure(AcquireFood, hungerDeficit, ["Hunger"]));

        int sleepDeficit = Deficit(ctx.Needs.Sleep);
        if (sleepDeficit > 0)
            result.Add(new Pressure(SeekRest, sleepDeficit, ["Sleep"]));

        int socialDeficit = Deficit(ctx.Needs.Social);
        if (socialDeficit > 0)
            result.Add(new Pressure(SeekSocial, socialDeficit, ["Social"]));

        if (TryEarnIncome(ctx, hungerDeficit, out var earn))
            result.Add(earn);

        if (TryProtectHousehold(ctx, out var protect))
            result.Add(protect);

        return result;
    }

    private static bool TryEarnIncome(DecisionContext ctx, int hungerDeficit, out Pressure pressure)
    {
        var factors = new List<string>(3);
        double intensity = 0;

        if (ctx.Personality.Ambition >= 60)
        {
            factors.Add("Ambition");
            intensity += ctx.Personality.Ambition * 0.35;
        }

        if (ctx.Household is { } household)
        {
            long totalStock = 0;
            foreach (var qty in household.Stock.Values)
                totalStock += qty;
            if (totalStock < 1)
            {
                factors.Add("EmptyHouseholdStock");
                intensity += 40;
            }
        }
        else if (hungerDeficit >= 40)
        {
            factors.Add("NoHouseholdHunger");
            intensity += hungerDeficit * 0.4;
        }

        if (factors.Count == 0)
        {
            pressure = null!;
            return false;
        }

        pressure = new Pressure(EarnIncome, Math.Clamp(intensity, 0, 100), factors);
        return true;
    }

    /// <summary>ProtectHousehold combina ≥3 eixos quando presentes (doc#34): dependentes,
    /// força de relação, ameaça, recursos, personalidade, capacidade física.</summary>
    private static bool TryProtectHousehold(DecisionContext ctx, out Pressure pressure)
    {
        if (ctx.Household is not { } household)
        {
            pressure = null!;
            return false;
        }

        var factors = new List<string>(6);
        double intensity = 0;

        if (household.Members.Count > 1)
        {
            factors.Add("Dependents");
            intensity += Math.Min(40, (household.Members.Count - 1) * 15);
        }

        if (ctx.KnownRelationships.Any(r => r.Affection >= 50 || r.Trust >= 50 || r.Respect >= 50))
        {
            factors.Add("RelationshipStrength");
            var best = ctx.KnownRelationships.Max(r =>
                Math.Max(r.Affection, Math.Max(r.Trust, r.Respect)));
            intensity += best * 0.25;
        }

        if (HasThreatSignal(ctx))
        {
            factors.Add("Threat");
            intensity += 35;
        }

        long stockUnits = 0;
        foreach (var qty in household.Stock.Values)
            stockUnits += qty;
        if (stockUnits < 1)
        {
            factors.Add("HouseholdResources");
            intensity += 20;
        }

        if (ctx.Personality.Loyalty >= 55 || ctx.Personality.Altruism >= 55)
        {
            factors.Add("Personality");
            intensity += Math.Max(ctx.Personality.Loyalty, ctx.Personality.Altruism) * 0.2;
        }

        if (ctx.Body.MuscleMass < 25 || ctx.Body.WorkCapacityMultiplier < 0.95)
        {
            factors.Add("PhysicalCapacity");
            intensity += 15;
        }

        if (factors.Count == 0)
        {
            pressure = null!;
            return false;
        }

        pressure = new Pressure(ProtectHousehold, Math.Clamp(intensity, 0, 100), factors);
        return true;
    }

    private static bool HasThreatSignal(DecisionContext ctx)
    {
        foreach (var memory in ctx.RelevantMemories)
        {
            if (ContainsAny(memory.Content, "traído", "traido", "betray", "threat", "perigo", "danger", "ameaça", "ameaca"))
                return true;
        }

        foreach (var belief in ctx.RelevantBeliefs)
        {
            if (ContainsAny(belief, "threat", "perigo", "danger", "ameaça", "ameaca", "raid", "attack"))
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
