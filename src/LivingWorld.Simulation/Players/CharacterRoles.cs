using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Family;

namespace LivingWorld.Simulation.Players;

/// <summary>O que o papel escolhido permite (AD-197t): quem é o parente, em que casa a pessoa entra, quem seriam os pais e as idades
/// em que o laço é possível (as que os pais podiam ter gerado, o cônjuge ainda livre...). Só lê; nada aqui muda o mundo.</summary>
internal sealed record RolePlan(Npc? Anchor, IReadOnlyList<Npc> Parents, IReadOnlyList<int> Ages, Household? Home);

/// <summary>As regras dos laços do criador de personagem: filho e irmão exigem pais que podiam ter gerado a pessoa na idade pedida
/// (a janela fértil do mundo, em vida na hora do nascimento); cônjuge exige alguém livre e que aceite (a mesma regra do cortejo);
/// o resto é de quem existe e vive na cidade.</summary>
internal static class CharacterRoles
{
    private static Result<RolePlan> Refuse(string reason) => Result<RolePlan>.Fail($"{PlayerCommands.Conflict} {reason}");

    public static Result<RolePlan> Plan(WorldState world, City city, CharacterRole role)
    {
        int min = MaterializationPrior.AdultYears, max = world.PopulationRules.LifeTable.MaxLongevityYears - 1;
        var all = Enumerable.Range(min, Math.Max(0, max - min + 1)).ToList();
        if (!CharacterRole.Kinds.Contains(role.Kind)) return Refuse($"papel desconhecido: {role.Kind}");
        if (role.Kind == CharacterRole.Newcomer)
        {
            if (role.FromCityId is { } from && (from == city.Id.Value || world.FindCity(new CityId(from)) is null))
                return Refuse("a cidade de origem do recém-chegado não existe ou é a própria cidade de destino");
            return Result<RolePlan>.Ok(new RolePlan(null, [], all, null));
        }

        var anchor = role.RelativeId is { } id ? world.FindNpc(new NpcId(id)) : null;
        if (anchor is null || !anchor.IsAlive) return Refuse("o parente escolhido não existe ou não está vivo");
        if (anchor.City != city.Id || anchor.DisappearedAtTick is not null)
            return Refuse($"{anchor.Name} não vive em {city.Name} agora");
        var home = anchor.Household is { } h ? world.FindHousehold(h) : null;
        var now = world.CurrentDate;

        switch (role.Kind)
        {
            case CharacterRole.Relative:
                return Result<RolePlan>.Ok(new RolePlan(anchor, [], all, home));
            case CharacterRole.Spouse:
                if (!CourtshipSystem.IsSingle(anchor, world) || anchor.CourtingWith is not null)
                    return Refuse($"{anchor.Name} não está livre para casar");
                if (world.LifeStageRules.LifeStageOf(anchor.AgeYears(now)) is not (LifeStage.Adult or LifeStage.Elder))
                    return Refuse($"{anchor.Name} ainda não é adulto para casar");
                return Result<RolePlan>.Ok(new RolePlan(anchor, [], all, null));
        }

        var parents = new List<Npc>();
        if (role.Kind == CharacterRole.Child)
        {
            parents.Add(anchor);
            if (anchor.Spouse is { } spouseId && world.FindNpc(spouseId) is { IsAlive: true } spouse) parents.Add(spouse);
        }
        else
        {
            foreach (var parentId in new[] { anchor.MotherId, anchor.FatherId })
            {
                if (parentId is null) continue;
                if (world.FindNpc(parentId.Value) is not { } parent) return Refuse($"os pais de {anchor.Name} já saíram do registro vivo do mundo");
                parents.Add(parent);
            }
            if (parents.Count == 0)
                return Refuse($"{anchor.Name} não tem pais registrados: sem eles não há laço de irmandade (escolha outro papel)");
        }
        var ages = all.Where(a => parents.All(p => CouldHaveFathered(world, p, a))).ToList();
        if (ages.Count == 0)
            return Refuse(role.Kind == CharacterRole.Child
                ? $"{anchor.Name} não tem a idade certa para ter um filho adulto agora"
                : $"os pais de {anchor.Name} não podem ter gerado outra pessoa adulta agora");
        return Result<RolePlan>.Ok(new RolePlan(anchor, parents, ages, home));
    }

    /// <summary>O pai ou a mãe podia ter gerado alguém que hoje tem <paramref name="childAge"/> anos: estava na idade fértil do mundo
    /// ao nascimento dela e ainda vivo nessa hora.</summary>
    private static bool CouldHaveFathered(WorldState world, Npc parent, int childAge)
    {
        var now = world.CurrentDate;
        int atBirth = YearsSince(parent.BirthDate, now) - childAge;
        if (atBirth < 0 || !world.PopulationRules.IsMarriageableAge(parent.Sex, atBirth)) return false;
        return parent.DeathDate is not { } died || died >= now.AddYears(-childAge);
    }

    public static int YearsSince(WorldDate from, WorldDate now) => (int)((now.TotalHours - from.TotalHours) / now.Calendar.HoursPerYear);

    /// <summary>O cônjuge aceita? A mesma regra do cortejo: sem parentesco próximo, na idade de casar, e cada um quer o outro (a
    /// preferência dele pesa a aparência, a idade, a saúde, os bens e o nome) acima da exigência dele. Recusa diz o motivo.</summary>
    public static Result<Unit> SpouseAccepts(WorldState world, Npc anchor, Npc person)
    {
        var now = world.CurrentDate;
        var rules = world.PopulationRules;
        if (CourtshipSystem.Reject(world, anchor, person, now, rules, null) is { } reason)
            return Result<Unit>.Fail($"{PlayerCommands.Conflict} {anchor.Name} não pode casar com essa pessoa: " + reason switch
            {
                CourtshipRejectionReason.Incesto => "são parentes próximos",
                CourtshipRejectionReason.ForaDaFaixaEtaria => "o sexo ou a idade não permitem o casamento",
                _ => reason.ToString(),
            });
        var familyRules = world.FamilyRules;
        double wanted = CourtshipSystem.Preference(anchor, person, null, familyRules, rules, now)
            * CourtshipSystem.RegardSignal(world, person, now.TotalHours);
        if (familyRules.Names.NameSignalOnAcceptance) wanted *= CourtshipSystem.NameSignal(world, anchor, person, familyRules, null);
        if (wanted < CourtshipSystem.ThresholdOf(world, anchor))
            return Result<Unit>.Fail($"{PlayerCommands.Conflict} {anchor.Name} não aceita: sem afinidade com essa pessoa");
        return Result<Unit>.Ok(Unit.Value);
    }
}
