using LivingWorld.Domain;

namespace LivingWorld.Simulation;

// SPEC_DEVIATION (Fase 8, T9): "líder de assentamento" (papel formal citado no design/roadmap)
// não tem campo nenhum em City (Foundation/T1 não declarou CityGovernment/Leader — nenhum
// critério de verificação da Fase 8 exige esse campo). Papel formal checado aqui é só o que já
// existe no domínio: chefe de household (Household.Head) e mestre de ofício (alguém referenciado
// como Mentor por outro Npc). Adicionar liderança de cidade exigiria um campo novo em City fora
// do escopo desta task (T9 só lista MaterializationSystem.cs).

/// <summary>Materializa/desmaterializa NPCs entre o <see cref="AggregatePopulationPool"/> de uma
/// cidade e uma linha real em <c>WorldState.Npcs</c> (Fase 8, T9, CITY-04/CITY-05, approach A).
/// <see cref="Tick"/> (Daily) desmaterializa por ociosidade quem não ocupa papel formal.</summary>
public sealed class MaterializationSystem : ISimulationSystem
{
    public const string SystemName = "cities-materialization";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.CityRules.Enabled) return;
        var rules = world.CityRules;

        // ToList: Dematerialize remove de world.Npcs — nunca mutar a coleção sendo enumerada.
        var candidates = world.Npcs
            .Where(n => n.IsAlive && n.MaterializedAtTick is not null)
            .OrderBy(n => n.Id.Value)
            .ToList();

        foreach (var npc in candidates)
        {
            long idleTicks = ctx.CurrentTick - npc.MaterializedAtTick!.Value;
            if (idleTicks < rules.MaterializationIdleTicksBeforeEligible) continue;
            if (HasFormalRole(world, npc.Id)) continue;

            Dematerialize(world, npc.Id);
        }
    }

    /// <summary>Papel formal (Fase 8, T9, CITY-05) — ver SPEC_DEVIATION acima sobre liderança de
    /// cidade não modelada.</summary>
    public static bool HasFormalRole(WorldState world, NpcId npcId) =>
        world.Households.Any(h => h.Head == npcId) || world.Npcs.Any(n => n.Mentor == npcId);

    /// <summary>Materializa 1 NPC do pool agregado de <paramref name="cityId"/>: debita
    /// exatamente 1 do <see cref="AggregatePopulationPool"/> e cria exatamente 1 linha de <see
    /// cref="Npc"/> (CITY-04). Atributos sorteados a partir das médias agregadas da cidade
    /// (Assumption confirmada: "sorteio livre a partir das faixas/estatísticas agregadas").</summary>
    public static Result<Npc> MaterializeOne(WorldState world, TickContext ctx, CityId cityId)
    {
        var city = world.FindCity(cityId);
        if (city is null) return Result<Npc>.Fail("City: não existe");

        long wealthPerHead = city.AggregatePool.Count > 0 ? city.AggregatePool.WealthSum / city.AggregatePool.Count : 0;
        long healthPerHead = city.AggregatePool.Count > 0
            ? Math.Clamp(city.AggregatePool.HealthSum / city.AggregatePool.Count, 0, 100)
            : 50;

        var materialized = city.Materialize(wealthPerHead, healthPerHead);
        if (!materialized.IsSuccess) return Result<Npc>.Fail(materialized.Error!);

        var id = world.NextNpcIdAndAdvance();
        var sex = ctx.Rng($"materialize-sex-{id.Value}").NextDouble() < 0.5 ? Sex.Female : Sex.Male;
        int maxAge = Math.Max(19, world.PopulationRules.LifeTable.MaxLongevityYears - 1);
        int ageYears = 18 + (int)(ctx.Rng($"materialize-age-{id.Value}").NextDouble() * (maxAge - 18));
        var birthDate = world.CurrentDate.AddYears(-ageYears);
        var personality = Personality.RollFrom(ctx.Rng($"materialize-personality-{id.Value}"));
        var profession = world.PopulationCatalog.RollProfession(ctx.Rng($"materialize-profession-{id.Value}"));
        var culture = world.PopulationCatalog.CultureIds.Count > 0
            ? new CultureId(world.PopulationCatalog.CultureIds.Order().First())
            : default;

        var npc = new Npc(
            id, $"npc-materialized-{id.Value}", sex, birthDate, culture, city.Location,
            motherId: null, fatherId: null, household: null, health: (int)healthPerHead,
            personality: personality, profession: profession, currentLocation: city.Location,
            wallet: new Money(wealthPerHead), city: cityId, materializedAtTick: ctx.CurrentTick);

        world.AddNpc(npc);
        return Result<Npc>.Ok(npc);
    }

    /// <summary>Desmaterializa: devolve riqueza/saúde ao pool e remove a linha (CITY-04). Só
    /// aceita NPC nascido do próprio pool (<see cref="Npc.MaterializedAtTick"/> não nulo) — NPC
    /// original (seed/nascimento) pode ter laços históricos (pai/mãe/cônjuge) que virariam
    /// ponteiro solto se removido (invariante de <c>WorldState.Npcs</c>), então nunca é
    /// elegível aqui.</summary>
    public static Result<Unit> Dematerialize(WorldState world, NpcId npcId)
    {
        var npc = world.FindNpc(npcId);
        if (npc is null || !npc.IsAlive) return Result<Unit>.Fail("Npc: não existe ou está morto");
        if (npc.MaterializedAtTick is null) return Result<Unit>.Fail("Npc: não veio do pool agregado, não elegível");
        if (HasFormalRole(world, npcId)) return Result<Unit>.Fail("Npc: ocupa papel formal, não elegível");

        var city = world.FindCity(npc.City);
        if (city is null) return Result<Unit>.Fail("City: não existe");

        city.Dematerialize(npc.Wallet.Amount, npc.Health);
        world.RemoveNpc(npcId);
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>Materializa sob demanda (CITY-05) — chamado pela inspeção (T14). Como o pool não
    /// guarda identidade por NPC, só há o que "garantir" para quem já existe como linha real; um
    /// id nunca visto não tem entidade a resolver.</summary>
    public static Result<Unit> EnsureMaterialized(WorldState world, NpcId npcId)
    {
        var npc = world.FindNpc(npcId);
        if (npc is null || !npc.IsAlive) return Result<Unit>.Fail("Npc: não existe ou está morto");
        return Result<Unit>.Ok(Unit.Value);
    }
}
