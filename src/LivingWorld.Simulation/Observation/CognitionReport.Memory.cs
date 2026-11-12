using System.Globalization;
using LivingWorld.Domain.Cognition;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Observation;

// Parte de CognitionTally — lembranças guardadas pelos adultos vivos no fim da rodada.
public sealed partial class CognitionTally
{
    public static string MemorySummary(WorldState world)
    {
        var inv = CultureInfo.InvariantCulture;
        var byKind = new SortedDictionary<string, int>(StringComparer.Ordinal);
        int adults = 0, memories = 0, grieving = 0;
        long now = world.CurrentDate.TotalHours;
        foreach (var npc in world.AliveNpcIndex.Alive.OrderBy(n => n.Id.Value))
        {
            if (npc.AgeYears(world.CurrentDate) < 18) continue;
            adults++;
            if (npc.Psyche is not { } psyche) continue;
            memories += psyche.Memories.Count;
            foreach (var m in psyche.Memories) byKind[m.Kind.ToString()] = byKind.GetValueOrDefault(m.Kind.ToString()) + 1;
            if (PsycheMechanic.EmotionOf(world, npc, Emotion.Grief, now) > 0.1) grieving++;
        }

        // tamanho: o mesmo snapshot JSON com e sem a vida interior, por NPC vivo
        string json = WorldSnapshot.Serialize(world);
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        foreach (var n in node["Npcs"]!.AsArray()) n!.AsObject().Remove("Psyche");
        int alive = Math.Max(1, world.AliveNpcIndex.Alive.Count);
        int without = node.ToJsonString().Length;
        return string.Create(inv, $"adultos={adults} memorias_por_adulto={(double)memories / Math.Max(1, adults):F2} enlutados(>0.1)={grieving} snapshot_bytes_por_vivo={json.Length / alive} sem_psique={without / alive} por_tipo={string.Join(",", byKind.Select(kv => $"{kv.Key}:{kv.Value}"))}");
    }
}
