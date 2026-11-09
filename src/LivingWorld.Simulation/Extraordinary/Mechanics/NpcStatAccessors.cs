using LivingWorld.Domain.Population;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>
/// Uma necessidade/atributo do NPC acessível ao extraordinário: chave, leitura no tick,
/// escrita bruta no tick e faixa. Escrita bruta = o setter do <see cref="Npc"/>; quem
/// precisa de clamp o aplica via <see cref="Clamp"/> (cada mecânica preserva a sua regra).
/// </summary>
public sealed record NpcStat(
    string Key,
    Func<Npc, long, int> Read,
    Action<Npc, int, long> Write,
    int Min = 0,
    int Max = 100)
{
    public int Clamp(long value) => (int)Math.Clamp(value, Min, Max);

    /// <summary>Soma com clamp na faixa (efeitos <c>npc.*</c>, crédito de <c>transfer.*</c>).</summary>
    public void AddClamped(Npc npc, long delta, long tick) =>
        Write(npc, Clamp((long)Read(npc, tick) + delta), tick);

    /// <summary>Subtrai sem clamp próprio (custos <c>carrier.*</c>, débito de <c>transfer.*</c>).</summary>
    public void SubtractRaw(Npc npc, int amount, long tick) =>
        Write(npc, Read(npc, tick) - amount, tick);
}

/// <summary>
/// Tabela única de atributos do NPC para o extraordinário (issue #64). Atributo novo entra
/// aqui e vira efeito <c>npc.&lt;chave&gt;</c>, custo <c>carrier.&lt;chave&gt;</c>, transferência
/// <c>transfer.&lt;chave&gt;</c> e vínculo <c>bond.share:&lt;chave&gt;</c> sem editar mecânica.
/// Resolução por <see cref="ReadOnlySpan{T}"/>: sem alocação por acesso.
/// </summary>
public static class NpcStatAccessors
{
    /// <summary>Saúde: o único atributo que o motor trata como vida (modo de falha <c>carrier.health</c>).</summary>
    public static NpcStat Health { get; } =
        new("health", static (npc, _) => npc.Health, static (npc, value, _) => npc.SetHealth(value));

    private static readonly NpcStat[] Stats =
    [
        Health,
        new("hunger", static (npc, tick) => npc.HungerAt(tick), static (npc, value, tick) => npc.SetHunger(value, tick)),
        new("thirst", static (npc, tick) => npc.ThirstAt(tick), static (npc, value, tick) => npc.SetThirst(value, tick)),
        new("sleep", static (npc, tick) => npc.SleepAt(tick), static (npc, value, tick) => npc.SetSleep(value, tick)),
        new("social", static (npc, tick) => npc.SocialAt(tick), static (npc, value, tick) => npc.SetSocial(value, tick)),
    ];

    private static readonly Dictionary<string, NpcStat> ByKey =
        Stats.ToDictionary(stat => stat.Key, StringComparer.Ordinal);

    private static readonly Dictionary<string, NpcStat>.AlternateLookup<ReadOnlySpan<char>> BySpan =
        ByKey.GetAlternateLookup<ReadOnlySpan<char>>();

    public static IReadOnlyList<NpcStat> All => Stats;

    public static bool TryGet(ReadOnlySpan<char> key, out NpcStat stat) =>
        BySpan.TryGetValue(key, out stat!);

    /// <summary>Resolve <c>&lt;prefixo&gt;&lt;chave&gt;</c> (ex.: <c>carrier.health</c>) sem alocar.</summary>
    public static bool TryGet(string prefixedKey, string prefix, out NpcStat stat)
    {
        stat = null!;
        return prefixedKey.StartsWith(prefix, StringComparison.Ordinal)
            && BySpan.TryGetValue(prefixedKey.AsSpan(prefix.Length), out stat!);
    }
}
