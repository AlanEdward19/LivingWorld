using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Invention;

/// <summary>Busca local no espaço de composição (ADR-0045): parte do que a pessoa conhece (o protótipo que falhou, os designs que
/// sabe, a peça natural dos materiais que viu boiar) e aplica operadores genéricos (mudar medida, duplicar ao lado, escavar,
/// revestir, trocar material, acrescentar parte chata, juntar dois designs, tirar parte, trocar junção, trocar primitiva). A falha
/// anterior puxa os operadores que a corrigiriam. Nenhum design pronto: toda parte nasce de um material ou de outra parte.</summary>
internal static class DesignSearch
{
    private enum Op { Scale, Duplicate, Hollow, Seal, SwapMaterial, AddFlat, Recombine, Remove, Joint, Reshape }

    private static readonly Op[] All = Enum.GetValues<Op>();

    /// <summary>Peça natural de um material: o tronco, o feixe, a lâmina, o bloco, do tamanho que a natureza dá.</summary>
    internal static DesignPart NaturalPiece(MaterialSpec m)
    {
        short length = (short)m.MaxPieceMm, thick = (short)m.MaxThicknessMm;
        return m.NaturalShape switch
        {
            PartShape.Box => new DesignPart(PartShape.Box, length, (short)Math.Max(thick, m.MaxPieceMm / 2), thick, 0, (short)m.Id, 0, JointKind.Fixed, -1, 0, 0, 0),
            PartShape.Sphere => new DesignPart(PartShape.Sphere, thick, thick, thick, 0, (short)m.Id, 0, JointKind.Fixed, -1, 0, 0, 0),
            _ => new DesignPart(m.NaturalShape, length, thick, thick, 0, (short)m.Id, 0, JointKind.Fixed, -1, 0, 0, 0),
        };
    }

    /// <summary>Uma variante: a semente com 1 a 3 operadores sorteados (pesados pela última falha).</summary>
    internal static List<DesignPart> Variant(
        IReadOnlyList<DesignPart> seed, IReadOnlyList<IReadOnlyList<DesignPart>> known, IReadOnlyList<MaterialSpec> usable,
        FailureMode lastFailure, WorldRng rng)
    {
        var parts = seed.ToList();
        int ops = 1 + (int)(rng.NextDouble() * 3);
        for (int k = 0; k < ops; k++) Apply(parts, Pick(lastFailure, rng), known, usable, rng);
        return parts;
    }

    /// <summary>A peça feita de verdade: cada medida com o erro de quem fabrica (‰), arredondada a 10 mm.</summary>
    internal static List<DesignPart> AsBuilt(IReadOnlyList<DesignPart> parts, IReadOnlyList<MaterialSpec> materials, int noisePm, WorldRng rng)
    {
        var built = new List<DesignPart>(parts.Count);
        foreach (var p in parts)
        {
            short Jitter(short v, int min) => (short)Math.Clamp(Round10(v * (1000 + (int)((rng.NextDouble() * 2 - 1) * noisePm)) / 1000), min, short.MaxValue);
            var wall = p.WallMm == 0 ? (short)0 : (short)Math.Max(1, (int)(p.WallMm * (1000 + (rng.NextDouble() * 2 - 1) * noisePm) / 1000));
            var made = p with { LengthMm = Jitter(p.LengthMm, 10), WidthMm = Jitter(p.WidthMm, 1), HeightMm = Jitter(p.HeightMm, 1), WallMm = wall };
            built.Add(Find(materials, p.Material) is { } m ? FitTo(made, m) : made); // a peça não sai maior que a natureza dá
        }
        return built;
    }

    private static Op Pick(FailureMode failure, WorldRng rng)
    {
        Op[] favored = failure switch
        {
            FailureMode.Sinks => [Op.Scale, Op.Duplicate, Op.Hollow, Op.Seal, Op.SwapMaterial],
            FailureMode.Capsizes => [Op.Duplicate, Op.Recombine, Op.Scale, Op.AddFlat],
            FailureMode.Breaks => [Op.Scale, Op.Joint, Op.SwapMaterial, Op.Duplicate],
            FailureMode.TooSlow => [Op.AddFlat, Op.Reshape, Op.Remove, Op.Scale],
            _ => All,
        };
        return rng.NextDouble() < 0.7 ? favored[(int)(rng.NextDouble() * favored.Length)] : All[(int)(rng.NextDouble() * All.Length)];
    }

    private static void Apply(List<DesignPart> parts, Op op, IReadOnlyList<IReadOnlyList<DesignPart>> known, IReadOnlyList<MaterialSpec> usable, WorldRng rng)
    {
        int i = (int)(rng.NextDouble() * parts.Count);
        var p = parts[i];
        var m = Find(usable, p.Material);
        switch (op)
        {
            case Op.Scale:
                {
                    double factor = 0.6 + rng.NextDouble();
                    int axis = (int)(rng.NextDouble() * 3);
                    parts[i] = axis switch
                    {
                        0 => p with { LengthMm = Scale(p.LengthMm, factor) },
                        1 => p with { WidthMm = Scale(p.WidthMm, factor) },
                        _ => p with { HeightMm = Scale(p.HeightMm, factor) },
                    };
                    break;
                }
            case Op.Duplicate when parts.Count < Design.MaxParts:
                {
                    int side = rng.NextDouble() < 0.5 ? 1 : -1;
                    int edge = parts.Max(q => side * q.OffsetYMm + q.SpanY / 2);
                    var joint = rng.NextDouble() < 0.6 ? JointKind.Flexible : JointKind.Fixed;
                    parts.Add(p with { Parent = (sbyte)i, Joint = joint, OffsetYMm = Clamp16(side * (edge + p.SpanY / 2)) });
                    break;
                }
            case Op.Hollow when m is not null:
                {
                    int span = Math.Min(p.SpanY, p.SpanZ);
                    int wall = p.IsHollow ? Scale(p.WallMm, 0.6 + rng.NextDouble()) : Math.Max(m.MinWallMm, span / (4 + (int)(rng.NextDouble() * 8)));
                    parts[i] = p with { WallMm = (short)Math.Clamp(wall, 0, Math.Max(0, span / 2 - 1)) };
                    break;
                }
            case Op.Seal:
                {
                    var sealers = usable.Where(s => s.CoatingOnly || (s.Formable && s.ImpermeabilityPm >= 600)).ToList();
                    parts[i] = p.Coating != 0 || sealers.Count == 0
                        ? p with { Coating = 0 }
                        : p with { Coating = (short)sealers[(int)(rng.NextDouble() * sealers.Count)].Id };
                    break;
                }
            case Op.SwapMaterial:
                {
                    var structural = usable.Where(s => !s.CoatingOnly && s.Id != p.Material).ToList();
                    if (structural.Count == 0) break;
                    var to = structural[(int)(rng.NextDouble() * structural.Count)];
                    parts[i] = FitTo(p with { Material = (short)to.Id }, to);
                    break;
                }
            case Op.AddFlat when parts.Count < Design.MaxParts:
                {
                    var structural = usable.Where(s => !s.CoatingOnly).ToList();
                    if (structural.Count == 0) break;
                    var mat = structural[(int)(rng.NextDouble() * structural.Count)];
                    short thick = (short)Math.Max(mat.MinWallMm, Math.Min(mat.MaxThicknessMm, Math.Max(1, p.SpanY / 8)));
                    short length = (short)Math.Clamp(p.LengthMm / (2 + (int)(rng.NextDouble() * 3)), 10, mat.MaxPieceMm);
                    short width = (short)Math.Clamp(Math.Max(thick * 8, p.SpanY / 2), thick, Math.Max(thick, Math.Min(mat.MaxPieceMm, (int)length)));
                    var joint = rng.NextDouble() < 0.5 ? JointKind.Hinge : JointKind.Fixed;
                    parts.Add(new DesignPart(PartShape.Box, length, width, thick, 0, (short)mat.Id, 0, joint, (sbyte)i,
                        p.OffsetXMm, Clamp16(p.OffsetYMm + (rng.NextDouble() < 0.5 ? 1 : -1) * (p.SpanY / 2 + width / 2)), Clamp16(p.Top)));
                    break;
                }
            case Op.Recombine when known.Count > 0:
                {
                    var other = known[(int)(rng.NextDouble() * known.Count)];
                    int baseIndex = parts.Count;
                    int shift = parts.Max(q => q.OffsetYMm + q.SpanY / 2) + other.Max(q => q.SpanY / 2 - q.OffsetYMm);
                    for (int k = 0; k < other.Count && parts.Count < Design.MaxParts; k++)
                    {
                        var q = other[k];
                        parts.Add(q with
                        {
                            Parent = (sbyte)(k == 0 ? 0 : baseIndex + q.Parent),
                            Joint = k == 0 ? JointKind.Flexible : q.Joint,
                            OffsetYMm = Clamp16(q.OffsetYMm + shift),
                        });
                    }
                    break;
                }
            case Op.Remove when parts.Count > 1 && i > 0:
                {
                    parts.RemoveAt(i);
                    for (int k = 1; k < parts.Count; k++)
                    {
                        var q = parts[k];
                        int parent = q.Parent == i ? Math.Max(0, i - 1) : q.Parent > i ? q.Parent - 1 : q.Parent;
                        parts[k] = q with { Parent = (sbyte)Math.Min(parent, k - 1) };
                    }
                    break;
                }
            case Op.Joint when i > 0:
                parts[i] = p with { Joint = (JointKind)(int)(rng.NextDouble() * 5) };
                break;
            case Op.Reshape:
                {
                    var shape = (PartShape)(int)(rng.NextDouble() * 4);
                    parts[i] = shape == PartShape.Sphere ? p with { Shape = shape, LengthMm = (short)p.SpanY } : p with { Shape = shape };
                    break;
                }
        }
    }

    /// <summary>Medidas cortadas ao que o novo material dá na natureza.</summary>
    private static DesignPart FitTo(DesignPart p, MaterialSpec m)
    {
        int crossLimit = p.IsHollow && m.Formable ? m.MaxPieceMm : m.MaxThicknessMm;
        short wall = p.IsHollow ? (short)Math.Clamp(p.WallMm, m.MinWallMm, m.MaxThicknessMm) : (short)0;
        return p with
        {
            LengthMm = (short)Math.Min(p.LengthMm, m.MaxPieceMm),
            WidthMm = (short)Math.Min(p.WidthMm, p.Shape == PartShape.Box && !p.IsHollow ? m.MaxPieceMm : crossLimit),
            HeightMm = (short)Math.Min(p.HeightMm, crossLimit),
            WallMm = wall,
        };
    }

    private static short Scale(short value, double factor) => (short)Math.Clamp(Round10((int)(value * factor)), 1, short.MaxValue);

    private static int Round10(int value) => value >= 20 ? (value + 5) / 10 * 10 : Math.Max(1, value);

    private static short Clamp16(int value) => (short)Math.Clamp(value, short.MinValue, short.MaxValue);

    private static MaterialSpec? Find(IReadOnlyList<MaterialSpec> materials, int id)
    {
        foreach (var m in materials) if (m.Id == id) return m;
        return null;
    }
}
