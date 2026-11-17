using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Population.Naming;

/// <summary>Nome próprio e de família de toda pessoa (AD-178), em toda origem: fundador semeado, recém-nascido, materializado.
/// Família autorada (cenário ou jogador) mantém o nome escolhido. Cada pessoa sorteia num stream próprio e não guardado
/// (<c>StreamFor(propósito, id)</c>), então nomear não desloca nenhum outro sorteio do mundo.
/// <para>Regras declaradas: o filho de casal casado leva o nome de família da linhagem da cultura (pai ou mãe); filho fora do
/// casamento leva o da mãe; o órfão acolhido mantém o nome com que nasceu; com o costume de adotar o nome, quem casa fora da
/// linhagem passa a usar o nome de família do cônjuge (vale o costume da cultura do chefe do novo lar, o de menor id); nunca
/// dois vivos com o mesmo nome completo na mesma casa.</para></summary>
public static class PersonNaming
{
    private const int MaxRetries = 12;

    public static string Full(string given, string family) => $"{given} {family}";

    /// <summary>Nome de família atual; o nome legado (<c>npc-…</c>, mundo antigo) ganha um nome de família estável derivado do id,
    /// para os filhos de quem veio de um mundo antigo já nascerem com nome.</summary>
    public static string FamilyOf(WorldState world, Npc npc) =>
        npc.FamilyName ?? CultureNaming.Of(world, npc.Culture).NewFamilyName(world.Rng.StreamFor("legacy-family-name", npc.Id.Value));

    /// <summary>Fundadores da semeadura, já agrupados em casas (casal primeiro, depois as crianças da casa). Cada adulto nasce
    /// de uma família própria; a casa usa o nome da linhagem do casal; as crianças, o da casa.</summary>
    public static void NameFounders(WorldState world, CultureId culture, IReadOnlyList<IReadOnlyList<Npc>> groups)
    {
        var naming = CultureNaming.Of(world, culture);
        foreach (var group in groups)
        {
            if (group.Count == 0) continue;
            var founder = group[0];
            Npc? spouse = group.Count > 1 && founder.Spouse == group[1].Id ? group[1] : null;
            string founderFamily = naming.NewFamilyName(world.Rng.StreamFor("founder-family-name", founder.Id.Value));
            string houseFamily = founderFamily;
            string? spouseFamily = null;
            if (spouse is not null)
            {
                spouseFamily = naming.NewFamilyName(world.Rng.StreamFor("founder-family-name", spouse.Id.Value));
                var holder = LineageHolder(naming, founder, spouse);
                houseFamily = ReferenceEquals(holder, founder) ? founderFamily : spouseFamily;
                if (naming.SpouseTakesName && founder.Sex != spouse.Sex) founderFamily = spouseFamily = houseFamily;
            }

            var taken = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < group.Count; i++)
            {
                var person = group[i];
                string family = i == 0 ? founderFamily : ReferenceEquals(person, spouse) ? spouseFamily! : houseFamily;
                var rng = world.Rng.StreamFor("founder-name", person.Id.Value);
                string name = Fresh(naming, person.Sex, family, rng, taken);
                taken.Add(name);
                person.Rename(name);
            }
        }
    }

    /// <summary>Nome do recém-nascido. Filho de casal casado: nome de família da linhagem; senão o da mãe. Com a chance do
    /// costume, o nome próprio homenageia um avô/avó (ou o pai morto antes do parto) do mesmo sexo, de preferência quem já
    /// morreu.</summary>
    public static string NameNewborn(WorldState world, Npc mother, Npc? father, NpcId babyId, Sex sex, Household household) =>
        NameNewborn(world, CultureNaming.Of(world, mother.Culture), mother, father, babyId, sex, household);

    internal static string NameNewborn(
        WorldState world, CultureNaming naming, Npc mother, Npc? father, NpcId babyId, Sex sex, Household household)
    {
        bool wed = father is not null && mother.Spouse == father.Id;
        var holder = wed ? LineageHolder(naming, mother, father!) : mother;
        string family = FamilyOf(world, holder);
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in household.Members)
            if (world.FindNpc(member) is { IsAlive: true } living) taken.Add(living.Name);

        var rng = world.Rng.StreamFor("birth-name", babyId.Value);
        if (rng.NextDouble() < naming.AncestorNameChance && Honoured(world, mother, father, sex, rng) is { } given
            && !taken.Contains(Full(given, family)))
            return Full(given, family);
        return Fresh(naming, sex, family, rng, taken);
    }

    /// <summary>Quem sai do pool agregado da cidade (materializado): nome próprio e de família novos da cultura.</summary>
    public static string NameStranger(WorldState world, CultureId culture, Sex sex, NpcId id)
    {
        var naming = CultureNaming.Of(world, culture);
        var rng = world.Rng.StreamFor("stranger-name", id.Value);
        return Full(naming.NewGivenName(sex, rng), naming.NewFamilyName(rng));
    }

    /// <summary>Casamento: com o costume, quem casa fora da linhagem adota o nome de família do cônjuge. Casal do mesmo sexo
    /// e nome legado (sem nome próprio separável) ficam como estão.</summary>
    public static void OnMarriage(WorldState world, Npc a, Npc b)
    {
        if (a.Sex == b.Sex) return;
        var head = a.Id.Value <= b.Id.Value ? a : b;
        OnMarriage(world, CultureNaming.Of(world, head.Culture), a, b);
    }

    internal static void OnMarriage(WorldState world, CultureNaming naming, Npc a, Npc b)
    {
        if (a.Sex == b.Sex || !naming.SpouseTakesName) return;
        var holder = LineageHolder(naming, a, b);
        var joiner = ReferenceEquals(holder, a) ? b : a;
        if (joiner.FamilyName is null) return;
        joiner.Rename(Full(joiner.GivenName, FamilyOf(world, holder)));
    }

    private static Npc LineageHolder(CultureNaming naming, Npc a, Npc b)
    {
        var wanted = naming.Lineage == Lineage.Patrilineal ? Sex.Male : Sex.Female;
        return a.Sex == wanted ? a : b.Sex == wanted ? b : (a.Id.Value <= b.Id.Value ? a : b);
    }

    private static string Fresh(CultureNaming naming, Sex sex, string family, WorldRng rng, HashSet<string> taken)
    {
        string name = Full(naming.NewGivenName(sex, rng), family);
        for (int i = 0; i < MaxRetries && taken.Contains(name); i++)
            name = Full(naming.NewGivenName(sex, rng), family);
        return name;
    }

    /// <summary>Nome próprio de um parente do mesmo sexo para homenagear: avós dos dois lados e o pai morto antes do parto.
    /// Quem já morreu tem a preferência; nome legado (sem nome próprio) não serve.</summary>
    private static string? Honoured(WorldState world, Npc mother, Npc? father, Sex sex, WorldRng rng)
    {
        var dead = new List<string>(4);
        var living = new List<string>(4);
        void Consider(NpcId? id)
        {
            if (id is not { } value) return;
            if (world.FindNpc(value) is { } npc)
            {
                if (npc.Sex == sex && npc.FamilyName is not null) (npc.IsAlive ? living : dead).Add(npc.GivenName);
            }
            else if (world.ColdArchive.Lookup(value.Value) is { } archived && archived.Sex == sex
                     && archived.Name.LastIndexOf(' ') is var cut and > 0)
                dead.Add(archived.Name[..cut]);
        }
        Consider(mother.MotherId);
        Consider(mother.FatherId);
        Consider(father?.MotherId);
        Consider(father?.FatherId);
        if (father is { IsAlive: false } && father.Sex == sex && father.FamilyName is not null) dead.Add(father.GivenName);
        var pool = dead.Count > 0 ? dead : living;
        return pool.Count == 0 ? null : pool[Math.Min(pool.Count - 1, (int)(rng.NextDouble() * pool.Count))];
    }
}
