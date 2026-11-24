using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Population.Naming;

/// <summary>O que a cidade sabe das linhagens no tick (AD-183): por cidade e nome de família, o mais honrado (vivo ou morto,
/// enquanto lembrado) e o de pior fama (vivo), os dois primeiros de cada para excluir a própria pessoa. Montado na primeira
/// leitura do tick e reaproveitado até o fim dele: o feito do dia corre a cidade no dia seguinte. Derivado, fora do snapshot
/// e do hash; o mundo reaberto o remonta no mesmo ponto do tick.</summary>
internal sealed class LineageFame
{
    private readonly struct Top
    {
        public readonly double First, Second;
        public readonly long FirstNpc;

        public Top(double first, long firstNpc, double second) => (First, FirstNpc, Second) = (first, firstNpc, second);

        public Top With(double value, long npc) =>
            value > First || (value == First && npc < FirstNpc) ? new Top(value, npc, First)
            : value > Second ? new Top(First, FirstNpc, value) : this;

        public double Except(long npc) => FirstNpc == npc ? Second : First;
    }

    private readonly long _tick;
    private readonly Dictionary<Guid, Dictionary<string, Top>> _honor = new();
    private readonly Dictionary<Guid, Dictionary<string, Top>> _infamy = new();

    private LineageFame(long tick) => _tick = tick;

    public static LineageFame Of(WorldState world, long now)
    {
        if (world.LineageFameMemo is { } memo && memo._tick == now) return memo;
        var fame = new LineageFame(now);
        if (world.Honor is { } honor)
            foreach (var record in honor.Records)
                if (record.Family.Length > 0)
                    Add(fame._honor, record.City, record.Family, Honor.Decayed(world, record, now), record.Npc);
        if (world.CrimeOn)
            foreach (var record in world.Crime!.Records)
                if (world.FindNpc(new LivingWorld.Domain.Shared.NpcId(record.Npc)) is { IsAlive: true } npc
                    && Honor.FamilyOf(npc.Name) is { Length: > 0 } family)
                    Add(fame._infamy, npc.City.Value, family,
                        LivingWorld.Simulation.Crime.Justice.InfamyOf(world, record.Npc, now).All, record.Npc);
        world.LineageFameMemo = fame;
        return fame;
    }

    private static void Add(Dictionary<Guid, Dictionary<string, Top>> into, Guid city, string family, double value, long npc)
    {
        if (value <= 0) return;
        if (!into.TryGetValue(city, out var byFamily)) into[city] = byFamily = new Dictionary<string, Top>(StringComparer.Ordinal);
        byFamily[family] = byFamily.TryGetValue(family, out var top) ? top.With(value, npc) : new Top(value, npc, 0);
    }

    public double BestHonorExcept(Npc npc) => Lookup(_honor, npc);

    public double WorstInfamyExcept(Npc npc) => Lookup(_infamy, npc);

    private static double Lookup(Dictionary<Guid, Dictionary<string, Top>> from, Npc npc)
    {
        if (from.Count == 0 || !from.TryGetValue(npc.City.Value, out var byFamily)) return 0;
        int cut = npc.Name.LastIndexOf(' ');
        if (cut <= 0) return 0;
        var lookup = byFamily.GetAlternateLookup<ReadOnlySpan<char>>();
        return lookup.TryGetValue(npc.Name.AsSpan(cut + 1), out var top) ? top.Except(npc.Id.Value) : 0;
    }
}
