using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Objects;

/// <summary>Conforto do lar do NPC: magnitude do efeito do objeto da moradia que serve dormir e comer (#47 fatia 3, ADR-0038).
/// Entra no <see cref="Behavior.Decision.DecisionContext"/> como oportunidade; zero = sem objeto (ou sem catálogo).</summary>
public readonly record struct ObjectComfort(float Sleep, float Eat);

/// <summary>Uso de objetos pelo motor: qual objeto da mobília do prédio atende a ação de um NPC, quem pode usá-lo e o efeito
/// que isso tem. Determinístico e sem estado: lê o catálogo do mundo, o domicílio e a moradia do NPC. Sem catálogo, tudo é neutro.</summary>
public static class ObjectUse
{
    /// <summary>Piso do bônus somado: o loader já recusa soma ≤ -1, e este piso garante que um catálogo vindo de snapshot nunca zere o divisor da duração.</summary>
    private const double MinimumBonus = -0.99;

    /// <summary>Primeiro objeto, na ordem da mobília do tipo de prédio, com ponto de uso que atende à ação e que o NPC pode usar.</summary>
    public static ObjectDescriptor? Serving(ObjectCatalog catalog, int buildingTypeId, ActionType action, Npc? npc)
    {
        // for e não foreach: enumerar IReadOnlyList pela interface aloca um enumerador por chamada (caminho quente do tick)
        var furnishing = catalog.FurnishingFor(buildingTypeId);
        for (int i = 0; i < furnishing.Count; i++)
        {
            var descriptor = catalog.Find(furnishing[i]);
            if (descriptor is null) continue;
            var points = descriptor.UsePoints;
            for (int p = 0; p < points.Count; p++)
                if (points[p].Action == (int)action && Allows(descriptor, action, npc))
                    return descriptor;
        }

        return null;
    }

    /// <summary>Sem <c>Interactions</c> o objeto serve a quem tem ponto de uso; com elas, só às ações listadas e a quem cumpre os requisitos.</summary>
    public static bool Allows(ObjectDescriptor descriptor, ActionType action, Npc? npc)
    {
        if (descriptor.Interactions is not { Count: > 0 } interactions) return true;
        for (int i = 0; i < interactions.Count; i++)
        {
            var interaction = interactions[i];
            if (interaction.Action != (int)action) continue;
            if (npc is null) return true;
            if (interaction.Profession is int profession && npc.Profession.Id != profession) return false;
            if (interaction.SkillId is int skill && npc.Skills.Get(new SkillType(skill)) < (interaction.SkillMin ?? 0)) return false;
            return true;
        }

        return false;
    }

    /// <summary>Magnitude somada dos efeitos do objeto da moradia do NPC que atende à ação (0 sem objeto). Com
    /// <paramref name="mustBeHome"/> o NPC precisa estar na célula de sono do domicílio (vale para a duração); sem ele é a expectativa de conforto.</summary>
    public static double Bonus(WorldState world, Npc npc, ActionType action, bool mustBeHome)
    {
        if (world.ObjectCatalog is not { } catalog) return 0.0;
        string? target = action switch { ActionType.Sleep => ObjectEffectTargets.Sleep, ActionType.Eat => ObjectEffectTargets.Eat, _ => null };
        if (target is null) return 0.0;
        if (npc.Household is not { } householdId || world.FindHousehold(householdId) is not { Dwelling: { } dwelling } household) return 0.0;
        if (mustBeHome && npc.CurrentLocation != household.Location) return 0.0;
        if (world.FindBuilding(dwelling) is not { } building) return 0.0;
        if (Serving(catalog, building.BuildingTypeId, action, npc) is not { Effects: { } effects }) return 0.0;

        double sum = 0.0;
        for (int i = 0; i < effects.Count; i++)
            if (effects[i].Target == target)
                sum += effects[i].Magnitude;
        return Math.Max(sum, MinimumBonus);
    }

    /// <summary>Duração da ação em horas: a do catálogo de ações, dividida por (1 + efeito do objeto) para dormir e comer em casa (mínimo 1 h).
    /// Agendador e conclusão usam esta mesma função, então concordam sobre quando a ação termina.</summary>
    public static int DurationHours(WorldState world, Npc npc, ActionType action, ActionCatalog catalog)
    {
        int baseHours = catalog.DurationOf(action);
        if (world.ObjectCatalog is null) return baseHours;
        double bonus = Bonus(world, npc, action, mustBeHome: true);
        return bonus == 0.0 ? baseHours : Math.Max(1, (int)Math.Ceiling(baseHours / (1.0 + bonus)));
    }

    public static ObjectComfort ComfortOf(WorldState world, Npc npc) =>
        world.ObjectCatalog is null
            ? default
            : new ObjectComfort((float)Bonus(world, npc, ActionType.Sleep, mustBeHome: false), (float)Bonus(world, npc, ActionType.Eat, mustBeHome: false));
}
