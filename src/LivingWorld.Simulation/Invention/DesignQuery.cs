using System.Globalization;
using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Invention;

/// <summary>Uma parte para o cliente desenhar: forma no vocabulário de <c>ObjectPart</c> (box [x,y,z] · cylinder [raio,altura] ·
/// sphere [raio] · capsule [raio,altura]) em metros, offset do centro em metros (Y para cima), parede, junção à mãe e material.
/// <c>Axis</c> "x": cilindro e cápsula deitados ao longo do comprimento (o cliente gira a malha, que nasce em pé).</summary>
public sealed record DesignPartView(string Shape, IReadOnlyList<double> Size, IReadOnlyList<double> Offset, string Axis, double WallM, string Joint, int Parent, int Material, int Coating);

/// <summary>Linha de <c>GET /designs</c>.</summary>
public sealed record DesignListing(int Id, bool Lost, bool Works, int Knowers, int Items);

/// <summary>Aparência de um material: cor, acabamento (0..1), grão (0..1), condutividade (0..1). O cliente deriva albedo,
/// rugosidade, metálico e textura daqui; nenhum nome, nenhum modelo.</summary>
public sealed record MaterialLookView(int Id, string Color, double Finish, double Grain, double Conductivity);

/// <summary>Contrato de <c>GET /designs/{id}</c> (AD-141): partes, materiais e o que o avaliador mediu; lápide = só a proveniência.</summary>
public sealed record DesignView(
    int Id, bool Lost, long InventorId, long Tick, int ParentId, int SecondParentId, string Origin,
    IReadOnlyList<DesignPartView> Parts, IReadOnlyList<MaterialLookView> Materials,
    bool Works, string Failure, int SpeedMmS, long PayloadKg, int DraftMm, int MetacentricMm, int Knowers, int Items);

public static class DesignQuery
{
    public const string Unknown = "Design: não existe";

    /// <summary>Designs vivos e lápides, por id.</summary>
    public static IReadOnlyList<DesignListing> All(WorldState world)
    {
        if (world.Invention is not { } ledger || world.InventionRules is not { } rules) return [];
        return ledger.Designs.Select(d => new DesignListing(d.Id, false, ledger.PerformanceOf(d, rules).Works,
                ledger.Known.Count(k => k.DesignId == d.Id && world.FindNpc(new NpcId(k.NpcId)) is { IsAlive: true }),
                ledger.Items.Count(i => i.DesignId == d.Id)))
            .Concat(ledger.Tombstones.Select(t => new DesignListing(t.DesignId, true, false, 0, 0)))
            .OrderBy(l => l.Id).ToList();
    }

    public static Result<DesignView> Of(WorldState world, int id)
    {
        if (world.Invention is not { } ledger || world.InventionRules is not { } rules) return Result<DesignView>.Fail(Unknown);
        if (ledger.FindDesign(id) is not { } design)
        {
            foreach (var t in ledger.Tombstones)
                if (t.DesignId == id)
                    return Result<DesignView>.Ok(new DesignView(id, true, t.InventorId, t.InventedTick, 0, 0, "", [], [], false, "", 0, 0, 0, 0, 0, 0));
            return Result<DesignView>.Fail(Unknown);
        }

        var perf = ledger.PerformanceOf(design, rules);
        var parts = design.Parts.Select(View).ToList();
        var materials = design.Parts.SelectMany(p => new[] { (int)p.Material, p.Coating }).Where(m => m > 0).Distinct().Order()
            .Select(m => rules.Material(m)).OfType<MaterialSpec>()
            .Select(m => new MaterialLookView(m.Id, string.Create(CultureInfo.InvariantCulture, $"#{m.ColorRgb:x6}"),
                m.FinishPm / 1000.0, m.GrainPm / 1000.0, m.ConductivityPm / 1000.0))
            .ToList();
        int knowers = ledger.Known.Count(k => k.DesignId == id && world.FindNpc(new NpcId(k.NpcId)) is { IsAlive: true });
        int items = ledger.Items.Count(i => i.DesignId == id);
        return Result<DesignView>.Ok(new DesignView(
            design.Id, false, design.InventorId, design.Tick, design.ParentId, design.SecondParentId, design.Origin.ToString(),
            parts, materials, perf.Works, perf.Failure.ToString(), perf.SpeedMmS, perf.PayloadG / 1000, perf.DraftMm, perf.MetacentricMm,
            knowers, items));
    }

    /// <summary>Converte mm do motor (X comprido, Z para cima, base em Z) para metros do cliente (Y para cima, centro).</summary>
    private static DesignPartView View(DesignPart p)
    {
        const double M = 0.001;
        IReadOnlyList<double> size = p.Shape switch
        {
            PartShape.Box => [p.LengthMm * M, p.HeightMm * M, p.WidthMm * M],
            PartShape.Sphere => [p.LengthMm * M / 2],
            _ => [p.WidthMm * M / 2, p.LengthMm * M],
        };
        string shape = p.Shape.ToString().ToLowerInvariant();
        string axis = p.Shape is PartShape.Cylinder or PartShape.Capsule ? "x" : "";
        return new DesignPartView(shape, size, [p.OffsetXMm * M, (p.OffsetZMm + p.SpanZ / 2.0) * M, p.OffsetYMm * M],
            axis, p.WallMm * M, p.Joint.ToString().ToLowerInvariant(), p.Parent, p.Material, p.Coating);
    }
}
