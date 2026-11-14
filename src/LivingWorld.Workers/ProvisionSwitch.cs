using LivingWorld.Domain.Behavior;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Workers;

/// <summary>Medição da compra da casa (AD-148): <c>LW_PROVISION</c> vazio = padrão; <c>legacy</c> = <see cref="ProvisioningRules.Legacy"/>;
/// ou uma lista com vírgula das chaves que ficam DESLIGADAS (<c>topup</c>, <c>partial</c>, <c>pay</c>, <c>one</c>, <c>work</c>,
/// <c>stall</c>, <c>leisure</c> = <c>PsycheRules.CriticalBodyOverridesLeisure</c>) e <c>+carry</c> liga a carga pessoal. Só para os relatórios.</summary>
public static class ProvisionSwitch
{
    /// <summary><c>+carry</c> em <c>LW_PROVISION</c> liga <c>InventionRules.PantryNeedsPresence</c> (carga pessoal, AD-147).</summary>
    public static LivingWorld.Domain.Invention.InventionRules? InventionRules() =>
        (Environment.GetEnvironmentVariable("LW_PROVISION") ?? "").Contains("+carry", StringComparison.Ordinal)
            ? LivingWorld.Domain.Invention.InventionRules.Default with { PantryNeedsPresence = true }
            : null;

    public static NeedsRules? NeedsRules()
    {
        var raw = Environment.GetEnvironmentVariable("LW_PROVISION");
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
