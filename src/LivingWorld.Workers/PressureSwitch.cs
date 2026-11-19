using LivingWorld.Domain.Population.Family;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Workers;

/// <summary>Medição com os freios preventivos (AD-146): <c>LW_PRESSURE</c> vazio = padrão (ligado desde o AD-148); <c>off</c> = desligado; <c>all</c> = <see
/// cref="PopulationPressureRules.Measured"/>; ou uma lista com vírgula das chaves que ficam ligadas (<c>means</c>, <c>first</c>,
/// <c>kin</c>, <c>alms</c>, <c>topup</c>, <c>emig</c>, <c>house</c>), o resto desligado; <c>nohouse</c> = padrão sem a posse para casar (AD-158). Só para os relatórios, não muda o cenário padrão.</summary>
public static class PressureSwitch
{
    public static FamilyRules? FamilyRules()
    {
        var raw = Environment.GetEnvironmentVariable("LW_PRESSURE");
        // AD-166: LW_AID=old (ajuda de antes), market (só a moeda que vê a banca), food (só a comida do excedente)
        if (string.IsNullOrWhiteSpace(raw) && Environment.GetEnvironmentVariable("LW_AID") is { Length: > 0 } aid)
            return ScenarioRunner.DefaultFamilyRules with
            {
                Pressure = PopulationPressureRules.Measured with { AidSeesFoodForSale = aid == "market", FoodAidFromSurplus = aid == "food" },
            };
        // AD-166: LW_AID_RESERVE=n troca os dias que quem socorre guarda antes de mandar comida
        if (string.IsNullOrWhiteSpace(raw) && int.TryParse(Environment.GetEnvironmentVariable("LW_AID_RESERVE"), out int reserve))
            return ScenarioRunner.DefaultFamilyRules with { Pressure = PopulationPressureRules.Measured with { FoodAidReserveDays = reserve } };
        // AD-182 (08c item 151): LW_CONCEPTION_PANTRY=old volta o piso de comida em casa para conceber
        if (string.IsNullOrWhiteSpace(raw) && Environment.GetEnvironmentVariable("LW_CONCEPTION_PANTRY") == "old")
            return ScenarioRunner.DefaultFamilyRules with { ConceptionFloorReadsBody = false };
        // AD-182 (08c item 149): LW_AID_WHO=off volta a contar a moeda de quem não sabe comprar
        if (string.IsNullOrWhiteSpace(raw) && Environment.GetEnvironmentVariable("LW_AID_WHO") == "off")
            return ScenarioRunner.DefaultFamilyRules with { Pressure = PopulationPressureRules.Measured with { AidSeesWhoCanBuy = false } };
        // AD-191: LW_AD191=off tira a bolsa da casa para quem compra
        if (string.IsNullOrWhiteSpace(raw) && Ad191.Off)
            return ScenarioRunner.DefaultFamilyRules with { Pressure = PopulationPressureRules.Measured with { PurseToShopper = false } };
        // AD-190: LW_AID190=off tira o socorro do vizinho
        if (string.IsNullOrWhiteSpace(raw) && Environment.GetEnvironmentVariable("LW_AID190") is { Length: > 0 } a190)
            return ScenarioRunner.DefaultFamilyRules with
            {
                Pressure = PopulationPressureRules.Measured with
                {
                    NeighbourAidHouses = 0,
                },
            };
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (raw == "off") return ScenarioRunner.DefaultFamilyRules with { Pressure = null }; // AD-148: o padrão agora é ligado
        if (raw == "nohouse") return ScenarioRunner.DefaultFamilyRules with { Pressure = PopulationPressureRules.Measured with { Holdings = null } }; // AD-158
        if (raw == "all") return ScenarioRunner.DefaultFamilyRules with { Pressure = PopulationPressureRules.Measured };
        var on = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var m = PopulationPressureRules.Measured;
        return ScenarioRunner.DefaultFamilyRules with
        {
            Pressure = m with
            {
                MarriageMeansWeight = on.Contains("means") ? m.MarriageMeansWeight : 0,
                FirstBirthMeansWeight = on.Contains("first") ? m.FirstBirthMeansWeight : 0,
                KinFoodAid = on.Contains("kin"),
                RationedAlms = on.Contains("alms"),
                TopUpPurchases = on.Contains("topup"),
                Emigration = on.Contains("emig") ? m.Emigration : EmigrationRules.Disabled,
                Holdings = on.Contains("house") ? m.Holdings : null,
            },
        };
    }
}
