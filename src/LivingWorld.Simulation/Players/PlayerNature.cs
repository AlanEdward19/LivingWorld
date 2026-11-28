using System.Runtime.CompilerServices;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Players;

/// <summary>#239/#241: onde há árvore para o corpo conduzido cortar lenha. A árvore é a que o cliente desenha (<c>scenery.gd</c>): terra seca,
/// fora do contorno das cidades (já crescido, mais uma célula de folga) e com o sorteio da célula abaixo de 16%; o motor diz onde cada
/// árvore está (o sorteio é função pura da célula, o mesmo <c>cell_hash</c> do cliente, travado por teste de paridade) e o cliente
/// desenha o que o motor aceita. A oferta <c>nature</c>/<c>gather</c> só aparece a até 1,5 m do tronco. Só lê; o limite das cidades
/// fica em memória por hora de mundo.</summary>
internal static class PlayerNature
{
    /// <summary>Chance de uma célula ter árvore (o <c>roll &lt; 0.16</c> do cliente).</summary>
    public const double TreeChance = 0.16;

    /// <summary>Até onde se alcança o tronco com o machado: 1,5 m (uma célula na escala de 1,5 m).</summary>
    public const double ReachMeters = 1.5;

    /// <summary>Uma árvore: a célula e o tronco em milésimos de célula absolutos (o jitter do cliente, ±0,3 célula).</summary>
    public readonly record struct Tree(CellCoord Cell, long FineX, long FineY);

    private sealed class Cache
    {
        public long Hour = -1;
        public List<CityBounds> Cities = [];
        public List<ClearZone> Clear = [];
    }

    /// <summary>#258: um retângulo de células sem árvore (quintal limpo em volta dos prédios, rua e calçada): origem e tamanho.</summary>
    public readonly record struct ClearZone(int X, int Y, int Width, int Height)
    {
        public bool Contains(CellCoord cell) => cell.X >= X && cell.X < X + Width && cell.Y >= Y && cell.Y < Y + Height;
    }

    private static readonly ConditionalWeakTable<WorldState, Cache> Caches = new();

    /// <summary>O mesmo <c>Scenery.cell_hash</c> do cliente (inteiros de 64 bits, deslocamento aritmético): 0..1 por célula e sal.</summary>
    public static double CellHash(CellCoord cell, int salt = 0)
    {
        unchecked
        {
            long h = ((long)cell.X * 73856093L) ^ ((long)cell.Y * 19349663L) ^ ((long)salt * 83492791L);
            h = (h ^ (h >> 13)) * 1274126177L;
            long a = h == long.MinValue ? h : Math.Abs(h);
            return (a % 10000) / 10000.0;
        }
    }

    /// <summary>A árvore da célula, ou nula: seca, fora das cidades e sem prédio, com o sorteio do cliente.</summary>
    public static Tree? TreeAt(WorldState world, CellCoord coord)
    {
        if (CellHash(coord) >= TreeChance || !world.Map.TryGetCell(coord, out var cell) || cell.HasWater) return null;
        foreach (var b in Known(world).Cities)
            if (coord.X >= b.Origin.X - 1 && coord.X <= b.Origin.X + b.Width && coord.Y >= b.Origin.Y - 1 && coord.Y <= b.Origin.Y + b.Height) return null;
        if (PlayerGround.At(world, coord) is not null) return null;
        foreach (var zone in Known(world).Clear)
            if (zone.Contains(coord)) return null; // #258: quintal limpo, rua e calçada: o cenário os declara
        long x = coord.X * (long)FinePose.PerCell + FinePose.PerCell / 2 + (long)Math.Round((CellHash(coord, 1) - 0.5) * 0.6 * FinePose.PerCell);
        long y = coord.Y * (long)FinePose.PerCell + FinePose.PerCell / 2 + (long)Math.Round((CellHash(coord, 2) - 0.5) * 0.6 * FinePose.PerCell);
        return new Tree(coord, x, y);
    }

    /// <summary>#243: a madeira de uma árvore inteira, em unidades: de 30% a 70% da casa de material do cenário
    /// (<c>HouseMaterialUnits</c>, 20 por padrão: 6 a 14), pelo mesmo sorteio que dá o porte da árvore no cliente (<c>cell_hash</c> sal 3):
    /// árvore maior rende mais. Função pura da célula.</summary>
    public static long Capacity(WorldState world, CellCoord cell) =>
        Math.Max(2, Math.Max(1, world.StreetRules.HouseMaterialUnits) * (30 + (long)(CellHash(cell, 3) * 40)) / 100);

    /// <summary>#243: quanto tempo a árvore leva para recuperar uma unidade de madeira: 30 dias do calendário (uma árvore derrubada de 10
    /// unidades volta inteira em 10 meses). Regra simples e determinística enquanto a flora (#199) não dá planta por planta.</summary>
    public static long RegrowMsPerUnit(WorldState world) => Math.Max(1, world.Calendar.HoursPerDay) * 30L * WorldMillis.PerHour;

    /// <summary>#243: a madeira que a árvore da célula tem agora: a inteira se ninguém a tocou; senão o que sobrou do último corte mais o
    /// que cresceu desde então (conta pura do livro e do relógio; nada por tick).</summary>
    public static long Left(WorldState world, CellCoord cell, long nowMs)
    {
        long cap = Capacity(world, cell);
        return world.Trees?.Find(cell.X, cell.Y) is { } cut ? Math.Min(cap, cut.Left + Math.Max(0, nowMs - cut.AtMs) / RegrowMsPerUnit(world)) : cap;
    }

    /// <summary>O estágio que o cliente desenha: <c>whole</c> (inteira), <c>cut</c> (cortada em parte: copa menor) ou <c>stump</c> (toco).</summary>
    public static string StageOf(long left, long capacity) => left >= capacity ? "whole" : left <= 0 ? "stump" : "cut";

    /// <summary>#243: tira até <paramref name="units"/> da madeira da árvore da célula, agora, e devolve quanto saiu (0 sem madeira). Registra no
    /// livro o que sobrou e de que instante volta a crescer; o livro solta as árvores que já voltaram inteiras. Quem corta (o corpo do
    /// jogador, o NPC da obra) tira da mesma árvore: o mundo é um só.</summary>
    public static long Take(WorldState world, CellCoord cell, long nowMs, long units)
    {
        if (units <= 0) return 0;
        long cap = Capacity(world, cell), rate = RegrowMsPerUnit(world);
        var book = world.TreeBook();
        bool fresh = book.Find(cell.X, cell.Y) is null; // ainda intocada: estava inteira
        var cut = book.Touch(cell.X, cell.Y);
        long left = cap;
        long since = nowMs;
        if (!fresh)
        {
            long grown = Math.Max(0, nowMs - cut.AtMs) / rate;
            left = Math.Min(cap, cut.Left + grown);
            since = left >= cap ? nowMs : cut.AtMs + grown * rate; // o que já cresceu pela metade da próxima unidade não se perde
        }
        long taken = Math.Min(units, left);
        left -= taken;
        cut.Left = left;
        cut.AtMs = since;
        for (int i = book.Cuts.Count - 1; i >= 0; i--) // as que já voltaram inteiras saem do livro
        {
            var other = book.Cuts[i];
            if (other == cut && left < cap) continue;
            if (other.Left + Math.Max(0, nowMs - other.AtMs) / rate >= Capacity(world, new CellCoord(other.X, other.Y))) book.Remove(other);
        }
        world.DropEmptyTrees();
        return taken;
    }

    /// <summary>Devolve à árvore a madeira que tirou e não pôde ser levada (o inverso exato de <see cref="Take"/> no mesmo instante).</summary>
    internal static void Give(WorldState world, CellCoord cell, long nowMs, long units)
    {
        if (world.Trees?.Find(cell.X, cell.Y) is not { } cut) return;
        cut.Left += units;
        world.CanonicalHashCache.MarkPropertyDirty(nameof(WorldState.Trees));
    }

    /// <summary>A árvore viva (com madeira) mais perto de <paramref name="from"/>, até <paramref name="radius"/> células (anéis, menor
    /// distância; empate: menor linha, depois coluna); nula sem nenhuma. É a que os NPCs da obra vão cortar.</summary>
    public static CellCoord? NearestLive(WorldState world, CellCoord from, int radius)
    {
        long now = world.CurrentDate.TotalMilliseconds;
        for (int r = 0; r <= radius; r++)
        {
            CellCoord? best = null;
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    var cell = new CellCoord(from.X + dx, from.Y + dy);
                    if (TreeAt(world, cell) is null || Left(world, cell, now) <= 0) continue;
                    if (best is null || cell.Y < best.Value.Y || (cell.Y == best.Value.Y && cell.X < best.Value.X)) best = cell;
                }
            if (best is not null) return best;
        }
        return null;
    }

    /// <summary>As árvores tocadas agora, com o que cada uma tem (a que já voltou inteira não aparece): o que o cliente desenha em lugar da
    /// regra natural. Ordem do livro (linha, coluna).</summary>
    public static IReadOnlyList<(CellCoord Cell, long Left, long Capacity)> Touched(WorldState world)
    {
        var list = new List<(CellCoord, long, long)>();
        if (world.Trees is not { } book) return list;
        long now = world.CurrentDate.TotalMilliseconds;
        foreach (var cut in book.Cuts)
        {
            var cell = new CellCoord(cut.X, cut.Y);
            long cap = Capacity(world, cell), left = Left(world, cell, now);
            if (left < cap) list.Add((cell, left, cap));
        }
        return list;
    }

    /// <summary>A célula é mata: tem árvore.</summary>
    public static bool IsWild(WorldState world, MapCell cell, ResourceType resource) => TreeAt(world, cell.Coord) is not null;

    /// <summary>A árvore COM madeira mais perto ao alcance do corpo (do ponto fino ao tronco, <see cref="ReachMeters"/>); empate: menor linha, depois coluna.</summary>
    public static Tree? NearestWithinReach(WorldState world, CellCoord at, FinePose? fine)
    {
        var from = fine is { } f && f.CellX == at.X && f.CellY == at.Y ? f : new FinePose(at.X * FinePose.PerCell + 500, at.Y * FinePose.PerCell + 500, 0, 0, 0);
        double reach = ReachMeters / Math.Max(0.1, world.Players.CellM) * FinePose.PerCell; // milésimos de célula
        Tree? best = null;
        double bestD2 = double.MaxValue;
        for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                if (TreeAt(world, new CellCoord(at.X + dx, at.Y + dy)) is not { } tree || Left(world, tree.Cell, world.CurrentDate.TotalMilliseconds) <= 0) continue; // toco não oferece
                double ex = tree.FineX - from.X, ey = tree.FineY - from.Y, d2 = ex * ex + ey * ey;
                if (d2 > reach * reach || d2 >= bestD2) continue;
                (best, bestD2) = (tree, d2);
            }
        return best;
    }

    private static Cache Known(WorldState world)
    {
        var cache = Caches.GetOrCreateValue(world);
        long hour = world.CurrentDate.TotalHours;
        if (cache.Hour == hour) return cache;
        cache.Hour = hour;
        cache.Cities = [];
        foreach (var city in world.ActiveCities())
            cache.Cities.Add(LivingWorld.Simulation.Cities.CityOccupancy
                .ResolveGrownBounds(world, city, LivingWorld.Simulation.Cities.Queries.CityPopulationQuery.Population(world, city.Id)).Bounds);
        cache.Clear = ComputeClear(world);
        return cache;
    }

    /// <summary>#258: onde o cenário diz que não há árvore: o entorno de cada prédio (a caixa da planta mais <c>ClearAroundBuildingsCells</c>) e as células com
    /// calçamento (rua e calçada), em faixas por linha. Vazio sem a regra: o mundo de antes não paga nada.</summary>
    private static List<ClearZone> ComputeClear(WorldState world)
    {
        var rules = world.StreetRules;
        var zones = new List<ClearZone>();
        if (rules.ClearAroundBuildingsCells <= 0) return zones;
        int margin = rules.ClearAroundBuildingsCells;
        foreach (var building in world.Buildings)
        {
            if (building.Position is not { } origin) continue;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var cell in LivingWorld.Domain.Cities.Buildings.BuildingFootprintGenerator.Generate(building))
            {
                minX = Math.Min(minX, cell.Cell.X); maxX = Math.Max(maxX, cell.Cell.X);
                minY = Math.Min(minY, cell.Cell.Y); maxY = Math.Max(maxY, cell.Cell.Y);
            }
            if (minX == int.MaxValue) continue;
            zones.Add(new ClearZone(origin.X + minX - margin, origin.Y + minY - margin, maxX - minX + 1 + 2 * margin, maxY - minY + 1 + 2 * margin));
        }

        if (world.Ground is { } ground && world.StreetsOn)
            for (int y = 0; y < ground.Height; y++)
            {
                int runStart = -1;
                for (int x = 0; x <= ground.Width; x++)
                {
                    bool paved = x < ground.Width && ground.PavingAt(y * ground.Width + x) != LivingWorld.Simulation.Cities.Streets.GroundSurface.None;
                    if (paved && runStart < 0) runStart = x;
                    else if (!paved && runStart >= 0) { zones.Add(new ClearZone(runStart, y, x - runStart, 1)); runStart = -1; }
                }
            }
        return zones;
    }

    /// <summary>#258: as zonas sem árvore agora, para o cliente não desenhar árvore onde o motor não a aceita.</summary>
    public static IReadOnlyList<ClearZone> Clear(WorldState world) => Known(world).Clear;
}
