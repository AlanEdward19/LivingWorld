using System.Globalization;
using LivingWorld.Domain.Economy;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Workers;

/// <summary>Medição da lavoura (AD-149), só para os relatórios: <c>LW_AG</c> com vírgulas. <c>noland</c> = terra sem limite;
/// <c>noreap</c> = sem a ceifa da vila toda; <c>presence</c> liga <c>EconomyRules.PresenceNeedsWork</c> (E1), <c>nopresence</c>
/// desliga; <c>freeration</c> volta à ração da oficina de graça e <c>nomeal</c> tira a refeição do empregador (AD-151); <c>land=x</c>, <c>gain=x</c>, <c>reap=n</c>, <c>keep=n</c>, <c>seed=n</c>, <c>yield=x</c>, <c>dearth=n</c> trocam o número; <c>olddearth</c> volta a venda da carestia de antes (AD-182); <c>freegrain</c> volta o grão entre fazendas de graça (AD-190).</summary>
public static class AgricultureSwitch
{
    private static HashSet<string> Keys() =>
        (Environment.GetEnvironmentVariable("LW_AG") ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet();

    private static double? Number(string key) =>
        Keys().FirstOrDefault(k => k.StartsWith(key + "=", StringComparison.Ordinal)) is { } raw
            ? double.Parse(raw[(key.Length + 1)..], CultureInfo.InvariantCulture) : null;

    public static AgricultureRules? AgricultureRules()
    {
        var keys = Keys();
        if (Ad191.Off) { keys.Add("nofieldvalue"); keys.Add("nobarnprice"); } // AD-191: LW_AD191=off tira tudo o que a fatia ligou
        if (keys.Count == 0) return null;
        var ag = Domain.Economy.AgricultureRules.Default;
        if (keys.Contains("noland")) ag = ag with { SownHectaresPerFounder = 0 };
        if (keys.Contains("noreap")) ag = ag with { WholeVillageReaps = false };
        if (Number("land") is { } land) ag = ag with { SownHectaresPerFounder = land };
        if (Number("gain") is { } gain) ag = ag with { HandIntensityGain = gain };
        if (Number("reap") is { } reap) ag = ag with { ReapPerWorkerDay = (long)reap };
        if (Number("keep") is { } keep) ag = ag with { HandKeepPerYear = (long)keep };
        if (Number("seed") is { } seed) ag = ag with { SeedPerResident = (long)seed };
        if (Number("yield") is { } yield) ag = ag with { YieldPerSeed = yield };
        // AD-182 (08c item 148): olddearth = sem repartir a venda na carestia e com a semente à venda (o de antes); dearth=n troca os dias
        if (keys.Contains("olddearth")) ag = ag with { DearthSaleDays = 0, SeedKeptFromSale = false };
        if (Number("dearth") is { } dearth) ag = ag with { DearthSaleDays = (int)dearth };
        if (keys.Contains("seedsale")) ag = ag with { SeedKeptFromSale = false };
        if (keys.Contains("nodearthsow")) ag = ag with { DearthSowBoost = 0 }; // AD-190
        if (keys.Contains("freegrain")) ag = ag with { PayForCityGrain = false }; // AD-190: o grão da outra fazenda volta a ser de graça
        if (keys.Contains("nofieldvalue")) ag = ag with { FieldLaborValue = false };
        if (keys.Contains("nobarnprice")) ag = ag with { BarnReserveYears = 0 }; // AD-191: o preço volta a olhar só a banca // AD-191: a lavoura volta a valer o mínimo na escolha de emprego
        return ag;
    }

    public static EconomyRules? EconomyRules()
    {
        var keys = Keys();
        var econ = ScenarioRunner.DefaultEconomyRules;
        bool changed = false;
        if (keys.Contains("presence")) { econ = econ with { PresenceNeedsWork = true }; changed = true; }
        if (keys.Contains("nopresence")) { econ = econ with { PresenceNeedsWork = false }; changed = true; }
        if (keys.Contains("freeration")) { econ = econ with { FreeWorkRation = true }; changed = true; } // AD-151: a ração de antes
        if (keys.Contains("nomeal")) { econ = econ with { NoWorkMeal = true }; changed = true; } // AD-151: sem refeição do empregador
        // AD-155: só para medir: E20 desligado (o dono larga a oficina como no AD-152) e o crédito todo desligado
        if (keys.Contains("noownerkeeps")) { econ = econ with { Venture = econ.Venture! with { OwnerKeepsShop = false } }; changed = true; }
        if (keys.Contains("nocredit")) { econ = econ with { Credit = null }; changed = true; }
        return changed ? econ : null;
    }
}
