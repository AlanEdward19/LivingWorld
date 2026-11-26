using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Family;

namespace LivingWorld.Simulation.Players;

/// <summary>O que o papel escolhido permite (AD-197t/t2): quem é o parente, por quem o laço passa (<see cref="Via"/>), em que casa a
/// pessoa entra, quem seriam os pais e as idades em que o laço é possível (as que os pais podiam ter gerado, o cônjuge ainda livre,
/// menor só com quem cuide). Só lê; nada aqui muda o mundo.</summary>
internal sealed record RolePlan(Npc? Anchor, IReadOnlyList<Npc> Parents, IReadOnlyList<int> Ages, Household? Home, Npc? Via = null);

/// <summary>Quem é filho de quem, pelos pais registrados (<see cref="Npc.MotherId"/>/<see cref="Npc.FatherId"/>): o elo de toda a
/// genealogia do criador (neto, sobrinho, tio, primo). Montado por pedido, numa varredura; ninguém guarda.</summary>
public sealed class KinIndex
{
    private static readonly List<Npc> None = [];
    private readonly Dictionary<long, List<Npc>> _children = [];
    private readonly WorldState _world;

    private KinIndex(WorldState world)
    {
        _world = world;
        foreach (var npc in world.Npcs)
        {
            if (npc.MotherId is { } mother) Add(mother.Value, npc);
            if (npc.FatherId is { } father) Add(father.Value, npc);
        }
    }

    public static KinIndex Of(WorldState world) => new(world);

    private void Add(long parent, Npc child)
    {
        if (!_children.TryGetValue(parent, out var list)) _children[parent] = list = [];
        list.Add(child);
    }

    public IReadOnlyList<Npc> ChildrenOf(NpcId parent) => _children.TryGetValue(parent.Value, out var list) ? list : None;

    /// <summary>Irmãos (inteiros ou meios): quem divide ao menos um pai registrado.</summary>
    public IEnumerable<Npc> SiblingsOf(Npc person)
    {
        var seen = new HashSet<long> { person.Id.Value };
        foreach (var parent in new[] { person.MotherId, person.FatherId })
            if (parent is { } id)
                foreach (var child in ChildrenOf(id))
                    if (seen.Add(child.Id.Value)) yield return child;
    }

    /// <summary>Os pais de <paramref name="person"/> que ainda estão no registro do mundo.</summary>
    public IEnumerable<Npc> ParentsOf(Npc person)
    {
        foreach (var parent in new[] { person.MotherId, person.FatherId })
            if (parent is { } id && _world.FindNpc(id) is { } found) yield return found;
    }
}

/// <summary>As regras dos laços do criador de personagem. Filho, neto, sobrinho e primo nascem de pais que existem (<c>ViaId</c>:
/// o filho do parente, o irmão dele, o tio ou a tia dele), que podiam tê-lo gerado na idade pedida (janela fértil, vivos ao
/// nascimento); irmão e tio dividem os pais registrados (de quem se escolheu, ou do pai ou da mãe dele). Menor (a partir de
/// <see cref="MinMinorAge"/>) só entra onde há adulto vivo que cuide. Cônjuge exige alguém livre que aceite (a regra do cortejo).
/// Nenhum antepassado é inventado: sem pais no registro não há irmandade (ADR-0053, emenda t2).</summary>
internal static class CharacterRoles
{
    /// <summary>Menor idade que o criador admite (criança que já anda e fala); lactente segue só por nascimento.</summary>
    public const int MinMinorAge = 6;

    private static Result<RolePlan> Refuse(string reason) => Result<RolePlan>.Fail($"{PlayerCommands.Conflict} {reason}");

    private static IReadOnlyList<Npc> Couple(WorldState world, Npc person) =>
        person.Spouse is { } id && world.FindNpc(id) is { IsAlive: true } spouse ? [person, spouse] : [person];

    public static Result<RolePlan> Plan(WorldState world, City city, CharacterRole role, KinIndex? kin = null)
    {
        int max = world.PopulationRules.LifeTable.MaxLongevityYears - 1;
        var adults = Enumerable.Range(MaterializationPrior.AdultYears, Math.Max(0, max - MaterializationPrior.AdultYears + 1)).ToList();
        if (!CharacterRole.Kinds.Contains(role.Kind)) return Refuse($"papel desconhecido: {role.Kind}");
        if (role.Kind == CharacterRole.Newcomer)
        {
            if (role.FromCityId is { } from && (from == city.Id.Value || world.FindCity(new CityId(from)) is null))
                return Refuse("a cidade de origem do recém-chegado não existe ou é a própria cidade de destino");
            return Result<RolePlan>.Ok(new RolePlan(null, [], adults, null));
        }

        var anchor = role.RelativeId is { } id ? world.FindNpc(new NpcId(id)) : null;
        if (anchor is null || !anchor.IsAlive) return Refuse("o parente escolhido não existe ou não está vivo");
        if (anchor.City != city.Id || anchor.DisappearedAtTick is not null)
            return Refuse($"{anchor.Name} não vive em {city.Name} agora");
        var anchorHome = anchor.Household is { } h ? world.FindHousehold(h) : null;
        var now = world.CurrentDate;

        switch (role.Kind)
        {
            case CharacterRole.Relative:
                return Result<RolePlan>.Ok(new RolePlan(anchor, [], adults, anchorHome));
            case CharacterRole.Spouse:
                if (!CourtshipSystem.IsSingle(anchor, world) || anchor.CourtingWith is not null)
                    return Refuse($"{anchor.Name} não está livre para casar");
                if (world.LifeStageRules.LifeStageOf(anchor.AgeYears(now)) is not (LifeStage.Adult or LifeStage.Elder))
                    return Refuse($"{anchor.Name} ainda não é adulto para casar");
                return Result<RolePlan>.Ok(new RolePlan(anchor, [], adults, null));
        }

        kin ??= KinIndex.Of(world);
        Npc? via = null;
        IReadOnlyList<Npc> parents;
        Household? careHome;
        if (role.Kind == CharacterRole.Child)
        {
            parents = Couple(world, anchor);
            careHome = anchorHome;
        }
        else if (role.Kind is CharacterRole.Grandchild or CharacterRole.Nephew or CharacterRole.Cousin)
        {
            var picked = ViaOf(world, city, role, anchor, kin);
            if (!picked.IsSuccess) return Result<RolePlan>.Fail(picked.Error!);
            via = picked.Value!;
            parents = Couple(world, via);
            careHome = via.Household is { } viaHome ? world.FindHousehold(viaHome) : null;
        }
        else // irmão (pais do parente) e tio (pais do pai ou da mãe do parente)
        {
            var of = anchor;
            if (role.Kind == CharacterRole.Uncle)
            {
                var picked = ViaOf(world, city, role, anchor, kin);
                if (!picked.IsSuccess) return Result<RolePlan>.Fail(picked.Error!);
                via = of = picked.Value!;
            }
            var found = new List<Npc>();
            foreach (var parentId in new[] { of.MotherId, of.FatherId })
            {
                if (parentId is null) continue;
                if (world.FindNpc(parentId.Value) is not { } parent)
                    return Refuse($"os pais de {of.Name} já saíram do registro vivo do mundo");
                found.Add(parent);
            }
            if (found.Count == 0)
                return Refuse($"{of.Name} não tem pais registrados: o motor não inventa antepassados, então não há como ser "
                    + (role.Kind == CharacterRole.Uncle ? "tio ou tia dessa pessoa" : "irmão ou irmã dela") + " (escolha outro laço)");
            parents = found;
            careHome = anchorHome;
        }

        bool minorsOk = role.Kind != CharacterRole.Uncle && careHome is not null && HasCaringAdult(world, careHome);
        var candidates = minorsOk ? Enumerable.Range(MinMinorAge, Math.Max(0, max - MinMinorAge + 1)) : adults;
        var ages = candidates.Where(a => parents.All(p => CouldHaveFathered(world, p, a))).ToList();
        if (ages.Count == 0)
            return Refuse($"{anchor.Name} e os parentes por onde o laço passa não podem ter gerado outra pessoa agora (idade dos pais)");
        return Result<RolePlan>.Ok(new RolePlan(anchor, parents, ages, careHome, via));
    }

    /// <summary>Menor só mora onde há um adulto vivo para cuidar: a dependência que vale para toda criança.</summary>
    private static bool HasCaringAdult(WorldState world, Household home) =>
        home.Members.Any(m => world.FindNpc(m) is { IsAlive: true } n && n.AgeYears(world.CurrentDate) >= MaterializationPrior.AdultYears);

    /// <summary>Por quem o laço passa: neto (um filho do parente), sobrinho (um irmão dele), primo (um irmão do pai ou da mãe dele),
    /// tio (o pai ou a mãe dele). O escolhido (<c>ViaId</c>) tem de ter esse parentesco registrado e, salvo no tio, viver na cidade.</summary>
    private static Result<Npc> ViaOf(WorldState world, City city, CharacterRole role, Npc anchor, KinIndex kin)
    {
        if (role.ViaId is not { } id)
            return Result<Npc>.Fail($"{PlayerCommands.Conflict} escolha por qual parente o laço passa (ViaId)");
        var via = ViaCandidates(role.Kind, anchor, kin).FirstOrDefault(c => c.Id.Value == id);
        if (via is null)
            return Result<Npc>.Fail($"{PlayerCommands.Conflict} esse parente não tem o parentesco registrado com {anchor.Name} para esse laço");
        if (role.Kind != CharacterRole.Uncle && (!via.IsAlive || via.City != city.Id || via.DisappearedAtTick is not null))
            return Result<Npc>.Fail($"{PlayerCommands.Conflict} {via.Name} não vive em {city.Name} agora: a criança precisa de pais vivos na cidade");
        return Result<Npc>.Ok(via);
    }

    /// <summary>Os possíveis parentes de passagem (sem checar vida nem cidade): filhos do parente (neto), irmãos dele (sobrinho), irmãos
    /// de seus pais (primo), seus pais com pais registrados (tio).</summary>
    public static IEnumerable<Npc> ViaCandidates(string kind, Npc anchor, KinIndex kin) => kind switch
    {
        CharacterRole.Grandchild => kin.ChildrenOf(anchor.Id),
        CharacterRole.Nephew => kin.SiblingsOf(anchor),
        CharacterRole.Cousin => kin.ParentsOf(anchor).SelectMany(p => kin.SiblingsOf(p)).DistinctBy(n => n.Id.Value),
        CharacterRole.Uncle => kin.ParentsOf(anchor).Where(p => p.MotherId is not null || p.FatherId is not null),
        _ => [],
    };

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
