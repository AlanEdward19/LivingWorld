using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Narrative;

namespace LivingWorld.Simulation.Players;

/// <summary>Uma linha do diário: o instante (ms do mundo), de onde veio (<c>fact</c>: o que o mundo registrou sobre a pessoa;
/// <c>own</c>: o que o jogador fez com o corpo), o tipo, o peso, se é marco de vida e se aconteceu sob o controle do jogador.</summary>
public sealed record DiaryEntry(long AtMs, string Source, string Kind, double Significance, bool Milestone, bool WhileControlled,
    string? Verb = null, long? TargetId = null);

/// <summary>Diário do corpo (#235): reaproveita a linha do tempo da biografia (<see cref="NpcBiographyQuery"/>, os fatos do mundo em
/// que a pessoa participa) e acrescenta o que o jogador fez, tirado do registro de sessão. Nada novo é gravado no mundo; só lê.</summary>
public static class PlayerDiary
{
    public const int Max = 60;

    /// <summary>Marcos de vida: o que muda a vida da pessoa (nascer, casar, mudar de teto, de ofício ou de cidade, perder o laço).</summary>
    public static readonly IReadOnlySet<WorldEventKind> Milestones = new HashSet<WorldEventKind>
    {
        WorldEventKind.Birth, WorldEventKind.Marriage, WorldEventKind.Hired, WorldEventKind.Fired, WorldEventKind.VocationChosen,
        WorldEventKind.CharacterArrived, WorldEventKind.CharacterJoinedFamily, WorldEventKind.HouseSold, WorldEventKind.UnionEnded,
        WorldEventKind.PlayerIncarnated, WorldEventKind.Returned, WorldEventKind.Banished,
    };

    /// <summary>Verbos do registro de sessão que valem como "fiz isto" (andar e pular ficam de fora: seria ruído).</summary>
    private static readonly IReadOnlySet<PlayerCommandKind> Deeds = new HashSet<PlayerCommandKind>
    {
        PlayerCommandKind.Act, PlayerCommandKind.Interact, PlayerCommandKind.Strike, PlayerCommandKind.Persuade, PlayerCommandKind.Sleep,
    };

    /// <summary>As últimas <see cref="Max"/> linhas, da mais nova para a mais velha. <paramref name="controlledSinceMs"/>: quando o
    /// jogador assumiu o corpo (nulo: o corpo todo conta como controlado).</summary>
    public static IReadOnlyList<DiaryEntry> Of(WorldState world, Npc npc, string playerId, PlayerSessionLog? log, long? controlledSinceMs)
    {
        long since = controlledSinceMs ?? 0;
        var entries = new List<DiaryEntry>();
        var timeline = NpcBiographyQuery.Timeline(world, npc.Id);
        if (timeline.IsSuccess)
            foreach (var f in timeline.Value!)
            {
                long at = f.Tick * WorldMillis.PerHour;
                var other = f.Participants.Where(p => p != npc.Id).Select(p => (long?)p.Value).FirstOrDefault();
                entries.Add(new DiaryEntry(at, "fact", f.Kind.ToString(), Math.Round(f.Significance, 3), Milestones.Contains(f.Kind),
                    at >= since, TargetId: other));
            }
        foreach (var r in log?.Records ?? [])
            if (r.PlayerId == playerId && r.NpcId == npc.Id.Value && !r.Refused && r.AtMs >= since && Deeds.Contains(r.Kind))
                entries.Add(new DiaryEntry(r.AtMs, "own", r.Kind.ToString(), 0, false, true,
                    Verb: r.Action?.ToString() ?? r.Verb ?? r.Interaction, TargetId: r.TargetId));
        return entries.OrderByDescending(e => e.AtMs).ThenBy(e => e.Source, StringComparer.Ordinal).ThenBy(e => e.Kind, StringComparer.Ordinal)
            .Take(Max).ToList();
    }

    /// <summary>Quando o jogador assumiu <paramref name="npc"/> pela última vez, pelo registro de sessão.</summary>
    public static long? ControlledSince(PlayerSessionLog? log, string playerId, long npcId) =>
        log?.Records.LastOrDefault(r => r.PlayerId == playerId && r.NpcId == npcId && !r.Refused
            && r.Kind is PlayerCommandKind.Incarnate or PlayerCommandKind.Create or PlayerCommandKind.Character)?.AtMs;
}
