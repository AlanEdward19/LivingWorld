namespace LivingWorld.Workers;

/// <summary>AD-196, só para os relatórios: <c>LW_AD196=off</c> volta o desaparecimento e a volta ao AD-111 (vínculo de parente
/// sempre 100, procura de 30 dias para todos, sem luto nem reencontro na psique, encarnação no log do mundo, sem política de
/// abandono nem poder pelo jogador). AD-197f: <c>LW_AD197F=off|nowill|nostrange|noreact</c> desliga a posse como disputa de
/// vontade, o estranhamento ou a reação (uma regra por vez, ou todas). O mundo sem jogador não muda com as chaves.</summary>
public static class Ad196
{
    public static LivingWorld.Domain.Population.PlayerRules? Rules
    {
        get
        {
            if (Environment.GetEnvironmentVariable("LW_AD196") == "off") return LivingWorld.Domain.Population.PlayerRules.Off;
            var possession = LivingWorld.Domain.Population.PossessionRules.Default;
            possession = Environment.GetEnvironmentVariable("LW_AD197F") switch
            {
                "off" => LivingWorld.Domain.Population.PossessionRules.Off,
                "nowill" => possession with { WillContest = false },
                "nostrange" => possession with { Strangeness = false },
                "noreact" => possession with { Reaction = false },
                _ => possession,
            };
            return possession == LivingWorld.Domain.Population.PossessionRules.Default
                ? null
                : LivingWorld.Domain.Population.PlayerRules.Default with { Possession = possession };
        }
    }
}
