using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

/// <summary>Filtro do criador de personagem (AD-197q): só restringe a quem o agregado já tem; nunca escolhe atributo.</summary>
public sealed record CharacterFilter(Sex? Sex = null, int? MinAge = null, int? MaxAge = null, int? ProfessionId = null)
{
    public static CharacterFilter None { get; } = new();

    internal bool Accepts(MaterializationProfile p) =>
        (Sex is null || p.Sex == Sex) && (MinAge is null || p.AgeYears >= MinAge) && (MaxAge is null || p.AgeYears <= MaxAge)
        && (ProfessionId is null || p.Profession.Id == ProfessionId);
}

/// <summary>Um candidato: o membro reservado <see cref="NpcId"/> do agregado de <see cref="CityId"/> e quem ele vira ao materializar.
/// <see cref="BornInCityId"/>: onde nasceu (a cidade-mãe, se é mais velho que a cidade); <see cref="YearsInCity"/>: há quanto tempo
/// vive nela. Família detalhada não há: os parentes, se existem, seguem no agregado (materializar não inventa ninguém).
/// <see cref="WillPrevents"/>: a vontade da pessoa impede a posse comum (a mesma regra de encarnar).</summary>
public sealed record CharacterCandidate(
    long NpcId, Guid CityId, string Name, Sex Sex, int AgeYears, int ProfessionId, int CultureId, long Wealth, int Health,
    Personality Personality, Guid BornInCityId, int YearsInCity, bool WillPrevents);

/// <summary>Uma amostra do agregado: <see cref="Sample"/> é o número dela no registro do jogador (o replay a refaz igual).</summary>
public sealed record CharacterSample(
    Guid CityId, long Sample, long AggregateCount, long Matching, CharacterFilter Filter, IReadOnlyList<CharacterCandidate> Candidates);

/// <summary>Criador de personagem pelo motor (Fase 25, task 1; AD-197q): entrar como pessoa nova é tirar alguém do agregado de uma
/// cidade (materializar move, nunca cria) e encarná-la pelo caminho de sempre. A amostra é sorteada por chave fixa
/// (cidade, número da amostra): não mexe em stream guardado, e pedir outra é um comando registrado como qualquer outro.</summary>
public static class CharacterCreation
{
    public const int DefaultCount = 6;
    public const int MaxCount = 12;

    /// <summary>Até <paramref name="count"/> candidatos do agregado de <paramref name="cityId"/> que passam no filtro, na amostra
    /// <paramref name="sample"/>. Só lê. 404 sem cidade; 409 com agregado vazio ou ninguém no filtro.</summary>
    public static Result<CharacterSample> Candidates(WorldState world, CityId cityId, long sample, CharacterFilter filter, int count)
    {
        var city = world.FindActiveCity(cityId);
        if (city is null) return Result<CharacterSample>.Fail($"{PlayerCommands.NotFound} cidade ausente");
        if (city.PoolNpcIds.Count == 0)
            return Result<CharacterSample>.Fail($"{PlayerCommands.Conflict} o agregado de {city.Name} não tem ninguém para materializar");
        if (count is < 1 or > MaxCount) return Result<CharacterSample>.Fail($"count: entre 1 e {MaxCount}");

        var prior = MaterializationPrior.Of(world, city);
        var matching = new List<MaterializationProfile>();
        foreach (var id in city.PoolNpcIds)
        {
            var profile = prior.Profile(id);
            if (filter.Accepts(profile)) matching.Add(profile);
        }
        if (matching.Count == 0)
            return Result<CharacterSample>.Fail($"{PlayerCommands.Conflict} ninguém no agregado de {city.Name} com esse filtro");

        // Fisher–Yates parcial com a chave da amostra: mesma cidade, mesmo número e mesmo agregado = mesmos candidatos
        var rng = world.Rng.OneShot($"character-sample|{cityId.Value}|{sample}");
        int take = Math.Min(count, matching.Count);
        for (int i = 0; i < take; i++)
        {
            int j = i + (int)(rng.NextDouble() * (matching.Count - i));
            (matching[i], matching[j]) = (matching[j], matching[i]);
        }

        long tick = world.CurrentDate.TotalHours;
        int cityYears = (int)(Math.Max(0, tick - city.FoundedAtTick) / world.Calendar.HoursPerYear);
        var candidates = new List<CharacterCandidate>(take);
        for (int i = 0; i < take; i++)
        {
            var p = matching[i];
            var npc = prior.Build(p, tick);
            bool bornHere = p.BornIn == city.Id;
            candidates.Add(new CharacterCandidate(
                p.Id.Value, city.Id.Value, npc.Name, p.Sex, p.AgeYears, p.Profession.Id, p.Culture.Id, p.Wealth, p.Health, p.Personality,
                (p.BornIn ?? city.Id).Value, bornHere ? p.AgeYears : Math.Min(p.AgeYears, cityYears),
                PossessionContest.Prevents(world, npc, tick)));
        }
        return Result<CharacterSample>.Ok(new CharacterSample(cityId.Value, sample, city.PoolNpcIds.Count, matching.Count, filter, candidates));
    }

    /// <summary>Entra como <paramref name="npcId"/>, que tem de estar na amostra <paramref name="sample"/> refeita agora: materializa
    /// pelo caminho de sempre (<see cref="MaterializationSystem.MaterializeOne"/>) e encarna em posse comum (a mesma regra de
    /// qualquer encarnação). Toda recusa vem antes de mexer no mundo.</summary>
    public static Result<Npc> Enter(WorldState world, TickContext ctx, string playerId, CityId cityId, NpcId npcId, long sample,
        CharacterFilter filter, int count)
    {
        if (string.IsNullOrWhiteSpace(playerId)) return Result<Npc>.Fail("playerId: obrigatório");
        if (PlayerCommands.ControlledBy(world, playerId) is not null)
            return Result<Npc>.Fail($"{PlayerCommands.Conflict} jogador já controla outro NPC");
        var drawn = Candidates(world, cityId, sample, filter, count);
        if (!drawn.IsSuccess) return Result<Npc>.Fail(drawn.Error!);
        var chosen = drawn.Value!.Candidates.FirstOrDefault(c => c.NpcId == npcId.Value);
        if (chosen is null) return Result<Npc>.Fail($"{PlayerCommands.Conflict} essa pessoa não está mais na amostra: peça outra");
        if (chosen.WillPrevents)
            return Result<Npc>.Fail($"{PlayerCommands.Refused} a vontade de {chosen.Name} impede a posse");

        var materialized = MaterializationSystem.MaterializeOne(world, ctx, cityId, npcId);
        if (!materialized.IsSuccess) return Result<Npc>.Fail($"{PlayerCommands.Conflict} {materialized.Error}");
        var npc = materialized.Value!;
        var incarnated = PlayerCommands.Incarnate(world, ctx, npc.Id, playerId);
        // As checagens acima cobrem toda recusa de Incarnate (jogador livre, pessoa viva e sem controlador, vontade medida no mesmo
        // perfil); chegar aqui com falha é defeito, não recusa.
        if (!incarnated.IsSuccess) throw new InvalidOperationException($"criador de personagem: encarnar falhou depois de materializar ({incarnated.Error})");
        return Result<Npc>.Ok(npc);
    }
}
