using LivingWorld.Domain.Behavior;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Workers;

/// <summary>Medição da compra da casa (AD-148): <c>LW_PROVISION</c> vazio = padrão; <c>legacy</c> = <see cref="ProvisioningRules.Legacy"/>;
/// ou uma lista com vírgula das chaves que ficam DESLIGADAS (<c>topup</c>, <c>partial</c>, <c>pay</c>, <c>one</c>, <c>work</c>,
/// <c>stall</c>, <c>leisure</c> = <c>PsycheRules.CriticalBodyOverridesLeisure</c>) e <c>+carry</c> liga a carga pessoal. Só para os relatórios.</summary>
public static class ProvisionSwitch
{
    /// <summary><c>+carry</c> em <c>LW_PROVISION</c> liga <c>InventionRules.PantryNeedsPresence</c> (carga pessoal, AD-147).</summary>
    /// AD-149: a carga pessoal é padrão do cenário de código; <c>-carry</c> a desliga para medir.
    /// AD-153: <c>-landcargo</c> desliga o corte da compra por terra e <c>-haul</c> as ideias de levar por terra.
    public static LivingWorld.Domain.Invention.InventionRules? InventionRules()
    {
        var raw = Environment.GetEnvironmentVariable("LW_PROVISION") ?? "";
        var rules = ScenarioRunner.DefaultInventionRules; // AD-157b: a carga pessoal vem ligada no cenário
        bool changed = false;
        if (raw.Contains("-carry", StringComparison.Ordinal)) { rules = rules with { PantryNeedsPresence = false }; changed = true; }
        else if (raw.Contains("+carry", StringComparison.Ordinal)) { rules = rules with { PantryNeedsPresence = true }; changed = true; }
        if (raw.Contains("-landcargo", StringComparison.Ordinal)) { rules = rules with { LandCargo = false }; changed = true; }
        if (raw.Contains("-haul", StringComparison.Ordinal)) { rules = rules with { LandVehicles = false }; changed = true; }
        // AD-165 (correções #13): LW_PULL=N troca a força humana sustentada puxando
        if (int.TryParse(Environment.GetEnvironmentVariable("LW_PULL"), System.Globalization.CultureInfo.InvariantCulture, out int pull))
        { rules = rules with { HumanPullN = pull }; changed = true; }
        // AD-166 (correções #6): LW_FOOT_KG=N troca a carga a pé
        if (int.TryParse(Environment.GetEnvironmentVariable("LW_FOOT_KG"), System.Globalization.CultureInfo.InvariantCulture, out int foot))
        { rules = rules with { FootCarryKg = foot }; changed = true; }
        // AD-168: LW_LEAVINGS=off devolve a carga de quem morre longe à despensa na hora (o de antes), para o pareado
        if (Environment.GetEnvironmentVariable("LW_LEAVINGS") == "off")
        { rules = rules with { LeavingsOverride = LivingWorld.Domain.Invention.LeavingsRules.Disabled }; changed = true; }
        // AD-182: LW_MARKET_TRIP=off devolve a ida ao mercado de antes (recipiente livre sem lanche, volta por qualquer sobra; 08c
        // 111 e 147); LW_GROUND=old devolve o estrago no chão de antes (taxa da despensa, água sem perda, sem calor; 08c 156)
        if (Environment.GetEnvironmentVariable("LW_MARKET_TRIP") == "off")
        { rules = rules with { MarketTripOverride = LivingWorld.Domain.Invention.MarketTripRules.Disabled }; changed = true; }
        // AD-182 (08c item 152): LW_DEATH_FORGETS=old devolve o de antes do AD-166 (acidente, velhice, homicídio e forca não levam o saber)
        if (Environment.GetEnvironmentVariable("LW_DEATH_FORGETS") == "old") { rules = rules with { EveryDeathForgets = false }; changed = true; }
        // AD-182 (08c item 155): LW_LEAVINGS_THEFT=old tira a barreira do laço com o morto
        if (Environment.GetEnvironmentVariable("LW_LEAVINGS_THEFT") == "old")
        { rules = rules with { LeavingsOverride = rules.Leavings with { KnownDeadBarrier = 0 } }; changed = true; }
        if (Environment.GetEnvironmentVariable("LW_GROUND") == "old")
        {
            rules = rules with
            {
                LeavingsOverride = rules.Leavings with { GroundSpoilagePm = 1000, GroundWaterLossPerDayPm = 0, GroundQ10 = 1, CountDaysOnGround = false },
            };
            changed = true;
        }
        return changed ? rules : null;
    }

    public static NeedsRules? NeedsRules()
    {
        var raw = Environment.GetEnvironmentVariable("LW_PROVISION");
        // AD-191: LW_AD191=off volta à banca mais perto que vende comida ou água
        if (Ad191.Off)
        {
            var n = ScenarioRunner.DefaultNeedsRules;
            return n with { Provisioning = n.Provisioning with { FoodFromAnyStall = false }, Psyche = n.Psyche with { IncomeNeedNeedsWork = false } };
        }
        // AD-186: LW_CARE=-company volta a conversar sozinho (sem sair atrás de companhia)
        if ((Environment.GetEnvironmentVariable("LW_CARE") ?? "").Contains("-company", StringComparison.Ordinal))
        {
            var n = ScenarioRunner.DefaultNeedsRules;
            return n with { Psyche = n.Psyche with { SeekCompany = false } };
        }
        // AD-165: LW_FEAR_TRAVEL=x troca o peso do medo contra sair (FearTravelPenalty) para testar se ele corta as idas da vítima
        if (double.TryParse(Environment.GetEnvironmentVariable("LW_FEAR_TRAVEL"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double fear))
        {
            var n = ScenarioRunner.DefaultNeedsRules;
            return n with { Psyche = n.Psyche with { FearTravelPenalty = fear } };
        }
        // AD-166: LW_GRIEF=old (sem luto por acidente/velhice e luto vencendo o comer), mourn (só o luto novo), yield (só o luto
        // cedendo ao corpo)
        if (Environment.GetEnvironmentVariable("LW_GRIEF") is { Length: > 0 } grief)
        {
            var n = ScenarioRunner.DefaultNeedsRules;
            return n with { Psyche = n.Psyche with { MournEveryDeath = grief == "mourn", GriefYieldsToBody = grief == "yield" } };
        }
        // AD-182 (08c item 149): LW_BODY_BUY=off volta o teto do lazer a só valer com a despensa servindo
        if (string.IsNullOrWhiteSpace(raw) && Environment.GetEnvironmentVariable("LW_BODY_BUY") == "off")
        {
            var n = ScenarioRunner.DefaultNeedsRules;
            return n with { Psyche = n.Psyche with { CriticalBodyCountsPurchase = false } };
        }
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var keys = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var d = ScenarioRunner.DefaultNeedsRules;
        var p = keys.Contains("legacy") ? ProvisioningRules.Legacy : d.Provisioning with
        {
            TopUp = !keys.Contains("topup"),
            BuyWhatIsThere = !keys.Contains("partial"),
            OnlyIfCanPay = !keys.Contains("pay"),
            OneShopperPerHousehold = !keys.Contains("one"),
            RestockDuringWorkBelowDays = keys.Contains("work") ? -1 : d.Provisioning.RestockDuringWorkBelowDays,
            FoodStallRetryHours = keys.Contains("stall") ? 0 : d.Provisioning.FoodStallRetryHours,
        };
        var psyche = keys.Contains("leisure") ? d.Psyche with { CriticalBodyOverridesLeisure = false } : d.Psyche;
        return d with { Provisioning = p, Psyche = psyche };
    }
}
