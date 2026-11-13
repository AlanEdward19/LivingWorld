using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Tests.Unit.Invention;

/// <summary>AD-141: o avaliador físico decide por leis (Arquimedes, metacentro, viga, arrasto), nunca por tabela de artefatos.
/// As peças dos testes saem da natureza do cenário (a peça natural de cada material), não de literais de design.</summary>
public class PhysicsEvaluatorTests
{
    private static readonly InventionRules Rules = InventionRules.Default;
    private const int Rider = 65;

    private static MaterialSpec Floating => Rules.Materials.First(m => !m.CoatingOnly && m.FloatsIn(Rules.WaterDensityKgM3) && m.NaturalShape == PartShape.Cylinder && m.MaxThicknessMm >= 400);

    private static DesignPart Natural(MaterialSpec m) => new(m.NaturalShape, (short)m.MaxPieceMm, (short)m.MaxThicknessMm, (short)m.MaxThicknessMm, 0, (short)m.Id, 0, JointKind.Fixed, -1, 0, 0, 0);

    /// <summary>A mesma peça repetida lado a lado, amarrada à primeira.</summary>
    private static List<DesignPart> SideBySide(DesignPart piece, int count)
    {
        var parts = new List<DesignPart> { piece };
        for (int k = 1; k < count; k++)
            parts.Add(piece with { Parent = 0, Joint = JointKind.Flexible, OffsetYMm = (short)((k % 2 == 1 ? 1 : -1) * ((k + 1) / 2) * piece.SpanY) });
        return parts;
    }

    [Fact]
    public void A_single_floating_log_carries_a_rider_but_rolls_over()
    {
        var perf = PhysicsEvaluator.Evaluate([Natural(Floating)], Rules, Rider);

        Assert.Equal(FailureMode.Capsizes, perf.Failure);
    }

    [Fact]
    public void Three_floating_logs_lashed_side_by_side_float_stay_upright_hold_and_move()
    {
        var perf = PhysicsEvaluator.Evaluate(SideBySide(Natural(Floating), 3), Rules, Rider);

        Assert.True(perf.Works, $"falhou: {perf}");
    }

    [Fact]
    public void A_block_of_material_denser_than_water_sinks()
    {
        var dense = Rules.Materials.First(m => !m.CoatingOnly && !m.FloatsIn(Rules.WaterDensityKgM3));

        Assert.Equal(FailureMode.Sinks, PhysicsEvaluator.Evaluate([Natural(dense)], Rules, Rider).Failure);
    }

    [Fact]
    public void A_heavier_rider_sinks_a_raft_that_carries_a_lighter_one()
    {
        var raft = SideBySide(Natural(Floating), 3);
        long payload = PhysicsEvaluator.Evaluate(raft, Rules, Rider).PayloadG / 1000;

        Assert.Equal(FailureMode.Sinks, PhysicsEvaluator.Evaluate(raft, Rules, Rider + (int)payload + 1).Failure);
    }

    [Fact]
    public void A_piece_larger_than_nature_gives_is_not_buildable()
    {
        var log = Natural(Floating);

        Assert.Equal(FailureMode.Invalid, PhysicsEvaluator.Evaluate([log with { WidthMm = (short)(log.WidthMm + 10) }], Rules, Rider).Failure);
    }

    [Fact]
    public void A_flat_hinged_part_pushes_water_better_than_bare_hands()
    {
        var raft = SideBySide(Natural(Floating), 3);
        var log = raft[0];
        var flat = new DesignPart(PartShape.Box, (short)(log.LengthMm / 4), (short)(log.SpanY / 2), (short)(log.SpanY / 16), 0, log.Material, 0, JointKind.Hinge, 0, 0, 0, (short)log.Top);

        int bare = PhysicsEvaluator.Evaluate(raft, Rules, Rider).SpeedMmS;
        int paddled = PhysicsEvaluator.Evaluate([.. raft, flat], Rules, Rider).SpeedMmS;

        Assert.True(paddled > bare, $"remo {paddled} mm/s contra mãos {bare} mm/s");
    }

    /// <summary>Teste de perturbação física (ADR-0045, teste 3): a madeira mais densa que a água afunda como peça maciça em qualquer
    /// composição gerada pelos operadores de busca; só um casco oco estanque ainda desloca mais água do que pesa (Arquimedes: é
    /// assim que um navio de ferro boia), então nenhum design de madeira maciça é aceito.</summary>
    [Fact]
    public void Wood_denser_than_water_no_solid_wooden_design_is_accepted()
    {
        var dense = Perturbed(densityKgM3: 1100, impermeabilityPm: null);
        var accepted = WoodenVariants(dense).Where(v => PhysicsEvaluator.Evaluate(v, dense, Rider).Works).ToList();

        Assert.DoesNotContain(accepted, v => v.All(p => !p.IsHollow));
    }

    /// <summary>A mesma perturbação com a madeira que encharca (impermeabilidade abaixo da estanque): sem casco que segure a água
    /// fora, nenhum design só de madeira é aceito, em nenhuma composição.</summary>
    [Fact]
    public void Wood_denser_than_water_and_porous_no_wooden_design_at_all_is_accepted()
    {
        var dense = Perturbed(densityKgM3: 1100, impermeabilityPm: 100);

        Assert.DoesNotContain(WoodenVariants(dense), v => PhysicsEvaluator.Evaluate(v, dense, Rider).Works);
    }

    [Fact]
    public void Same_parts_always_give_the_same_measure()
    {
        var raft = SideBySide(Natural(Floating), 3);

        Assert.Equal(PhysicsEvaluator.Evaluate(raft, Rules, Rider), PhysicsEvaluator.Evaluate(raft.ToList(), Rules, Rider));
    }

    private static InventionRules Perturbed(int densityKgM3, int? impermeabilityPm) => Rules with
    {
        Materials = Rules.Materials.Select(m => m.Id == Floating.Id
            ? m with { DensityKgM3 = densityKgM3, ImpermeabilityPm = impermeabilityPm ?? m.ImpermeabilityPm }
            : m).ToList(),
    };

    /// <summary>Milhares de composições só de madeira, a partir da peça natural, pelos mesmos operadores que o NPC usa.</summary>
    private static List<List<DesignPart>> WoodenVariants(InventionRules rules)
    {
        var wood = rules.Material(Floating.Id)!;
        var usable = new[] { wood };
        var rng = new WorldRng(7);
        var variants = new List<List<DesignPart>>();
        IReadOnlyList<DesignPart> seed = [LivingWorld.Simulation.Invention.DesignSearch.NaturalPiece(wood)];
        for (int i = 0; i < 3000; i++)
        {
            var v = LivingWorld.Simulation.Invention.DesignSearch.Variant(seed, [], usable, (FailureMode)(i % 6), rng);
            variants.Add(v);
            if (i % 5 == 0) seed = v;
        }
        return variants;
    }
}
