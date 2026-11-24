using LivingWorld.Domain.Economy;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population;
using LivingWorld.Simulation.Population.Body;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Population.Naming;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

/// <summary>A pessoa como o motor a validou: a linha de <see cref="Npc"/> ainda fora do mundo (a prévia a mostra; criar a adiciona),
/// o papel, o dinheiro e quem a aceita. <see cref="Money"/> é o que entra na carteira (cunhado: vem de fora).</summary>
public sealed record ResolvedCharacter(Npc Npc, CharacterRole Role, long Money, Npc? Anchor, string CityName);

/// <summary>Criador de personagem de verdade (AD-197t, emenda do ADR-0053): o jogador escolhe nome, idade, sexo, traços, cidade, lugar
/// na família ou chegada de fora, dinheiro e aparência; o motor valida tudo antes de mexer no mundo, sorteia o que ficou em branco
/// pela chave (jogador, semente) e aplica pelo caminho de sempre (casa, parentes, herança, chegada de migrante). Recusa = 409 com o
/// motivo e o mundo intacto.</summary>
public static class CharacterBuilder
{
    public const int MinName = 2, MaxName = 60;

    /// <summary>As idades (menor e maior) em que o laço <paramref name="role"/> é possível agora, ou o motivo de não ser. Só lê.</summary>
    public static Result<(int Min, int Max)> AgeWindow(WorldState world, LivingWorld.Domain.Cities.City city, CharacterRole role)
    {
        var plan = CharacterRoles.Plan(world, city, role);
        if (!plan.IsSuccess) return Result<(int, int)>.Fail(plan.Error!);
        return plan.Value!.Ages.Count == 0 ? Result<(int, int)>.Fail($"{PlayerCommands.Conflict} nenhuma idade adulta serve a esse laço")
            : Result<(int, int)>.Ok((plan.Value.Ages[0], plan.Value.Ages[^1]));
    }

    private static Result<ResolvedCharacter> Refuse(string reason) => Result<ResolvedCharacter>.Fail($"{PlayerCommands.Conflict} {reason}");

    /// <summary>Valida e monta a pessoa sem tocar no mundo (a prévia e a criação passam por aqui; a mesma escolha, a mesma pessoa).</summary>
    public static Result<ResolvedCharacter> Resolve(WorldState world, string playerId, CharacterSpec spec)
    {
        if (string.IsNullOrWhiteSpace(playerId)) return Result<ResolvedCharacter>.Fail("playerId: obrigatório");
        if (PlayerCommands.ControlledBy(world, playerId) is not null) return Refuse("jogador já controla outro NPC");
        var city = world.FindActiveCity(new LivingWorld.Domain.Shared.CityId(spec.CityId));
        if (city is null) return Refuse("a cidade escolhida não existe");
        var role = spec.Role ?? new CharacterRole();
        var plan = CharacterRoles.Plan(world, city, role);
        if (!plan.IsSuccess) return Result<ResolvedCharacter>.Fail(plan.Error!);
        var (anchor, parents, ages, home) = (plan.Value!.Anchor, plan.Value.Parents, plan.Value.Ages, plan.Value.Home);
        if (ages.Count == 0) return Refuse("este mundo não tem idade adulta possível para criar uma pessoa");

        string salt = $"character|{playerId}|{spec.Seed}";
        var prior = MaterializationPrior.Of(world, city);
        var drawn = prior.Profile(new NpcId(0), salt);
        var now = world.CurrentDate;

        // idade: a escolhida tem de caber na longevidade e no laço; em branco, o motor sorteia entre as que servem
        int minAge = ages.Count > 0 ? ages[0] : MaterializationPrior.AdultYears, maxAge = ages.Count > 0 ? ages[^1] : minAge;
        int age;
        if (spec.AgeYears is { } chosenAge)
        {
            int longevity = world.PopulationRules.LifeTable.MaxLongevityYears;
            if (chosenAge < MaterializationPrior.AdultYears || chosenAge >= longevity)
                return Refuse($"idade fora da longevidade: de {MaterializationPrior.AdultYears} a {longevity - 1} anos");
            if (!ages.Contains(chosenAge))
                return Refuse($"com esse laço a idade só pode ser de {minAge} a {maxAge} anos (os pais ou o cônjuge não permitem {chosenAge})");
            age = chosenAge;
        }
        else
            age = role.Kind is CharacterRole.Newcomer or CharacterRole.Relative && ages.Contains(drawn.AgeYears)
                ? drawn.AgeYears : ages[(int)(world.Rng.OneShot($"{salt}-age").NextDouble() * ages.Count)];

        var sex = spec.Sex ?? drawn.Sex;
        if (!Enum.IsDefined(sex)) return Refuse("sexo desconhecido");
        if (spec.ProfessionId is { } professionId && (professionId < 0 || !world.PopulationCatalog.IsValidProfession(new ProfessionType(professionId))))
            return Refuse("o ofício escolhido não existe neste período");
        var profession = spec.ProfessionId is { } chosenProfession ? new ProfessionType(chosenProfession) : drawn.Profession;
        if (spec.Health is { } chosenHealth && chosenHealth is < 1 or > 100) return Refuse("saúde: de 1 a 100");
        if (spec.Traits is { } traits)
            foreach (var (name, value) in traits.All())
                if (value is < 0 or > 100) return Refuse($"traço {name}: de 0 a 100");
        var personality = spec.Traits?.Over(drawn.Personality) ?? drawn.Personality;

        var culture = anchor?.Culture ?? OriginCulture(world, role, drawn);
        string name2 = spec.Name?.Trim() ?? "";
        if (spec.Name is not null && (name2.Length < MinName || name2.Length > MaxName)) return Refuse($"nome: de {MinName} a {MaxName} letras");
        if (name2.Length == 0) name2 = DrawName(world, salt, culture, sex, anchor, role);

        var money = Money(world, city, prior, spec.Money);
        if (!money.IsSuccess) return Result<ResolvedCharacter>.Fail(money.Error!);

        var look = Look(world, salt, sex, age, spec.Look, role, parents);
        if (!look.IsSuccess) return Result<ResolvedCharacter>.Fail(look.Error!);

        var birth = now.AddYears(-age).AddHours(-drawn.BirthdayOffsetHours);
        var born = role.FromCityId is { } fromCity && world.FindCity(new LivingWorld.Domain.Shared.CityId(fromCity)) is { } origin ? origin.Location : city.Location;
        var mother = parents.FirstOrDefault(p => p.Sex == Sex.Female);
        var father = parents.FirstOrDefault(p => p.Sex == Sex.Male);
        var npc = new Npc(
            new NpcId(world.NextNpcId), name2, sex, birth, culture, anchor is not null ? city.Location : born,
            mother?.Id, father?.Id, home?.Id, spec.Health ?? drawn.Health, personality, profession,
            home?.Location ?? city.Location, wallet: new Money(money.Value), city: city.Id,
            height: look.Value!.Height, weight: look.Value.Weight, muscleMass: look.Value.Muscle, appearance: look.Value.Appearance);
        npc.SetStature(look.Value.StatureGene, look.Value.AdultHeight);
        PopulationSeeder.SeedPracticedSkill(world, npc); // o ofício que pratica desde moço: pesa na preferência de quem o aceita
        if (PossessionContest.Prevents(world, npc, now.TotalHours)) // a mesma regra de encarnar: sem exceção por ser personagem novo
            return Result<ResolvedCharacter>.Fail($"{PlayerCommands.Refused} a vontade de {npc.Name} impede a posse: mude os traços");
        if (role.Kind == CharacterRole.Spouse)
        {
            var accepted = CharacterRoles.SpouseAccepts(world, anchor!, npc);
            if (!accepted.IsSuccess) return Result<ResolvedCharacter>.Fail(accepted.Error!);
        }
        return Result<ResolvedCharacter>.Ok(new ResolvedCharacter(npc, role, money.Value, anchor, city.Name));
    }

    private static CultureId OriginCulture(WorldState world, CharacterRole role, MaterializationProfile drawn)
    {
        if (role.FromCityId is not { } from || world.FindCity(new LivingWorld.Domain.Shared.CityId(from)) is not { } origin) return drawn.Culture;
        return MaterializationPrior.Of(world, origin).Profile(new NpcId(0), "origin").Culture;
    }

    private static string DrawName(WorldState world, string salt, CultureId culture, Sex sex, Npc? anchor, CharacterRole role)
    {
        var naming = CultureNaming.Of(world, culture);
        var rng = world.Rng.OneShot($"{salt}-name");
        string given = naming.NewGivenName(sex, rng);
        bool kin = role.Kind is CharacterRole.Child or CharacterRole.Sibling && anchor?.FamilyName is not null;
        return PersonNaming.Full(given, kin ? anchor!.FamilyName! : naming.NewFamilyName(rng));
    }

    /// <summary>Dinheiro inicial: o escolhido (de zero ao teto) ou, em branco, o que um morador típico da cidade tem. O teto é
    /// 5× o que o morador típico tem (mínimo 500). Sem economia no mundo, só zero.</summary>
    internal static Result<long> Money(WorldState world, LivingWorld.Domain.Cities.City city, MaterializationPrior prior, long? chosen)
    {
        if (!world.EconomyRules.Enabled)
            return chosen is > 0 ? Result<long>.Fail($"{PlayerCommands.Conflict} este mundo não tem economia: o dinheiro inicial só pode ser zero")
                : Result<long>.Ok(0);
        long typical = TypicalWealth(world, city);
        long cap = MoneyCap(world, city);
        if (chosen is { } value && (value < 0 || value > cap))
            return Result<long>.Fail($"{PlayerCommands.Conflict} dinheiro inicial: de 0 a {cap} (o morador típico de {city.Name} tem {typical})");
        return Result<long>.Ok(chosen ?? typical);
    }

    internal static long TypicalWealth(WorldState world, LivingWorld.Domain.Cities.City city)
    {
        var wallets = world.Npcs.Where(n => n.IsAlive && n.City == city.Id && n.DisappearedAtTick is null).Select(n => n.Wallet.Amount).Order().ToList();
        return wallets.Count == 0 ? 0 : wallets[wallets.Count / 2];
    }

    internal static long MoneyCap(WorldState world, LivingWorld.Domain.Cities.City city) => Math.Max(500, 5 * TypicalWealth(world, city));

    private sealed record Built(double Height, double Weight, double Muscle, double StatureGene, double AdultHeight, Appearance Appearance);

    private static int Gene(WorldRng rng) => Math.Min((int)(rng.NextDouble() * 101), 100);

    /// <summary>Aparência e corpo: o escolhido valida (genes em [0,100], altura na faixa do sexo, traje do catálogo); o resto o motor
    /// sorteia (do filho, herdado dos pais). A altura vira estatura adulta e o corpo o peso do arcabouço: o crescimento do mês a mantém.</summary>
    private static Result<Built> Look(WorldState world, string salt, Sex sex, int age, CharacterLook? chosen, CharacterRole role, IReadOnlyList<Npc> parents)
    {
        chosen ??= new CharacterLook();
        var checks = new (string Name, int? Value, int Max)[]
        {
            ("pele", chosen.SkinTone, 100), ("cabelo", chosen.HairTone, 100), ("ondulação do cabelo", chosen.HairCurl, 100),
            ("corpo", chosen.Build, 100), ("olhos", chosen.EyeTone, 100), ("rosto", chosen.FaceShape, 100),
            ("roupa", chosen.Outfit, Appearance.OutfitCount - 1),
        };
        foreach (var (name, value, max) in checks)
            if (value is < 0 || value > max) return Result<Built>.Fail($"{PlayerCommands.Conflict} {name}: de 0 a {max}");
        var growth = world.BodyRules.Growth;
        double minHeight = 1.40, maxHeight = 2.10;
        if (growth.Enabled)
        {
            minHeight = growth.AdultHeightMean(sex) - 3.5 * growth.AdultHeightStdDev(sex);
            maxHeight = growth.AdultHeightMean(sex) + 3.5 * growth.AdultHeightStdDev(sex);
        }
        if (chosen.HeightM is { } wanted && (!(wanted >= minHeight) || wanted > maxHeight))
            return Result<Built>.Fail($"{PlayerCommands.Conflict} altura: de {minHeight:0.00} a {maxHeight:0.00} m para esse sexo");

        var rng = world.Rng.OneShot($"{salt}-look");
        var rolled = Appearance.RollFrom(rng);
        var mother = parents.FirstOrDefault(p => p.Sex == Sex.Female)?.Appearance;
        var father = parents.FirstOrDefault(p => p.Sex == Sex.Male)?.Appearance;
        if (role.Kind is CharacterRole.Child or CharacterRole.Sibling && world.AppearanceRules is { } rules && (mother ?? father) is { } any)
            rolled = Appearance.Inherit(mother ?? any, father ?? any, world.Rng.OneShot($"{salt}-look-heredity"), rules.Heritability);
        int build = chosen.Build ?? rolled.Build;
        var appearance = new Appearance(
            chosen.SkinTone ?? rolled.SkinTone, chosen.HairTone ?? rolled.HairTone, chosen.HairCurl ?? rolled.HairCurl, build,
            EyeTone: chosen.EyeTone ?? Gene(rng), FaceShape: chosen.FaceShape ?? Gene(rng),
            Outfit: chosen.Outfit ?? Math.Min((int)(rng.NextDouble() * Appearance.OutfitCount), Appearance.OutfitCount - 1), BodyFrame: build);

        if (!growth.Enabled) // sem crescimento causal o corpo é o que foi escolhido (ou o padrão do mundo antigo)
            return Result<Built>.Ok(new Built(chosen.HeightM ?? 1.70, 68.0, 28.0, 0, 0, appearance));
        double mean = growth.AdultHeightMean(sex), deviation = growth.AdultHeightStdDev(sex);
        double fraction = growth.HeightAt(sex, age, 1.0, null);
        double adult;
        double gene;
        if (chosen.HeightM is { } height)
        {
            adult = height / fraction;
            gene = (adult - mean) / deviation;
        }
        else
        {
            gene = Math.Sqrt(Math.Clamp(growth.StatureHeritability, 0, 1)) * QuantitativeGenetics.Normal(rng);
            if (role.Kind is CharacterRole.Child or CharacterRole.Sibling && parents.Count > 0)
                gene = QuantitativeGenetics.Inherit(parents[0].StatureGene, parents[^1].StatureGene,
                    world.BodyRules.Genetics.Mendelian ? world.BodyRules.Genetics.StatureSegregationStdDev : 0.0, rng);
            adult = Math.Max(0.5, growth.AdultHeightFrom(sex, gene, QuantitativeGenetics.Normal(rng)));
        }
        double current = growth.HeightAt(sex, age, adult, null);
        double weight = growth.WeightAt(sex, age, current, BodyGrowth.FrameOf(build), 0, null);
        return Result<Built>.Ok(new Built(current, weight, growth.MuscleTarget(sex, age, weight, BodyGrowth.FounderWorkShare), gene, adult, appearance));
    }

    /// <summary>Cria a pessoa: valida tudo antes (recusa não muda o mundo), a registra como qualquer habitante (corpo, necessidades,
    /// ofício praticado, vigília, mortalidade), a põe na casa ou a faz chegar como migrante, casa quem casa e a encarna em posse comum.</summary>
    public static Result<Npc> Create(WorldState world, TickContext ctx, string playerId, CharacterSpec spec)
    {
        var resolved = Resolve(world, playerId, spec);
        if (!resolved.IsSuccess) return Result<Npc>.Fail(resolved.Error!);
        var (npc, role, money, anchor) = (resolved.Value!.Npc, resolved.Value.Role, resolved.Value.Money, resolved.Value.Anchor);
        if (world.NextNpcIdAndAdvance() != npc.Id) throw new InvalidOperationException("criador de personagem: id do próximo NPC mudou entre validar e criar");

        npc.ConfigureNeedDecay(world.NeedsRules, world.CurrentDate.TotalHours);
        world.AddNpc(npc);
        LivingWorld.Simulation.Ontogeny.OntogenyMechanic.Initialize(world, npc);
        if (money > 0) world.Mint(ctx, new Money(money), "player-character"); // o dinheiro inicial entra de fora e fica contado
        if (npc.Household is { } homeId) world.FindHousehold(homeId)!.AddMember(npc.Id);

        if (role.Kind == CharacterRole.Newcomer)
        {
            npc.MarkArrived(ctx.CurrentTick);
            ctx.LogEvent(WorldEventKind.CharacterArrived, $"{npc.Id.Value}|{npc.City.Value}|{role.FromCityId}|{money}", sourceSystem: "CharacterBuilder");
        }
        else
        {
            ctx.LogEvent(WorldEventKind.CharacterJoinedFamily, $"{npc.Id.Value}|{npc.City.Value}|{role.Kind}|{anchor!.Id.Value}|{money}", sourceSystem: "CharacterBuilder");
            if (role.Kind == CharacterRole.Spouse) MarriageSystem.Marry(world, ctx, anchor, npc);
        }
        MortalitySystem.CoverUntilNextSweep(world, ctx, npc);
        NpcWakeScheduler.ScheduleWake(world, ctx, npc.Id.Value, world.CurrentDate.TotalHours + 1);

        // encarna em posse comum: a vontade já foi medida ao validar (antes de mexer no mundo), então aqui não se recusa de novo
        var incarnated = npc.Incarnate(playerId);
        if (!incarnated.IsSuccess) throw new InvalidOperationException($"criador de personagem: encarnar falhou depois de criar ({incarnated.Error})");
        PossessionContest.Begin(world, ctx, npc);
        if (!world.Players.Enabled) ctx.LogEvent(WorldEventKind.PlayerIncarnated, $"{npc.Id.Value}|{playerId}", sourceSystem: "CharacterBuilder");
        return Result<Npc>.Ok(npc);
    }
}
