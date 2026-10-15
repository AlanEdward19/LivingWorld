using System.Text.Json.Nodes;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Population;

/// <summary>Lê o bloco opcional <c>Appearance</c> do período: <c>{ "Heritability": 0.6, "AttractionWeight": 1.0 }</c> (peso opcional, padrão 1.0). Ausente = <c>null</c> (sem genes de
/// aparência, hash e cliente como antes). Presente com <c>Enabled: false</c> também vale <c>null</c>.</summary>
public static class AppearanceScenarioLoader
{
    public static Result<AppearanceRules?> Load(JsonObject root)
    {
        if (root["Appearance"] is null) return Result<AppearanceRules?>.Ok(null);
        if (root["Appearance"] is not JsonObject block) return Result<AppearanceRules?>.Fail("Appearance: objeto inválido");

        if (block["Enabled"] is not null)
        {
            if (block["Enabled"] is not JsonValue ev || !ev.TryGetValue(out bool enabled))
                return Result<AppearanceRules?>.Fail("Appearance.Enabled: booleano inválido");
            if (!enabled) return Result<AppearanceRules?>.Ok(null);
        }

        if (block["Heritability"] is not JsonValue hv || !hv.TryGetValue(out double heritability))
            return Result<AppearanceRules?>.Fail("Appearance.Heritability: campo obrigatório ausente ou inválido");
        double attractionWeight = 1.0;
        if (block["AttractionWeight"] is not null
            && !(block["AttractionWeight"] is JsonValue av && av.TryGetValue(out attractionWeight)))
            return Result<AppearanceRules?>.Fail("Appearance.AttractionWeight: número inválido");
        var created = AppearanceRules.Create(heritability, attractionWeight);
        return created.IsSuccess ? Result<AppearanceRules?>.Ok(created.Value) : Result<AppearanceRules?>.Fail(created.Error!);
    }
}
