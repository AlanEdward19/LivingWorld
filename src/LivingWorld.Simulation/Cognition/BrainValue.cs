namespace LivingWorld.Simulation.Cognition;

/// <summary>O valor que o NPC dá ao próprio estado (AD-160b), parcela por parcela, com nome na leitura do cérebro: Corpo = as quatro
/// necessidades (0–400); Despensa e Moeda = dias de comida por morador na despensa da casa e que a moeda compra ao preço de
/// referência, até o horizonte, vezes o valor de um dia; Humor = 100 × valência. A recompensa de uma ação é a variação do total do
/// começo ao fim dela (mais o salário que a jornada rende, quando é trabalho).</summary>
public readonly record struct BrainValue(double Body, double Pantry, double Money, double Mood)
{
    public double Total => Body + Pantry + Money + Mood;
}
