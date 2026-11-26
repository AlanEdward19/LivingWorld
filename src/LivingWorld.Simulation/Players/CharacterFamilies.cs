using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Players;

/// <summary>Por quem um laço passa (<see cref="CharacterRole.ViaId"/>) e as idades que ele permite.</summary>
public sealed record ViaOption(long NpcId, string Name, int AgeYears, int MinAge, int MaxAge);

/// <summary>O que o criador pode fazer com um parente num laço: se dá, em que idades (a menor idade pode ser de menor de idade, com
/// quem cuide) e, nos laços que passam por alguém, por quem; ou o motivo de não dar.</summary>
public sealed record RoleOption(bool Available, int MinAge, int MaxAge, string Reason, IReadOnlyList<ViaOption> Vias);

public sealed record FamilyMemberOption(long NpcId, string Name, Sex Sex, int AgeYears, long SpouseId, IReadOnlyDictionary<string, RoleOption> Roles);

public sealed record FamilyOption(long HouseholdId, string Head, IReadOnlyList<FamilyMemberOption> Members);

/// <summary>As famílias da cidade e, para cada membro, os laços que o criador aceita (AD-197t/t2). Só lê. Uma varredura do mundo
/// (<see cref="KinIndex"/>) por pedido; o custo cresce com a cidade, por isso a lista tem teto (<see cref="MaxFamilies"/>, as maiores
/// primeiro) e cada laço oferece no máximo <see cref="MaxVias"/> parentes de passagem.</summary>
public static class CharacterFamilies
{
    public const int MaxFamilies = 120;
    public const int MaxVias = 8;

    /// <summary>Menor idade de menor de idade que o criador admite (com quem cuide).</summary>
    public const int MinMinorAge = CharacterRoles.MinMinorAge;

    public static IReadOnlyList<FamilyOption> List(WorldState world, City city)
    {
        var kin = KinIndex.Of(world);
        var households = world.Households.Where(h => h.City == city.Id)
            .Select(h => (Household: h, Members: h.Members.Select(world.FindNpc).Where(n => n is { IsAlive: true }).Cast<Npc>().ToList()))
            .Where(h => h.Members.Count > 0)
            .OrderByDescending(h => h.Members.Count).ThenBy(h => h.Household.Id.Value).Take(MaxFamilies).ToList();
        var kinds = CharacterRole.Kinds.Where(k => k != CharacterRole.Newcomer).ToList();
        return households.Select(h => new FamilyOption(h.Household.Id.Value, world.FindNpc(h.Household.Head)?.Name ?? "",
            h.Members.OrderBy(m => m.Id.Value).Select(m => new FamilyMemberOption(m.Id.Value, m.Name, m.Sex, m.AgeYears(world.CurrentDate),
                m.Spouse?.Value ?? 0, kinds.ToDictionary(k => k, k => Option(world, city, k, m, kin)))).ToList())).ToList();
    }

    /// <summary>O laço <paramref name="kind"/> com <paramref name="anchor"/>: nos que passam por alguém, o menor e o maior das idades de
    /// todos os parentes de passagem que servem, e a lista deles.</summary>
    private static RoleOption Option(WorldState world, City city, string kind, Npc anchor, KinIndex kin)
    {
        if (!CharacterRole.NeedsVia(kind))
        {
            var plan = CharacterRoles.Plan(world, city, new CharacterRole(kind, anchor.Id.Value), kin);
            return plan.IsSuccess ? new RoleOption(true, plan.Value!.Ages[0], plan.Value.Ages[^1], "", [])
                : new RoleOption(false, 0, 0, Reason(plan.Error!), []);
        }
        var vias = new List<ViaOption>();
        string reason = "nenhum parente registrado serve de passagem para esse laço";
        foreach (var candidate in CharacterRoles.ViaCandidates(kind, anchor, kin))
        {
            if (vias.Count >= MaxVias) break;
            var plan = CharacterRoles.Plan(world, city, new CharacterRole(kind, anchor.Id.Value, ViaId: candidate.Id.Value), kin);
            if (plan.IsSuccess)
                vias.Add(new ViaOption(candidate.Id.Value, candidate.Name, candidate.AgeYears(world.CurrentDate), plan.Value!.Ages[0], plan.Value.Ages[^1]));
            else
                reason = Reason(plan.Error!);
        }
        return vias.Count == 0 ? new RoleOption(false, 0, 0, reason, [])
            : new RoleOption(true, vias.Min(v => v.MinAge), vias.Max(v => v.MaxAge), "", vias);
    }

    private static string Reason(string error) => error.Replace(PlayerCommands.Conflict, "").Trim();
}
