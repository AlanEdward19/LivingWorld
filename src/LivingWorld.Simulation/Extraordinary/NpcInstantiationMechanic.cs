using LivingWorld.Domain;
using LivingWorld.Simulation.Behavior;

namespace LivingWorld.Simulation;

/// <summary>
/// Instanciação de NPC via poder (PWR-104..107). Prefixos longos para não colidir com
/// <see cref="NpcStatMechanic"/> (<c>npc.</c>).
/// </summary>
public static class NpcInstantiationMechanic
{
    public static void OnCarrierDeath(WorldState world, TickContext ctx, Npc npc)
    {
        if (!npc.IsAlive)
            return;

        int split = DeclaredMagnitudeSum(world, npc, "npc.split-on-death:");
        for (int i = 0; i < split; i++)
            InstantiateCopy(world, ctx, npc, "split-on-death");

        int reincarnate = DeclaredMagnitudeSum(world, npc, "npc.reincarnate:");
        if (reincarnate <= 0)
            return;

        var payload = new PendingReincarnationPayload(
            new Dictionary<int, double>(npc.Skills.Values),
            CopyPersonality(npc.Personality),
            reincarnate,
            ctx.CurrentTick);
        var carrier = world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == npc.Id);
        if (carrier is null)
        {
            world.UpsertExtraordinaryCarrier(new ExtraordinaryCarrierState(
                npc.Id, [], false, "dormant",
                new ExtraordinaryAppearanceState(1, "", ""),
                null, 1, PendingReincarnation: payload));
            return;
        }

        world.UpsertExtraordinaryCarrier(carrier with { PendingReincarnation = payload });
    }

    public static void ApplyPendingReincarnation(WorldState world, TickContext ctx, Npc baby)
    {
        var carrier = world.ExtraordinaryCarriers
            .Where(item => item.PendingReincarnation is not null)
            .OrderBy(item => item.PendingReincarnation!.QueuedTick)
            .ThenBy(item => item.CarrierId.Value)
            .FirstOrDefault();
        if (carrier?.PendingReincarnation is not { } pending)
            return;

        baby.RewritePersonality(BlendPersonality(pending.Personality, baby.Personality, pending.FractionPercent));
        foreach (var (skillId, value) in pending.Skills.OrderBy(pair => pair.Key))
            baby.GainSkill(new SkillType(skillId), value * pending.FractionPercent / 100.0, 100);

        world.UpsertExtraordinaryCarrier(carrier with { PendingReincarnation = null });
        ctx.LogEvent(
            WorldEventKind.NpcInstantiated,
            $"{carrier.CarrierId.Value}|{baby.Id.Value}|reincarnate", sourceSystem: "NpcInstantiationMechanic");
    }

    public static Npc InstantiateCopy(WorldState world, TickContext ctx, Npc source, string origin)
    {
        var id = AllocateNpcId(world);
        var personality = CopyPersonality(source.Personality);
        var skills = new SkillSet(new Dictionary<int, double>(source.Skills.Values));
        var clone = new Npc(
            id, $"{source.Name}-{origin}-{id.Value}", source.Sex, source.BirthDate, source.Culture,
            source.BirthLocation, motherId: null, fatherId: null, household: null, health: source.Health,
            personality, source.Profession, source.CurrentLocation,
            skills: skills, rateGene: source.RateGene, vitality: source.Vitality, upbringing: source.Upbringing,
            city: source.City);
        world.AddNpc(clone);
        clone.ConfigureNeedDecay(world.NeedsRules, world.CurrentDate.TotalHours);
        MortalitySystem.SchedulePlannedDeath(world, ctx, clone);
        NpcWakeScheduler.ScheduleWake(world, ctx, clone.Id.Value, world.CurrentDate.TotalHours + 1);

        var sourceState = world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == source.Id);
        if (sourceState is not null)
        {
            var appearance = new ExtraordinaryAppearanceState(
                sourceState.Appearance.ScaleMultiplier,
                sourceState.Appearance.SkinTint,
                sourceState.Appearance.MovementTrail);
            world.UpsertExtraordinaryCarrier(new ExtraordinaryCarrierState(
                clone.Id, [], false, "dormant", appearance, null, 1));
        }

        ctx.LogEvent(WorldEventKind.NpcInstantiated, $"{source.Id.Value}|{clone.Id.Value}|{origin}", sourceSystem: "NpcInstantiationMechanic");
        return clone;
    }

    public static Personality BlendPersonality(Personality donor, Personality baseline, int percent)
    {
        int Mix(int from, int to) => Math.Clamp(
            (int)Math.Round(to + (from - to) * (percent / 100d), MidpointRounding.AwayFromZero),
            0, 100);
        return Personality.Create(
            Mix(donor.Extroversion, baseline.Extroversion),
            Mix(donor.Agreeableness, baseline.Agreeableness),
            Mix(donor.Conscientiousness, baseline.Conscientiousness),
            Mix(donor.EmotionalStability, baseline.EmotionalStability),
            Mix(donor.Openness, baseline.Openness),
            Mix(donor.Ambition, baseline.Ambition),
            Mix(donor.Loyalty, baseline.Loyalty),
            Mix(donor.Altruism, baseline.Altruism),
            Mix(donor.Impulsivity, baseline.Impulsivity),
            Mix(donor.RiskAversion, baseline.RiskAversion)).Value!;
    }

    private static NpcId AllocateNpcId(WorldState world)
    {
        NpcId id;
        do
        {
            id = world.NextNpcIdAndAdvance();
        } while (world.FindNpc(id) is not null);
        return id;
    }

    private static int DeclaredMagnitudeSum(WorldState world, Npc npc, string tokenPrefix)
    {
        var carrier = world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == npc.Id);
        if (carrier is null)
            return 0;

        int total = 0;
        foreach (var effect in world.Extraordinary.Descriptors
                     .Where(descriptor => carrier.PowerIds.Contains(descriptor.Id, StringComparer.Ordinal))
                     .OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal)
                     .SelectMany(descriptor => descriptor.Effects))
        {
            if (!effect.StartsWith(tokenPrefix, StringComparison.Ordinal))
                continue;
            var parsed = ExtraordinaryMechanicSupport.ParseAmount(effect, "Effects", allowSigned: false);
            if (parsed.IsSuccess)
                total += parsed.Value.Amount;
        }

        return total;
    }

    private static Personality CopyPersonality(Personality source) => Personality.Create(
        source.Extroversion, source.Agreeableness, source.Conscientiousness, source.EmotionalStability,
        source.Openness, source.Ambition, source.Loyalty, source.Altruism, source.Impulsivity,
        source.RiskAversion).Value!;
}
