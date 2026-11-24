using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Population.Family;

/// <summary>Só casa quem tem do que viver (AD-158, <see cref="HoldingRules"/>). A terra demarcada das fazendas da cidade
/// (<c>CropBatch.ArableSeed</c>, AD-149) é a de um ano de semente; quem mora numa família que vive da terra (ninguém dela num
/// ofício fora da lavoura) come <see cref="HoldingRules.PersonSownHectares"/> dela. Sobrando terra para uma posse nova
/// (<see cref="HoldingRules.HoldingSownHectares"/>), há posse vaga. O casal tem sustento se: um dos dois tem ofício fora da
/// lavoura com salário (artesão), ou chefia a própria casa e sai dela levando só os filhos (a posse vai com ele), ou há posse
/// vaga. Leitura pura do mundo, nenhum estado novo: a terra volta quando a família encolhe ou se desfaz (morte, partida).
/// Terra sem limite (0) = sem freio.</summary>
public static class Holdings
{
    public static HoldingRules? Rules(WorldState world) =>
        world.EconomyRules.Enabled ? world.FamilyRules.Pressure?.Holdings : null;

    /// <summary>Semente de um ano que a terra das fazendas da cidade toma, ou nulo se a terra não tem limite.</summary>
    public static long? Arable(WorldState world, CityId city)
    {
        long arable = 0;
        foreach (var workplace in world.Workplaces)
            if (workplace.City == city && world.FindCropAt(workplace.Location) is { ArableSeed: > 0 } field) arable += field.ArableSeed;
        return arable > 0 ? arable : null;
    }

    public static double SeedPerHolding(WorldState world, HoldingRules rules) => rules.HoldingSownHectares * world.AgricultureRules.SeedPerHectare;

    /// <summary>Terra em uso (semente) pelas famílias da cidade que vivem da terra: cada morador come
    /// <see cref="HoldingRules.PersonSownHectares"/>; a família com casal em idade de ter filhos guarda uma posse inteira (os
    /// filhos que virão). A casa de velhos ou de viúva que encolhe devolve a terra que não come.</summary>
    public static double LandInUse(WorldState world, CityId city, HoldingRules rules)
    {
        double perPerson = rules.PersonSownHectares * world.AgricultureRules.SeedPerHectare;
        double perHolding = SeedPerHolding(world, rules);
        int fertileMax = world.PopulationRules.FertilityMaxAge;
        double used = 0;
        foreach (var household in world.Households)
        {
            if (household.City != city || household.IsEmpty) continue;
            int living = 0;
            bool crafts = false, couple = false;
            foreach (var id in household.Members)
            {
                if (world.FindNpc(id) is not { IsAlive: true } member) continue;
                living++;
                if (HasCraft(world, member)) crafts = true;
                if (member.Sex == Sex.Female && member.Spouse is { } spouse && member.AgeYears(world.CurrentDate) <= fertileMax
                    && world.FindNpc(spouse) is { IsAlive: true }) couple = true;
            }

            if (!crafts) used += couple ? Math.Max(perHolding, living * perPerson) : living * perPerson;
        }

        return used;
    }

    /// <summary>Posses novas que a terra que sobra comporta (negativo = gente demais na terra); <see cref="int.MaxValue"/>
    /// sem a regra ou sem limite de terra.</summary>
    public static int Vacant(WorldState world, CityId city)
    {
        if (Rules(world) is not { } rules || Arable(world, city) is not { } arable) return int.MaxValue;
        return (int)Math.Floor((arable - LandInUse(world, city, rules)) / SeedPerHolding(world, rules));
    }

    /// <summary>Ofício fora da lavoura que paga salário: o artesão monta casa sem terra.</summary>
    public static bool HasCraft(WorldState world, Npc npc) =>
        npc.Employer is { } employer && world.FindWorkplace(employer) is { } workplace
        && world.FindCropAt(workplace.Location) is null
        && world.EconomyRules.WageByProfession.GetValueOrDefault(npc.Profession.Id) > 0;

    /// <summary>O casal tem do que viver: ofício fora da lavoura, a casa que um dos dois chefia (e deixa, levando os
    /// filhos), ou uma posse vaga na cidade.</summary>
    /// <remarks><paramref name="vacantMemo"/>: posses vagas por cidade já contadas neste tick (o cortejo anual pergunta para
    /// cada par e nada casa durante a rodada; só vale dentro dela).</remarks>
    public static bool HasLivelihood(WorldState world, Npc a, Npc b, Dictionary<CityId, int>? vacantMemo = null)
    {
        if (Rules(world) is null) return true;
        if (HasCraft(world, a) || HasCraft(world, b)) return true;
        if (HoldsOwnHome(world, a) || HoldsOwnHome(world, b)) return true;
        if (vacantMemo is null) return Vacant(world, a.City) > 0;
        if (!vacantMemo.TryGetValue(a.City, out int vacant)) vacantMemo[a.City] = vacant = Vacant(world, a.City);
        return vacant > 0;
    }

    /// <summary>Chefia a casa e os outros moradores são só filhos menores dele, que vão junto: a posse muda com ele.</summary>
    private static bool HoldsOwnHome(WorldState world, Npc npc)
    {
        if (npc.Household is not { } id || world.FindHousehold(id) is not { } home || home.Head != npc.Id) return false;
        int childMax = world.LifeStageRules.ChildMaxAge;
        foreach (var memberId in home.Members)
        {
            if (memberId == npc.Id) continue;
            if (world.FindNpc(memberId) is not { IsAlive: true } m) continue;
            bool ownChild = m.MotherId == npc.Id || m.FatherId == npc.Id;
            if (!ownChild || m.AgeYears(world.CurrentDate) > childMax) return false;
        }

        return true;
    }
}
