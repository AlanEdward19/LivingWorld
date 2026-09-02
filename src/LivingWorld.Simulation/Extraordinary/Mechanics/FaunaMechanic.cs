using LivingWorld.Domain.Fauna;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>
/// <c>fauna.dominate:&lt;raio|animal-id&gt;</c> segue o portador enquanto manifestado;
/// <c>fauna.infect-vector:&lt;doença&gt;</c> marca animais no raio de contato.
/// </summary>
public sealed class FaunaMechanic : ExtraordinaryMechanic
{
    public const string DominatePrefix = "fauna.dominate:";
    public const string InfectPrefix = "fauna.infect-vector:";
    public const int ContactRadius = 1;

    public override string Prefix => "fauna.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        if (declaration.StartsWith(DominatePrefix, StringComparison.Ordinal))
        {
            if (!TryParseDominate(declaration, out _, out _))
                return Result<PreparedMutation?>.Fail(
                    "Effects: fauna.dominate exige raio >= 0 ou id 'animal-N'");
            return Result<PreparedMutation?>.Ok(null);
        }

        if (!declaration.StartsWith(InfectPrefix, StringComparison.Ordinal))
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");

        if (!TryParseInfect(declaration, out _))
            return Result<PreparedMutation?>.Fail("Effects: fauna.infect-vector exige uma doença");
        return Result<PreparedMutation?>.Ok(null);
    }

    internal static bool TryParseInfect(string declaration, out string disease)
    {
        disease = "";
        if (!declaration.StartsWith(InfectPrefix, StringComparison.Ordinal)) return false;
        disease = declaration[InfectPrefix.Length..];
        return !string.IsNullOrWhiteSpace(disease);
    }

    internal static bool TryParseDominate(string declaration, out int? radius, out AnimalId? animalId)
    {
        radius = null;
        animalId = null;
        if (!declaration.StartsWith(DominatePrefix, StringComparison.Ordinal)) return false;
        string argument = declaration[DominatePrefix.Length..];
        const string idPrefix = "animal-";
        if (argument.StartsWith(idPrefix, StringComparison.Ordinal)
            && long.TryParse(argument[idPrefix.Length..], out long id)
            && argument[idPrefix.Length..].Equals(id.ToString(), StringComparison.Ordinal))
        {
            animalId = new AnimalId(id);
            return true;
        }

        if (int.TryParse(argument, out int parsedRadius) && parsedRadius >= 0
            && argument.Equals(parsedRadius.ToString(), StringComparison.Ordinal))
        {
            radius = parsedRadius;
            return true;
        }

        return false;
    }

    internal static int Chebyshev(CellCoord a, CellCoord b) =>
        Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}

/// <summary>Move animais dominados um passo em direção ao portador a cada tick horário.</summary>
public sealed class FaunaDominateSystem : ISimulationSystem
{
    public const string SystemName = "ExtraordinaryFauna";
    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.Extraordinary.Enabled) return;

        foreach (var carrierState in world.ExtraordinaryCarriers.OrderBy(item => item.CarrierId.Value))
        {
            if (carrierState is not { IsManifested: true }) continue;
            if (world.FindNpc(carrierState.CarrierId) is not { IsAlive: true } carrier) continue;

            foreach (var descriptor in world.Extraordinary.Descriptors
                         .Where(item => carrierState.PowerIds.Contains(item.Id, StringComparer.Ordinal))
                         .OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                if (!ExtraordinaryManifestationCondition.IsMet(descriptor.ManifestationCondition, world, carrier))
                    continue;
                foreach (var effect in descriptor.Effects)
                {
                    ApplyDominate(world, carrier, effect);
                    ApplyInfect(world, carrier, effect);
                }
            }
        }
    }

    private static void ApplyDominate(WorldState world, Npc carrier, string effect)
    {
        if (!FaunaMechanic.TryParseDominate(effect, out int? radius, out AnimalId? animalId)) return;

        foreach (var animal in world.Fauna.Where(item => item.IsAlive).OrderBy(item => item.Id.Value).ToList())
        {
            bool targeted = animalId is { } id
                ? animal.Id == id
                : FaunaMechanic.Chebyshev(animal.Position, carrier.CurrentLocation) <= radius!.Value;
            if (!targeted) continue;

            var next = StepToward(animal.Position, carrier.CurrentLocation);
            if (next == animal.Position || !world.Map.TryGetCell(next, out _)) continue;
            world.ReplaceAnimal(animal with { Position = next });
        }
    }

    private static void ApplyInfect(WorldState world, Npc carrier, string effect)
    {
        if (!FaunaMechanic.TryParseInfect(effect, out string disease)) return;

        foreach (var animal in world.Fauna.Where(item => item.IsAlive).OrderBy(item => item.Id.Value).ToList())
        {
            if (FaunaMechanic.Chebyshev(animal.Position, carrier.CurrentLocation) > FaunaMechanic.ContactRadius)
                continue;
            world.ReplaceAnimal(animal with { VectorDisease = disease });
        }
    }

    private static CellCoord StepToward(CellCoord from, CellCoord to)
    {
        if (from == to) return from;
        return new CellCoord(from.X + Math.Sign(to.X - from.X), from.Y + Math.Sign(to.Y - from.Y));
    }
}
