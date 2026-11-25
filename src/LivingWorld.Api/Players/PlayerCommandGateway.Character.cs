using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;

namespace LivingWorld.Api.Players;

/// <summary>AD-197t: o criador de personagem de verdade — o que a tela pode escolher (famílias da cidade, faixas, teto de dinheiro), a
/// prévia validada pelo motor (só lê, nada vai ao registro) e a pessoa como o motor a tem.</summary>
public sealed partial class PlayerCommandGateway
{
    /// <summary>No máximo estas famílias por cidade na lista da tela (as maiores primeiro); o resto some só da lista, não do mundo.</summary>
    private const int MaxFamilies = 120;

    /// <summary>Prévia: a pessoa que a escolha daria, validada e com o que ficou em branco sorteado pela mesma chave. Não registra.</summary>
    public PlayerReply Preview(string playerId, CharacterSpec spec)
    {
        lock (library.Gate)
        {
            var world = host.Current;
            return Map(CharacterBuilder.Resolve(world, playerId, spec),
                r => new { character = CharacterView(world, r.Npc, r.Role, r.Money, r.Anchor) });
        }
    }

    /// <summary>O que o criador oferece numa cidade: faixas de idade, altura por sexo, teto de dinheiro, ofícios, trajes, as cidades
    /// de onde se pode chegar e as famílias com, para cada membro, os papéis possíveis (e a faixa de idade de cada laço). Só lê.</summary>
    public PlayerReply CityOptions(Guid cityId)
    {
        lock (library.Gate)
        {
            var world = host.Current;
            if (world.FindActiveCity(new CityId(cityId)) is not { } city)
                return new PlayerReply(StatusCodes.Status404NotFound, null, $"{PlayerCommands.NotFound} cidade ausente");
            var names = Names?.For(world.PeriodId);
            var growth = world.BodyRules.Growth;
            object Height(Sex sex) => growth.Enabled
                ? new
                {
                    min = Math.Round(growth.AdultHeightMean(sex) - 3.5 * growth.AdultHeightStdDev(sex), 2),
                    max = Math.Round(growth.AdultHeightMean(sex) + 3.5 * growth.AdultHeightStdDev(sex), 2),
                    typical = Math.Round(growth.AdultHeightMean(sex), 2)
                }
                : new { min = 1.40, max = 2.10, typical = sex == Sex.Male ? 1.75 : 1.65 };
            var households = world.Households.Where(h => h.City == city.Id)
                .Select(h => (Household: h, Members: h.Members.Select(world.FindNpc).Where(n => n is { IsAlive: true }).Cast<Npc>().ToList()))
                .Where(h => h.Members.Count > 0).OrderByDescending(h => h.Members.Count).ThenBy(h => h.Household.Id.Value).Take(MaxFamilies).ToList();
            return new PlayerReply(StatusCodes.Status200OK, new
            {
                cityId = city.Id.Value,
                name = city.Name,
                ageMin = MaterializationPrior.AdultYears,
                ageMax = world.PopulationRules.LifeTable.MaxLongevityYears - 1,
                economy = world.EconomyRules.Enabled,
                typicalMoney = world.EconomyRules.Enabled ? CharacterBuilder.TypicalWealth(world, city) : 0,
                moneyCap = world.EconomyRules.Enabled ? CharacterBuilder.MoneyCap(world, city) : 0,
                heightM = new { female = Height(Sex.Female), male = Height(Sex.Male) },
                outfits = Appearance.OutfitNames,
                professions = world.PopulationCatalog.ProfessionIds.Order().Select(id => new { id, name = names?.Profession(id) ?? "" }).ToList(),
                roles = CharacterRole.Kinds,
                origins = world.ActiveCities().Where(c => c.Id != city.Id).OrderBy(c => c.Name, StringComparer.Ordinal)
                    .Select(c => new { cityId = c.Id.Value, name = c.Name }).ToList(),
                families = households.Select(h => new
                {
                    householdId = h.Household.Id.Value,
                    head = world.FindNpc(h.Household.Head)?.Name ?? "",
                    members = h.Members.OrderBy(m => m.Id.Value).Select(m => new
                    {
                        npcId = m.Id.Value,
                        name = m.Name,
                        sex = m.Sex.ToString(),
                        ageYears = m.AgeYears(world.CurrentDate),
                        spouseId = m.Spouse?.Value ?? 0,
                        roles = CharacterRole.Kinds.Where(k => k != CharacterRole.Newcomer).ToDictionary(k => k, k => RoleOption(world, city, k, m.Id.Value)),
                    }).ToList(),
                }).ToList(),
            }, null);
        }
    }

    private static object RoleOption(WorldState world, LivingWorld.Domain.Cities.City city, string kind, long relativeId)
    {
        var window = CharacterBuilder.AgeWindow(world, city, new CharacterRole(kind, relativeId));
        return window.IsSuccess
            ? new { available = true, minAge = window.Value.Min, maxAge = window.Value.Max, reason = "" }
            : new { available = false, minAge = 0, maxAge = 0, reason = window.Error!.Replace(PlayerCommands.Conflict, "").Trim() };
    }

    /// <summary>A pessoa como o motor a tem (ou a teria): o que o cliente desenha e mostra vem daqui, nunca da escolha local.</summary>
    internal object CharacterView(WorldState world, Npc npc, CharacterRole role, long money, Npc? relative)
    {
        var names = Names?.For(world.PeriodId);
        var look = npc.Appearance;
        return new
        {
            name = npc.Name,
            sex = npc.Sex.ToString(),
            ageYears = npc.AgeYears(world.CurrentDate),
            cityId = npc.City.Value,
            cityName = world.FindCity(npc.City)?.Name ?? "",
            professionId = npc.Profession.Id,
            profession = names?.Profession(npc.Profession.Id) ?? "",
            cultureId = npc.Culture.Id,
            health = npc.Health,
            money,
            personality = npc.Personality,
            role = new { kind = role.Kind, relativeId = relative?.Id.Value ?? 0, relativeName = relative?.Name ?? "", fromCityId = role.FromCityId?.ToString() ?? "" },
            look = new
            {
                heightM = Math.Round(npc.Height, 3),
                weightKg = Math.Round(npc.Weight, 1),
                skinTone = look?.SkinTone ?? 50,
                hairTone = look?.HairTone ?? 50,
                hairCurl = look?.HairCurl ?? 50,
                build = look?.Build ?? 50,
                eyeTone = look?.EyeTone ?? 50,
                faceShape = look?.FaceShape ?? 50,
                outfit = look?.Outfit ?? 0,
            },
            outfitName = look?.Outfit is { } o && o >= 0 && o < Appearance.OutfitNames.Count ? Appearance.OutfitNames[o] : "",
            household = npc.Household?.Value ?? 0,
        };
    }
}
