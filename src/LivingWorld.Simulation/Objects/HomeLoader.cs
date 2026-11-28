using System.Text.Json;
using System.Text.Json.Nodes;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Objects;

/// <summary>#264: lê do cenário o bloco <c>Home</c> (móveis à venda, preços, beleza, pinturas e o custo da obra; ver <see cref="HomeCatalog"/>) e o junta ao catálogo padrão
/// <c>scenarios/catalog/home-build.json</c> que o cenário estende. O código não conhece nenhum móvel nem preço: tudo vem do dado.</summary>
public static class HomeLoader
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { ReadCommentHandling = JsonCommentHandling.Skip };
    private static readonly string[] PaintKinds = ["floor", "wall"];

    /// <summary>Junta o bloco do catálogo-base ao do cenário: categoria, móvel (por objeto) e pintura de mesmo id ficam como o cenário os tem; regras e obra do cenário vencem.</summary>
    public static void Merge(JsonObject root, JsonObject catalog)
    {
        if (catalog["Home"] is not JsonObject baseHome) return;
        if (root["Home"] is not JsonObject own) { root["Home"] = baseHome.DeepClone(); return; }
        foreach (var key in new[] { "rules", "build" })
            if (own[key] is null && baseHome[key] is { } value) own[key] = value.DeepClone();
        MergeList(own, baseHome, "categories", "id");
        MergeList(own, baseHome, "items", "object");
        MergeList(own, baseHome, "paints", "id");
        MergeHaul(own, baseHome);
    }

    /// <summary>#265: o bloco <c>haul</c> do cenário vence; o que ele não diz (mãos, entrega, alcance) vem do catálogo-base, e peso e veículos juntam por id.</summary>
    private static void MergeHaul(JsonObject own, JsonObject baseHome)
    {
        if (baseHome["haul"] is not JsonObject baseHaul) return;
        if (own["haul"] is not JsonObject haul) { own["haul"] = baseHaul.DeepClone(); return; }
        foreach (var key in new[] { "hands", "delivery", "reachM", "fleet" })
            if (haul[key] is null && baseHaul[key] is { } value) haul[key] = value.DeepClone();
        MergeList(haul, baseHaul, "vehicles", "id");
        if (baseHaul["heft"] is JsonObject baseHeft)
        {
            var heft = haul["heft"] as JsonObject ?? (JsonObject)(haul["heft"] = new JsonObject());
            foreach (var (id, value) in baseHeft)
                if (!heft.ContainsKey(id) && value is not null) heft[id] = value.DeepClone();
        }
    }

    private static void MergeList(JsonObject own, JsonObject baseHome, string key, string idKey)
    {
        if (baseHome[key] is not JsonArray baseList) return;
        if (own[key] is not JsonArray list) { own[key] = baseList.DeepClone(); return; }
        var have = list.Select(o => o?[idKey]?.ToString()).ToHashSet();
        foreach (var item in baseList)
            if (item is JsonObject o && have.Add(o[idKey]?.ToString())) list.Add(o.DeepClone());
    }

    /// <summary>O bloco <c>Home</c>; ausente = nulo. Valida ids, objetos, categorias, recursos e números.</summary>
    public static Result<HomeCatalog?> Parse(JsonNode? node, IReadOnlyList<ObjectDescriptor> objects)
    {
        if (node is null) return Result<HomeCatalog?>.Ok(null);
        HomeCatalog? catalog;
        try { catalog = node.Deserialize<HomeCatalog>(Options); }
        catch (JsonException e) { return Fail($"Home: {e.Message}"); }
        if (catalog is null) return Fail("Home: precisa ser um objeto");

        var rules = catalog.Rules;
        if (rules.ResalePercent is < 0 or > 100 || rules.DeliveryMin < 0 || rules.OrderMarkupPercent < 0 || rules.MaxPieces is < 1 or > 500) return Fail("Home.rules: números fora do limite");
        var build = catalog.Build;
        if (build.Wall < 0 || build.Door < 0 || build.Demolish < 0 || build.Cell < 0 || build.MaxExtraCells is < 0 or > 64) return Fail("Home.build: preço negativo ou MaxExtraCells fora de 0 a 64");
        for (int i = 0; i < catalog.Categories.Count; i++)
        {
            var c = catalog.Categories[i];
            if (string.IsNullOrWhiteSpace(c.Id) || c.Name.Length is 0 or > 24 || catalog.Categories.Count(x => x.Id == c.Id) > 1) return Fail($"Home.categories[{i}]: {{id único, name (1 a 24 caracteres)}}");
        }

        for (int i = 0; i < catalog.Items.Count; i++)
        {
            var item = catalog.Items[i];
            var at = $"Home.items[{i}]";
            if (objects.All(o => o.Id != item.Object)) return Fail($"{at}.object: '{item.Object}' não existe em Objects");
            if (catalog.Items.Count(x => x.Object == item.Object) > 1) return Fail($"{at}.object: '{item.Object}' duplicado");
            if (catalog.Items.Count(x => x.Resource == item.Resource) > 1) return Fail($"{at}.resource: {item.Resource} já é de outro móvel");
            if (catalog.Categories.All(c => c.Id != item.Category)) return Fail($"{at}.category: '{item.Category}' não existe em Home.categories");
            if (item.Price < 0 || !double.IsFinite(item.Beauty) || Math.Abs(item.Beauty) > 50) return Fail($"{at}: preço negativo ou beleza fora de -50 a 50");
            if (item.Craft is { } craft && (craft.Hours < 0 || craft.Materials.Any(m => !int.TryParse(m.Key, out _) || m.Value < 0))) return Fail($"{at}.craft: horas e materiais (recurso → quantidade) válidos");
        }

        for (int i = 0; i < catalog.Paints.Count; i++)
        {
            var p = catalog.Paints[i];
            var at = $"Home.paints[{i}]";
            if (string.IsNullOrWhiteSpace(p.Id) || p.Name.Length is 0 or > 24 || catalog.Paints.Count(x => x.Id == p.Id) > 1) return Fail($"{at}: {{id único, name (1 a 24 caracteres)}}");
            if (!PaintKinds.Contains(p.Kind) || p.Color.Length != 6 || !p.Color.All(Uri.IsHexDigit)) return Fail($"{at}: kind floor|wall e color com 6 dígitos hexadecimais");
            if (p.Price < 0 || !double.IsFinite(p.Beauty) || Math.Abs(p.Beauty) > 50) return Fail($"{at}: preço negativo ou beleza fora de -50 a 50");
        }

        if (catalog.Haul is { } haul && HaulError(catalog, haul) is { } haulError) return Fail(haulError);
        return Result<HomeCatalog?>.Ok(catalog.Items.Count == 0 && catalog.Paints.Count == 0 ? null : catalog);
    }

    private static string? HaulError(HomeCatalog catalog, HomeHaul haul)
    {
        if (haul.Hands is null || haul.Delivery is null || haul.Vehicles is null || haul.Fleet is null || haul.Heft is null) return "Home.haul: precisa de hands, delivery, vehicles, fleet e heft";
        var hands = haul.Hands;
        if (!double.IsFinite(hands.MaxKg) || hands.MaxKg <= 0 || !double.IsFinite(hands.MaxLiters) || hands.MaxLiters <= 0 || hands.SlowPercent is < 0 or > 90 || hands.HelperFactor < 1) return "Home.haul.hands: {maxKg, maxLiters > 0, slowPercent 0 a 90, helperFactor >= 1}";
        var d = haul.Delivery;
        if (d.Fee < 0 || d.PerKg < 0 || d.OpenHour is < 0 or > 23 || d.CloseHour is < 1 or > 24 || d.CloseHour <= d.OpenHour || d.PrepMin < 0 || !double.IsFinite(d.Mps) || d.Mps <= 0) return "Home.haul.delivery: {fee, perKg >= 0, openHour < closeHour (0 a 24), prepMin >= 0, mps > 0}";
        if (!double.IsFinite(haul.ReachM) || haul.ReachM <= 0 || haul.ReachM > 30) return "Home.haul.reachM: de 0 a 30 m";
        for (int i = 0; i < haul.Vehicles.Count; i++)
        {
            var v = haul.Vehicles[i];
            if (string.IsNullOrWhiteSpace(v.Id) || haul.Vehicles.Count(x => x.Id == v.Id) > 1 || v.Name.Length is 0 or > 24 || v.TrunkKg <= 0 || v.TrunkLiters <= 0 || !double.IsFinite(v.Mps) || v.Mps <= 0 || v.Color.Length != 6 || !v.Color.All(Uri.IsHexDigit))
                return $"Home.haul.vehicles[{i}]: {{id único, name, trunkKg e trunkLiters > 0, mps > 0, color com 6 dígitos hexadecimais}}";
        }

        for (int i = 0; i < haul.Fleet.Count; i++)
            if (haul.Vehicle(haul.Fleet[i]) is null) return $"Home.haul.fleet[{i}]: veículo '{haul.Fleet[i]}' não existe em Home.haul.vehicles";
        for (int i = 0; i < catalog.Items.Count; i++)
            if (haul.HeftOf(catalog.Items[i].Object) is not { } heft || !double.IsFinite(heft.Kg) || heft.Kg <= 0 || !double.IsFinite(heft.Liters) || heft.Liters <= 0)
                return $"Home.haul.heft.{catalog.Items[i].Object}: o móvel precisa de peso (kg) e volume (litros) maiores que zero";
        return null;
    }

    private static Result<HomeCatalog?> Fail(string error) => Result<HomeCatalog?>.Fail(error);
}
