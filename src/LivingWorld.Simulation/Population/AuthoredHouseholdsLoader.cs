using System.Text.Json.Nodes;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Population;

/// <summary>Lê o bloco opcional <c>AuthoredHouseholds</c> do cenário e valida tudo na borda: o erro
/// nomeia o campo. Profissão e assentamento vêm do catálogo/mapa do próprio cenário, nunca de enum em C#.</summary>
public static class AuthoredHouseholdsLoader
{
    private static readonly string[] TraitNames =
        ["Extroversion", "Agreeableness", "Conscientiousness", "EmotionalStability", "Openness", "Ambition", "Loyalty", "Altruism", "Impulsivity", "RiskAversion"];

    public static Result<IReadOnlyList<AuthoredHousehold>> Load(
        JsonObject root, PopulationCatalog catalog, int maxLongevityYears, IReadOnlyCollection<string> settlementNames)
    {
        if (root["AuthoredHouseholds"] is null) return Result<IReadOnlyList<AuthoredHousehold>>.Ok([]);
        if (root["AuthoredHouseholds"] is not JsonArray familiesNode)
            return Fail("AuthoredHouseholds: precisa ser uma lista");

        var families = new List<AuthoredHousehold>();
        for (int f = 0; f < familiesNode.Count; f++)
        {
            string at = $"AuthoredHouseholds[{f}]";
            if (familiesNode[f] is not JsonObject family) return Fail($"{at}: precisa ser um objeto");
            string? settlement = family["Settlement"]?.GetValue<string>();
            if (settlement is not null && !settlementNames.Contains(settlement))
                return Fail($"{at}.Settlement: '{settlement}' não existe no mapa");
            if (family["Members"] is not JsonArray membersNode || membersNode.Count == 0)
                return Fail($"{at}.Members: precisa de ao menos um membro");

            var members = new List<AuthoredPerson>();
            for (int m = 0; m < membersNode.Count; m++)
            {
                var person = ReadPerson(membersNode[m], $"{at}.Members[{m}]", membersNode.Count, catalog, maxLongevityYears);
                if (!person.IsSuccess) return Fail(person.Error!);
                members.Add(person.Value!);
            }
            var link = ValidateLinks(members, at);
            if (link is not null) return Fail(link);
            if (!members.Any(p => p.AgeYears >= 18)) return Fail($"{at}.Members: a família precisa de ao menos um adulto");
            families.Add(new AuthoredHousehold(settlement, members));
        }
        return Result<IReadOnlyList<AuthoredHousehold>>.Ok(families);
    }

    private static Result<AuthoredPerson> ReadPerson(
        JsonNode? node, string at, int memberCount, PopulationCatalog catalog, int maxLongevityYears)
    {
        if (node is not JsonObject o) return Result<AuthoredPerson>.Fail($"{at}: precisa ser um objeto");
        string name = o["Name"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(name)) return Result<AuthoredPerson>.Fail($"{at}.Name: obrigatório");
        if (!Enum.TryParse<Sex>(o["Sex"]?.GetValue<string>(), ignoreCase: true, out var sex))
            return Result<AuthoredPerson>.Fail($"{at}.Sex: use Female ou Male");
        if (o["AgeYears"]?.GetValue<int>() is not { } age || age < 0 || age >= maxLongevityYears)
            return Result<AuthoredPerson>.Fail($"{at}.AgeYears: fora de [0,{maxLongevityYears - 1}]");
        if (o["ProfessionId"]?.GetValue<int>() is not { } profession || !catalog.ProfessionIds.Contains(profession))
            return Result<AuthoredPerson>.Fail($"{at}.ProfessionId: fora do catálogo do período");

        Personality? personality = null;
        if (o["Personality"] is JsonObject traits)
        {
            var v = new int[10];
            for (int i = 0; i < 10; i++)
                if (traits[TraitNames[i]]?.GetValue<int>() is { } value) v[i] = value;
                else return Result<AuthoredPerson>.Fail($"{at}.Personality.{TraitNames[i]}: obrigatório");
            var created = Personality.Create(v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8], v[9]);
            if (!created.IsSuccess) return Result<AuthoredPerson>.Fail($"{at}.Personality.{created.Error}");
            personality = created.Value;
        }

        int? spouse = o["Spouse"]?.GetValue<int>(), mother = o["Mother"]?.GetValue<int>(), father = o["Father"]?.GetValue<int>();
        foreach (var (label, index) in new[] { ("Spouse", spouse), ("Mother", mother), ("Father", father) })
            if (index is { } i && (i < 0 || i >= memberCount))
                return Result<AuthoredPerson>.Fail($"{at}.{label}: índice {i} não existe na família");
        return Result<AuthoredPerson>.Ok(new AuthoredPerson(name, sex, age, profession, personality, spouse, mother, father));
    }

    private static string? ValidateLinks(List<AuthoredPerson> members, string at)
    {
        for (int i = 0; i < members.Count; i++)
        {
            var p = members[i];
            if (p.Spouse is { } s && (s == i || members[s].Spouse != i))
                return $"{at}.Members[{i}].Spouse: o vínculo precisa ser recíproco e com outra pessoa";
            if (p.Mother is { } m && (m == i || members[m].Sex != Sex.Female || members[m].AgeYears <= p.AgeYears))
                return $"{at}.Members[{i}].Mother: precisa ser uma mulher mais velha";
            if (p.Father is { } fa && (fa == i || members[fa].Sex != Sex.Male || members[fa].AgeYears <= p.AgeYears))
                return $"{at}.Members[{i}].Father: precisa ser um homem mais velho";
        }
        return null;
    }

    private static Result<IReadOnlyList<AuthoredHousehold>> Fail(string error) => Result<IReadOnlyList<AuthoredHousehold>>.Fail(error);
}
