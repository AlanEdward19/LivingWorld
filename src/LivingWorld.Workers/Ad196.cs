namespace LivingWorld.Workers;

/// <summary>AD-196, só para os relatórios: <c>LW_AD196=off</c> volta o desaparecimento e a volta ao AD-111 (vínculo de parente
/// sempre 100, procura de 30 dias para todos, sem luto nem reencontro na psique, encarnação no log do mundo, sem política de
/// abandono nem poder pelo jogador). O mundo sem jogador não muda com a chave.</summary>
public static class Ad196
{
    public static LivingWorld.Domain.Population.PlayerRules? Rules =>
        Environment.GetEnvironmentVariable("LW_AD196") == "off" ? LivingWorld.Domain.Population.PlayerRules.Off : null;
}
