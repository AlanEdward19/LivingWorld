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

    // DESIGN (Fase 8, fix round 1, gap 2 — CITY-05 AC2): approach A (design.md) não atribui
    // NpcId a membro do AggregatePopulationPool — só existe contagem+somas, nunca identidade
    // individual. Isso tornava "consultar um NPC agregado por id" estruturalmente impossível:
    // não havia nenhum id nomeável apontando pro pool (gap achado pelo Verifier independente).
    //
    // Opção descartada: reservar uma faixa de NpcId por cidade (avançar NextNpcId em lockstep
    // com todo crescimento de pool: fundação, carga inicial de cenário, desmaterialização) e
    // guardar [startId, startId+count) na própria City. Rejeitada aqui porque exige (a) mudar a
    // superfície canônica de City (novo campo persistido, entra no hash — arrisca os testes de
    // round-trip/conservação de CITY-04, o núcleo mais frágil da fase) e (b) uma faixa compacta
    // por cidade quebra sob alocação intercalada do contador global entre cidades (reserva da
    // cidade B entre duas reservas da cidade A perfura o intervalo contíguo assumido pela cidade
    // A) — precisaria de uma lista de blocos por cidade, não um único range, pra ficar correto.
    //
    // Escolha: o único id "endereçável" de um membro do pool nunca materializado é exatamente o
    // próximo que o contador global (WorldState.NextNpcId) vai emitir — é o mesmo id que
    // MaterializeOne já atribuiria a esse membro no próximo sorteio. Consultar esse id
    // específico dispara a materialização real (mesmo MaterializeOne, mesmo sorteio) da primeira
    // cidade com pool não vazio, na ordem de world.Cities (determinístico, sem RNG na escolha da
    // cidade). Não adiciona estado novo a City nem ao snapshot — hash/conservação continuam
    // exatamente como antes.
    public static Result<Unit> EnsureMaterialized(WorldState world, NpcId npcId)
    {
        var npc = world.FindNpc(npcId);
        if (npc is not null)
            return npc.IsAlive ? Result<Unit>.Ok(Unit.Value) : Result<Unit>.Fail("Npc: não existe ou está morto");

        if (npcId.Value != world.NextNpcId) return Result<Unit>.Fail("Npc: não existe ou está morto");

        var city = world.Cities.FirstOrDefault(c => c.AggregatePool.Count > 0);
        if (city is null) return Result<Unit>.Fail("Npc: não existe ou está morto");

        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        var materialized = MaterializeOne(world, ctx, city.Id);
        return materialized.IsSuccess ? Result<Unit>.Ok(Unit.Value) : Result<Unit>.Fail(materialized.Error!);
    }
}
