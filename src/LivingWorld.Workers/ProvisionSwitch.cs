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
        return changed ? rules : null;
    }

    public static NeedsRules? NeedsRules()
    {
        var raw = Environment.GetEnvironmentVariable("LW_PROVISION");
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
