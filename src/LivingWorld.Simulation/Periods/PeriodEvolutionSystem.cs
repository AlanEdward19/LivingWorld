using LivingWorld.Domain;

namespace LivingWorld.Simulation.Periods;

/// <summary>Executa as <see cref="PeriodTransformationRule"/> declaradas de um período em
/// runtime (Fase 13, T13 — Goal #2 da fase: "profissões podem surgir, mudar e desaparecer em
/// runtime"). Muta <see cref="PopulationCatalog.ProfessionIds"/> in-place (mesmo <c>HashSet</c>
/// vive a vida toda do mundo — <see cref="WorldState.PopulationCatalog"/> nunca é reatribuído) e
/// reatribui NPCs cuja profissão deixa de existir, sem precisar de estado novo em
/// <see cref="WorldState"/>: cada regra é guardada pela própria pertença ao catálogo — uma vez
/// aplicada, a pré-condição vira falsa e a regra nunca reaplica.</summary>
public sealed class PeriodEvolutionSystem(IReadOnlyList<PeriodTransformationRule> rules) : ISimulationSystem
{
    public const string SystemName = "period-evolution";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        foreach (var rule in rules)
        {
            if (rule.TriggerTick is { } trigger && ctx.CurrentTick < trigger) continue;
            Apply(world, ctx, rule);
        }
    }

    private static void Apply(WorldState world, TickContext ctx, PeriodTransformationRule rule)
    {
        var professionIds = world.PopulationCatalog.ProfessionIds;

        switch (rule.Kind)
        {
            case PeriodTransformationKind.Emerge:
            {
                int target = rule.TargetProfessionIds[0];
                professionIds.Add(target); // idempotente: HashSet.Add é no-op se já presente
                break;
            }
            case PeriodTransformationKind.Disappear:
            {
                int source = rule.SourceProfessionIds[0];
                if (!professionIds.Remove(source)) return; // já aplicada (ou nunca esteve presente)
                ReassignHolders(world, source, ProfessionType.None);
                break;
            }
            case PeriodTransformationKind.Merge:
            {
                if (!rule.SourceProfessionIds.All(professionIds.Contains)) return; // já aplicada
                int target = rule.TargetProfessionIds[0];
                foreach (int source in rule.SourceProfessionIds)
                {
                    professionIds.Remove(source);
                    ReassignHolders(world, source, new ProfessionType(target));
                }
                professionIds.Add(target);
                break;
            }
            case PeriodTransformationKind.Split:
            {
                int source = rule.SourceProfessionIds[0];
                if (!professionIds.Remove(source)) return; // já aplicada
                foreach (int target in rule.TargetProfessionIds)
                    professionIds.Add(target);
                ReassignHoldersAmongTargets(world, ctx, source, rule.TargetProfessionIds);
                break;
            }
        }
    }

    private static void ReassignHolders(WorldState world, int sourceProfessionId, ProfessionType target)
    {
        foreach (var npc in world.Npcs)
            if (npc.IsAlive && npc.Profession.Id == sourceProfessionId)
                npc.SwitchProfession(target);
    }

    /// <summary>Split não tem um único alvo — cada portador da profissão de origem é
    /// redistribuído uniformemente entre os alvos declarados, sorteio determinístico por NPC
    /// (mesmo padrão de <c>MaterializationSystem</c> — stream de RNG derivado do id do NPC).</summary>
    private static void ReassignHoldersAmongTargets(
        WorldState world, TickContext ctx, int sourceProfessionId, IReadOnlyList<int> targetProfessionIds)
    {
        var sortedTargets = targetProfessionIds.OrderBy(id => id).ToList();
        foreach (var npc in world.Npcs)
        {
            if (!npc.IsAlive || npc.Profession.Id != sourceProfessionId) continue;
            var rng = ctx.Rng($"period-evolution-split-{npc.Id.Value}");
            int index = Math.Min((int)(rng.NextDouble() * sortedTargets.Count), sortedTargets.Count - 1);
            npc.SwitchProfession(new ProfessionType(sortedTargets[index]));
        }
    }
}
