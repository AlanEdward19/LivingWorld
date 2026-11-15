using LivingWorld.Domain.Invention;

namespace LivingWorld.Simulation.Invention;

/// <summary>Levar por terra (AD-153), dentro do problema de carregar (AD-147): quem teve a compra cortada pelo que os braços levam
/// também imagina arrastar, deslizar ou rolar a carga. Não há caminho próprio: a mesma mente, a mesma busca, o mesmo protótipo, o
/// mesmo ensino, cópia, encomenda e venda. O que muda é o juízo: cada ideia passa pela sonda de conter e pela de terra, e vale a
/// que resolve mais. Trenó, padiola ou carroça é o nome que alguém dá ao que a sonda de terra mostrou puxar mais que os braços.</summary>
internal static class HaulInvention
{
    public const byte Function = 2;

    /// <summary>O que a física diz que a ideia resolve para quem tem esta necessidade: a função (carregar ou levar por terra), se
    /// funciona, o modo de falha (que puxa os operadores da próxima tentativa) e a pontuação. Com <see cref="InventionRules.LandVehicles"/>
    /// desligado, é exatamente o juízo do recipiente (AD-147).</summary>
    internal readonly record struct Judgment(byte Function, bool Works, FailureMode Failure, long Score);

    internal static Judgment Judge(IReadOnlyList<DesignPart> parts, InventionMind mind, InventionRules rules, IReadOnlyList<MaterialSpec> materials)
    {
        var c = PhysicsEvaluator.Contain(parts, rules, materials);
        var carry = new Judgment(CarryInvention.Function, c.Works, c.Failure, CarryInvention.Score(c, mind, rules));
        if (!rules.LandVehicles) return carry;
        var h = PhysicsEvaluator.Haul(parts, rules, materials);
        long score = Score(h, mind, rules);
        if (score <= carry.Score) return carry;
        // puxa, mas não mais que os braços: não resolve nada (falha como pequeno demais)
        bool useful = h.Works && h.CapacityG > rules.FootCarryKg * 1000L;
        return new Judgment(Function, useful, useful ? FailureMode.None : h.Works ? FailureMode.TooSmall : h.Failure, score);
    }

    /// <summary>Quanto a ideia resolve o que ficou no mercado: só conta o que passa do que os braços já levam, até a necessidade; o
    /// peso próprio pesa pouco (vai no chão), o esforço de fazer pesa como no recipiente.</summary>
    internal static long Score(Haulage h, InventionMind mind, InventionRules rules)
    {
        long needG = Math.Max(rules.SmallestUnitMl(), mind.NeedMl) * (long)rules.WaterDensityKgM3 / 1000;
        long beyond = h.CapacityG - rules.FootCarryKg * 1000L;
        return h.Failure switch
        {
            FailureMode.None => InventionSystem.WorksScore + Math.Min(beyond, needG) * 10 - h.MassG - h.BuildMinutes * 20L,
            FailureMode.Breaks => 300_000 + Math.Min(h.RoomG, 100_000),
            FailureMode.TooHeavy => 250_000 - Math.Min(200_000, h.MassG / 10),
            FailureMode.TooSmall => 100_000 + Math.Min(h.RoomG, 100_000),
            _ => 0,
        };
    }

    /// <summary>Um tabuleiro chato e maciço do material, com a área para o que precisava levar (o que faltou mais o que os braços já
    /// levam) empilhado na altura de uma carga; nulo se a natureza não dá a peça.</summary>
    internal static DesignPart? NeedDeck(MaterialSpec m, int needMl, InventionRules rules)
    {
        long ml = needMl + (long)rules.FootCarryKg * 1000 * 1000 / rules.WaterDensityKgM3;
        long area = ml * 1000 / Math.Max(1, rules.PileHeightMm);
        int side = (int)Math.Min(short.MaxValue, (PhysicsEvaluator.ISqrt(area) + 9) / 10 * 10);
        int thick = Math.Clamp(side / 15, m.MinWallMm, m.MaxThicknessMm);
        var p = new DesignPart(PartShape.Box, (short)side, (short)side, (short)thick, 0, (short)m.Id, 0, JointKind.Fixed, -1, 0, 0, 0);
        return PhysicsEvaluator.IsBuildable([p], [m]) ? p : null;
    }
}
