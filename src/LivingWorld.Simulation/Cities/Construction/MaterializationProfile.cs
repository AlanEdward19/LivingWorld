using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Cities.Construction;

/// <summary>Quem um membro do agregado vira ao materializar (AD-197q): a mesma conta para a prévia do criador de personagem e
/// para <see cref="MaterializationSystem.MaterializeOne"/>, então o candidato mostrado é exatamente a pessoa que entra. Tudo sai de
/// sorteios de chave fixa por id (<c>OneShot</c>: não mexe em stream guardado) e do que a cidade de origem é agora.</summary>
public sealed record MaterializationProfile(
    NpcId Id, CityId City, Sex Sex, int AgeYears, long BirthdayOffsetHours, Personality Personality, ProfessionType Profession,
    CultureId Culture, long Wealth, int Health, CellCoord BirthLocation, CityId? BornIn);

/// <summary>O que o agregado de uma cidade diz de quem está nele (amostragem condicionada, <c>docs/domain/simulation-lod.md</c>):
/// riqueza e saúde por cabeça do pool, a pirâmide de idades, a distribuição de ofícios e a cultura predominante dos adultos detalhados da cidade (a parte
/// observada da mesma população), e de onde veio quem é mais velho que a cidade (a cidade-mãe, se ela foi fundada por outra).
/// Calculado uma vez por pedido; ninguém guarda.</summary>
public sealed class MaterializationPrior
{
    private readonly WorldState _world;
    private readonly City _city;
    private readonly long _wealthPerHead;
    private readonly int _healthPerHead;
    private readonly (int Profession, int Count)[] _professions;
    private readonly int _professionTotal;
    private readonly int[] _adultAges;
    private readonly CultureId _culture;
    private readonly int _cityAgeYears;
    private readonly City? _parent;

    private MaterializationPrior(WorldState world, City city)
    {
        _world = world;
        _city = city;
        var pool = city.AggregatePool;
        _wealthPerHead = pool.Count > 0 ? pool.WealthSum / pool.Count : 0;
        _healthPerHead = (int)(pool.Count > 0 ? Math.Clamp(pool.HealthSum / pool.Count, 0, 100) : 50);

        var professions = new SortedDictionary<int, int>();
        var cultures = new SortedDictionary<int, int>();
        var ages = new List<int>();
        foreach (var npc in world.Npcs)
        {
            if (!npc.IsAlive || npc.City != city.Id || npc.DisappearedAtTick is not null) continue;
            int age = npc.AgeYears(world.CurrentDate);
            if (age < AdultYears) continue;
            ages.Add(age);
            professions[npc.Profession.Id] = professions.GetValueOrDefault(npc.Profession.Id) + 1;
            cultures[npc.Culture.Id] = cultures.GetValueOrDefault(npc.Culture.Id) + 1;
        }
        _professions = professions.Select(p => (p.Key, p.Value)).ToArray();
        _professionTotal = _professions.Sum(p => p.Count);
        ages.Sort();
        _adultAges = [.. ages];
        _culture = cultures.Count > 0
            ? new CultureId(cultures.OrderByDescending(c => c.Value).ThenBy(c => c.Key).First().Key)
            : world.PopulationCatalog.CultureIds.Count > 0 ? new CultureId(world.PopulationCatalog.CultureIds.Order().First()) : default;
        _cityAgeYears = (int)(Math.Max(0, world.CurrentDate.TotalHours - city.FoundedAtTick) / world.Calendar.HoursPerYear);
        _parent = city.FoundedFromCityId is { } parent ? world.FindCity(parent) : null;
    }

    /// <summary>Idade mínima de quem sai do agregado (a materialização de sempre: adultos).</summary>
    public const int AdultYears = 18;

    public static MaterializationPrior Of(WorldState world, City city) => new(world, city);

    /// <summary>Riqueza por cabeça do pool agora.</summary>
    public long WealthPerHead => _wealthPerHead;

    /// <summary>Saúde por cabeça do pool agora (0–100).</summary>
    public int HealthPerHead => _healthPerHead;

    /// <summary>A pessoa que o id reservado <paramref name="id"/> vira ao materializar agora. Só lê.</summary>
    public MaterializationProfile Profile(NpcId id)
    {
        var rng = _world.Rng;
        var sex = rng.OneShot($"materialize-sex-{id.Value}").NextDouble() < 0.5 ? Sex.Female : Sex.Male;
        int ageYears = RollAge(rng.OneShot($"materialize-age-{id.Value}").NextDouble());
        long birthday = (long)(rng.OneShot($"materialize-birthday-{id.Value}").NextDouble() * _world.Calendar.HoursPerYear);
        var personality = Personality.RollFrom(rng.OneShot($"materialize-personality-{id.Value}"));
        var profession = RollProfession(rng.OneShot($"materialize-profession-{id.Value}"));
        // quem é mais velho que a cidade nasceu antes dela: na cidade-mãe, se ela veio de outra
        bool olderThanCity = ageYears > _cityAgeYears;
        var bornIn = olderThanCity && _parent is not null ? _parent : _city;
        return new MaterializationProfile(id, _city.Id, sex, ageYears, birthday, personality, profession, _culture,
            _wealthPerHead, _healthPerHead, bornIn.Location, olderThanCity && _parent is not null ? _parent.Id : _city.Id);
    }

    /// <summary>A idade sai da pirâmide dos adultos detalhados da cidade (a parte vista da mesma população: a idade de um deles,
    /// sorteado); cidade sem adulto detalhado cai no sorteio uniforme de antes, de 18 à longevidade máxima.</summary>
    private int RollAge(double draw)
    {
        if (_adultAges.Length > 0) return _adultAges[Math.Min((int)(draw * _adultAges.Length), _adultAges.Length - 1)];
        int maxAge = Math.Max(AdultYears + 1, _world.PopulationRules.LifeTable.MaxLongevityYears - 1);
        return AdultYears + (int)(draw * (maxAge - AdultYears));
    }

    /// <summary>O ofício sai da distribuição dos adultos detalhados da cidade (a parte vista da mesma população); cidade sem
    /// ninguém detalhado cai no catálogo do período, como antes.</summary>
    private ProfessionType RollProfession(WorldRng rng)
    {
        if (_professionTotal == 0) return _world.PopulationCatalog.RollProfession(rng);
        double target = rng.NextDouble() * _professionTotal;
        double cumulative = 0;
        foreach (var (profession, count) in _professions)
        {
            cumulative += count;
            if (target < cumulative) return new ProfessionType(profession);
        }
        return new ProfessionType(_professions[^1].Profession);
    }

    /// <summary>A linha de <see cref="Npc"/> do perfil, sem entrar no mundo (a prévia usa para medir a vontade; a materialização a
    /// adiciona).</summary>
    public Npc Build(MaterializationProfile profile, long materializedAtTick)
    {
        var birthDate = _world.CurrentDate.AddYears(-profile.AgeYears).AddHours(-profile.BirthdayOffsetHours);
        return new Npc(
            profile.Id, LivingWorld.Simulation.Population.Naming.PersonNaming.NameStranger(_world, profile.Culture, profile.Sex, profile.Id),
            profile.Sex, birthDate, profile.Culture, profile.BirthLocation,
            motherId: null, fatherId: null, household: null, health: profile.Health,
            personality: profile.Personality, profession: profile.Profession, currentLocation: _city.Location,
            wallet: new Money(profile.Wealth), city: _city.Id, materializedAtTick: materializedAtTick);
    }
}
