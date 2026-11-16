using System.Globalization;
using System.Text;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Observation;

/// <summary>Sonda das decisões de abrir (AD-159): por década, quantos NPCs passaram pela conta do negócio com margem positiva, quantos
/// decidiram, quantos tinham o capital no casal, quantos procuraram sócio, quantos pediram crédito e por que o crédito foi negado (o
/// credor que chegou mais perto). Só observa: fica fora do snapshot e do hash (<see cref="WorldState.VentureProbe"/> é nulo fora dos
/// relatórios) e os sistemas mensais só a tocam com <c>?.</c>; não muda nada no mundo.</summary>
public sealed class VentureProbe(WorldState world)
{
    public const int Decades = 10;

    /// <summary>Por que o crédito foi negado, em ordem de quão perto se chegou: ninguém para pedir; má fama ou calote; o credor não tem
    /// moeda acima da reserva; tem, mas o medo do risco não deixa pôr tanto; não confia o bastante; a parcela não cabe no ganho; o
    /// fundador já pedia mais que a parte que se financia (o teto do empréstimo).</summary>
    public static readonly string[] Refusals = ["sem_credor", "fama", "sem_moeda", "aversao", "sem_confianca", "parcela", "teto"];

    public static readonly string[] Steps =
        ["decidiram", "capital_no_casal", "procuraram_socio", "tiveram_socio", "pediram_credito", "credito_concedido", "abriram", "sem_capital", "sem_lote"];

    private readonly HashSet<long>[] _considered = Enumerable.Range(0, Decades).Select(_ => new HashSet<long>()).ToArray();
    private readonly HashSet<long>[] _decided = Enumerable.Range(0, Decades).Select(_ => new HashSet<long>()).ToArray();
    public int[,] StepCount { get; } = new int[Decades, Steps.Length];
    public int[,] RefusalCount { get; } = new int[Decades, Refusals.Length];

    private bool _open;
    private readonly bool[] _flags = new bool[Steps.Length];
    private int _refusal = -1;

    private int Decade => Math.Clamp((int)(world.CurrentDate.TotalHours / world.Calendar.HoursPerYear / 10), 0, Decades - 1);

    public int Considered(int decade) => _considered[decade].Count;
    public int Decided(int decade) => _decided[decade].Count;

    /// <summary>O NPC passou pela conta do negócio com margem positiva (sabe o ofício, tem a idade, não tem oficina trabalhando).</summary>
    public void Consider(long npc) => _considered[Decade].Add(npc);

    /// <summary>O sorteio do mês saiu: ele vai tentar abrir. Abre a tentativa; <see cref="End"/> a fecha.</summary>
    public void Begin(long npc)
    {
        _decided[Decade].Add(npc);
        Array.Clear(_flags);
        _refusal = -1;
        _open = true;
        _flags[0] = true;
    }

    public void Mark(string step)
    {
        if (!_open) return;
        int i = Array.IndexOf(Steps, step);
        if (i >= 0) _flags[i] = true;
    }

    /// <summary>O crédito foi negado; guarda o motivo do credor que chegou mais perto entre as tentativas.</summary>
    public void Refuse(int reason)
    {
        if (_open && reason > _refusal) _refusal = reason;
    }

    public void End(bool opened)
    {
        if (!_open) return;
        if (opened) _flags[Array.IndexOf(Steps, "abriram")] = true;
        else if (!_flags[Array.IndexOf(Steps, "sem_lote")]) _flags[Array.IndexOf(Steps, "sem_capital")] = true;
        int d = Decade;
        for (int i = 0; i < Steps.Length; i++)
            if (_flags[i]) StepCount[d, i]++;
        if (_refusal >= 0 && !_flags[Array.IndexOf(Steps, "credito_concedido")]) RefusalCount[d, _refusal]++;
        _open = false;
    }

    public static string Summarize(IReadOnlyList<VentureProbe> runs)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append("  sonda de abertura (soma das seeds) por decada: quiseram(distintos)/decidiram(distintos)");
        sb.AppendLine(string.Concat(Enumerable.Range(0, Decades).Select(d => $" {d * 10}:{runs.Sum(r => r.Considered(d))}/{runs.Sum(r => r.Decided(d))}")));
        sb.Append("    tentativas:");
        for (int s = 0; s < Steps.Length; s++)
            sb.Append(inv, $" {Steps[s]}={runs.Sum(r => Enumerable.Range(0, Decades).Sum(d => r.StepCount[d, s]))}");
        sb.AppendLine();
        sb.Append("    credito negado por:");
        for (int k = 0; k < Refusals.Length; k++)
            sb.Append(inv, $" {Refusals[k]}={runs.Sum(r => Enumerable.Range(0, Decades).Sum(d => r.RefusalCount[d, k]))}");
        sb.AppendLine();
        sb.Append("    por decada (decidiram/capital_no_casal/socio/pediram/concedido/abriram):");
        int[] cols = [0, 1, 3, 4, 5, 6];
        for (int d = 0; d < Decades; d++)
            sb.Append(inv, $" {d * 10}:{string.Join('/', cols.Select(c => runs.Sum(r => r.StepCount[d, c])))}");
        return sb.AppendLine().ToString();
    }
}
