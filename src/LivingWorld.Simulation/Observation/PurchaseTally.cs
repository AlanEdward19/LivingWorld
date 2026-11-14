namespace LivingWorld.Simulation.Observation;

/// <summary>Contagem da compra de comida e água (AD-148): por que se vai ao mercado e por que a ida traz pouco. Só para a sonda
/// <c>provision-trace</c> e os relatórios; nenhum sistema decide por ela, e o hash não a vê.</summary>
public sealed class PurchaseTally
{
    // de onde veio a decisão de ir comprar
    public long BuyByUtility, BuyByRestock;

    // tentativas no mercado
    public long Attempts, AttemptsNothingBought;

    // comida: o que impediu ou cortou a compra
    public long FoodNoMoney, FoodTargetMet, FoodWalletCapped, FoodCargoCut, FoodStockShort, FoodBought, FoodUnits, FoodWantedUnits;
    public long FoodPantryUnitsBefore, FoodPantryMembers, MarketFoodAtShort;

    // água
    public long WaterFromWell, WaterBought, WaterUnits, WaterStockShort;

    // AD-148: idas de reposição que a casa não fez (outra pessoa da casa já foi; no turno, com despensa para ele)
    public long SkippedHouseholdCovered, SkippedDuringWork;

    // Diagnóstico da hora de trabalho (só a sonda liga): o empregado na hora da rotina de trabalho, o que a decisão fez dela
    public static bool Diagnose;
    public long[] UrgentAtWork = new long[16];
    public long NonUrgentAtWork, RestockAtWork, MoodAtWork, PerceptionAtWork, DevelopmentAtWork, SickAtWork, CareAtWork;

    public PurchaseTally Copy()
    {
        var c = (PurchaseTally)MemberwiseClone();
        c.UrgentAtWork = (long[])UrgentAtWork.Clone();
        return c;
    }
}
