namespace LivingWorld.Domain.Ecology;

/// <summary>Parâmetros de ciclo de vida por espécie animal — consumido só por <c>FaunaLifecycleSystem</c> (diário desde a
/// AD-135; a fome segue em energia por hora). Os campos opcionais (AD-135) têm padrão neutro, o comportamento antigo:
/// <see cref="Diet"/> "flora" faz o herbívoro pastar a planta mais próxima no raio <see cref="ForageRadius"/> (a planta perde
/// <see cref="GrazeStageCost"/> de estágio, o animal ganha <see cref="MealEnergy"/>); a cria só nasce entre
/// <see cref="BreedStartMonth"/> e <see cref="BreedEndMonth"/>, em ninhadas de <see cref="LitterSize"/>, com
/// <see cref="GestationDays"/> entre ninhadas do mesmo animal; <see cref="AnnualMortality"/> é a morte por idade, doença e
/// acidente fora da fome e da predação; com <see cref="HuntsOnlyWhenHungry"/> o predador saciado não caça; com
/// <see cref="WandersWhenHungry"/> o animal com fome e sem comida no raio anda uma célula por dia à procura. <see cref="ReproduceProbability"/> e <see cref="PredationProbability"/> são por dia.</summary>
public sealed record AnimalSpeciesRules(
    string Species,
    double HungerDecayPerTick,
    double ReproduceEnergyThreshold,
    double ReproduceRadius,
    double ReproduceProbability,
    string? PredatorOf,
    double PredationProbability,
    string? Diet = null,
    double ForageRadius = 1,
    double MealEnergy = 0,
    int GrazeStageCost = 0,
    int BreedStartMonth = 0,
    int BreedEndMonth = 11,
    int GestationDays = 0,
    int LitterSize = 1,
    double AnnualMortality = 0,
    bool HuntsOnlyWhenHungry = false,
    bool WandersWhenHungry = false)
{
    public const string FloraDiet = "flora";

    public bool BreedsInMonth(int month) =>
        BreedStartMonth <= BreedEndMonth ? month >= BreedStartMonth && month <= BreedEndMonth : month >= BreedStartMonth || month <= BreedEndMonth;
}
