using System.Text.Json.Nodes;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>#258: o período do sandbox agora traz também o mercado do bairro e o comerciante que o abastece (<c>Seu Joaquim</c>). Os testes de vida em casa (necessidades,
/// objetos, interações do casal) querem só o casal: esta edição tira o comerciante e o mercado do JSON, sem mudar mais nada do cenário.</summary>
public static class SandboxFixtures
{
    public static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln"))) dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado");
    }

    public static JsonObject Parse() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "scenarios", "periods", "sandbox-casal.json")))!.AsObject();

    /// <summary>Só o casal numa casa: sem o mercado, sem o comerciante e sem a rua pavimentada nem o quintal limpo (o que mexe no sorteio do lote).</summary>
    public static JsonObject CoupleOnly(JsonObject root)
    {
        root["Workplaces"] = new JsonArray();
        root["MarketLocationTypeIds"] = new JsonArray();
        root.Remove("CareerStart"); // #263: o emprego do Rafael mora num empregador que o casal sozinho não tem
        root["CatalogExtends"] = new JsonArray("home-life", "social", "moods"); // sem carreiras nem contas (#263): os testes de vida em casa não as querem; o humor (#262) fica
        var furnished = root["Furnishings"]!["-1"]!.AsArray();
        foreach (var node in furnished.Where(n => n!.ToString() == "newspaper").ToList()) furnished.Remove(node);
        var households = root["AuthoredHouseholds"]!.AsArray();
        while (households.Count > 1) households.RemoveAt(households.Count - 1);
        return root;
    }
}
