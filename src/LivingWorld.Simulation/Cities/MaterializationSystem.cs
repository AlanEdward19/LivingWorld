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
    /// (Assumption confirmada: "sorteio livre a partir das faixas/estatísticas agregadas").
    /// <paramref name="specificId"/> materializa exatamente aquele id reservado (T50, clique do
    /// usuário num membro específico do pool); omitido, materializa qualquer um (o último
    /// reservado) — comportamento de sempre pra quem só quer "materialize alguém desta cidade".</summary>
    public static Result<Npc> MaterializeOne(WorldState world, TickContext ctx, CityId cityId, NpcId? specificId = null)
    {
        var city = world.FindCity(cityId);
        if (city is null) return Result<Npc>.Fail("City: não existe");
        if (city.PoolNpcIds.Count == 0) return Result<Npc>.Fail("AggregatePool: nenhum NPC agregado disponível para materializar");

        var id = specificId ?? city.PoolNpcIds[^1];

        long wealthPerHead = city.AggregatePool.Count > 0 ? city.AggregatePool.WealthSum / city.AggregatePool.Count : 0;
        long healthPerHead = city.AggregatePool.Count > 0
            ? Math.Clamp(city.AggregatePool.HealthSum / city.AggregatePool.Count, 0, 100)
            : 50;

        var materialized = city.Materialize(id, wealthPerHead, healthPerHead);
        if (!materialized.IsSuccess) return Result<Npc>.Fail(materialized.Error!);

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

        city.Dematerialize(npcId, npc.Wallet.Amount, npc.Health);
        world.RemoveNpc(npcId);
        return Result<Unit>.Ok(Unit.Value);
    }

    // DESIGN (Fase 8, fix round 1, gap 2 — CITY-05 AC2; T50 reabre e resolve de verdade):
    // approach A (design.md) não atribuía NpcId a membro do AggregatePopulationPool — só existia
    // contagem+somas, nunca identidade individual, tornando "consultar um NPC agregado por id"
    // estruturalmente impossível.
    //
    // Uma primeira tentativa (rejeitada, comentário histórico removido em T50) tentou uma FAIXA
    // contígua de NpcId por cidade — quebra porque o contador global é consumido por dois fluxos
    // intercalados (nascimentos a cada tick, materialização sob demanda), então um range simples
    // não fecha. Escolha final (T50): cada cidade guarda uma LISTA de ids já reservados (<see
    // cref="City.PoolNpcIds"/>, não um range) — cresce só nos 3 pontos que já mexiam no pool
    // (carga de cenário em lote, desmaterialização devolvendo o próprio id de quem saiu, fundação
    // de assentamento transferindo a lista inteira) e encolhe nos que já existiam (materializar,
    // emigrar). Nenhum range pra "fechar", intercalação não importa mais.
    public static Result<Unit> EnsureMaterialized(WorldState world, NpcId npcId)
    {
        var npc = world.FindNpc(npcId);
        if (npc is not null)
            return npc.IsAlive ? Result<Unit>.Ok(Unit.Value) : Result<Unit>.Fail("Npc: não existe ou está morto");

        var city = world.Cities.FirstOrDefault(c => c.PoolNpcIds.Contains(npcId));
        if (city is null) return Result<Unit>.Fail("Npc: não existe ou está morto");

        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        var materialized = MaterializeOne(world, ctx, city.Id, npcId);
        return materialized.IsSuccess ? Result<Unit>.Ok(Unit.Value) : Result<Unit>.Fail(materialized.Error!);
    }
}
