using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Objects;

/// <summary>#258: a casa que a pessoa mantém sozinha, só por usos do catálogo (nada escrito por objeto): a luz que se liga ao escurecer e se desliga ao ir
/// dormir ou sair, e a refeição que se prepara antes de comer. O NPC que ninguém conduz usa os mesmos objetos que o jogador (<see cref="ObjectUsage"/>);
/// o que cada objeto faz (<see cref="ObjectUseDef.Toggle"/>, <see cref="ObjectUseDef.Produces"/>) é dado do cenário.</summary>
public static class HomeTending
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ObjectCatalog, object> Applicable = new();

    /// <summary>O catálogo do mundo tem algum uso que liga a luz ou produz algo (senão a pessoa não tem o que manter e a decisão nem olha a casa). Em cache por catálogo.</summary>
    public static bool Applies(WorldState world) =>
        world.ObjectCatalog is { } catalog
        && (bool)Applicable.GetValue(catalog, static c => (object)c.Objects.Any(o => o.Uses?.Any(u => u.Toggle is not null || u.Produces is not null) == true));

    /// <summary>Depois do pôr e antes do nascer do sol do dia (a luz do tempo do motor); sem o tempo, nunca. É a mesma conta do cliente (<c>IsNight</c>).</summary>
    public static bool IsDark(WorldState world)
    {
        if (world.Weather is not { Day: >= 0 } ledger) return false;
        double hour = world.CurrentDate.TotalHours % world.Calendar.HoursPerDay * 24.0 / world.Calendar.HoursPerDay;
        double half = ledger.DaylightMinutes / 120.0;
        return hour < 12 - half || hour >= 12 + half;
    }

    /// <summary>O uso do objeto que liga (<paramref name="on"/>) ou desliga a luz dele; nulo se o objeto não tem esse uso.</summary>
    public static ObjectUseDef? LightUse(ObjectDescriptor descriptor, bool on) =>
        descriptor.Uses?.FirstOrDefault(u => u.Toggle == (on ? ObjectToggles.LightOn : ObjectToggles.LightOff));

    /// <summary>Quem ninguém conduz, em casa e com o corpo livre, mantém a casa: apaga as luzes ao ir dormir ou sair, acende a do cômodo em que está ao
    /// escurecer e, na hora da refeição sem urgência, prepara a refeição quando a despensa não a tem. Verdadeiro se começou um uso. Quem tem algo urgente não passa por aqui (a decisão não chama).</summary>
    public static bool TryTend(WorldState world, TickContext ctx, Npc npc, ActionType candidate, bool leaving)
    {
        if (npc.ControllerId is not null || world.ObjectCatalog is not { } catalog || npc.Household is not { } hid || world.FindHousehold(hid) is not { } household) return false;
        if (HomeLife.HomeOf(world, npc) is not { } home || npc.CurrentLocation != household.Location) return false;
        bool lights = world.Lights is not null || (IsDark(world) && candidate != ActionType.Sleep && !leaving);
        bool cooks = candidate == ActionType.Eat;
        if (!lights && !cooks) return false;
        ObjectUsage.SettleDue(world, ctx, npc);
        if (PlayerTasks.Of(world, npc) is not null) return false;
        var pieces = HomeFurniture.Of(home, catalog);
        bool dark = IsDark(world);
        bool off = candidate == ActionType.Sleep || leaving || !dark; // de dia, ou indo dormir ou sair, apaga
        bool wantOn = dark && !off;
        string? here = null;
        if (wantOn && npc.Interior is { } inside && inside.Building == home.Id && BuildingRooms.Of(home, catalog).RoomAt(inside.Floor.Value, inside.LocalCell) is { } room) here = room.Kind;
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i].Spot is not { } spot || catalog.Find(pieces[i].Id) is not { Uses: { } } descriptor) continue;
            bool lit = world.LightIsOn(home.Id.Value, i);
            if (lights && off && lit && LightUse(descriptor, false) is { } turnOff && ObjectUsage.Begin(world, ctx, npc, home.Id.Value, i, turnOff.Id).IsSuccess) return true;
            if (lights && wantOn && !lit && LightUse(descriptor, true) is { } turnOn && (here is null || HomeFurniture.RoomOf(home, catalog, spot) == here)
                && ObjectUsage.Begin(world, ctx, npc, home.Id.Value, i, turnOn.Id).IsSuccess) return true;
            if (cooks && descriptor.Uses!.FirstOrDefault(u => u.Produces is { Count: > 0 } && IsMeal(catalog, u.Produces[0].Resource)
                    && household.Stock.GetValueOrDefault(new LivingWorld.Domain.Geography.ResourceType(u.Produces[0].Resource)) < 1) is { } cook
                && ObjectUsage.Begin(world, ctx, npc, home.Id.Value, i, cook.Id).IsSuccess) return true;
        }

        return false;
    }

    /// <summary>O recurso do catálogo que sacia a fome (descritor de recurso com efeito <c>hunger</c>): a refeição pronta, por oposição à comida crua.</summary>
    public static bool IsMeal(ObjectCatalog catalog, int resourceId) =>
        catalog.ForResource(resourceId) is { Effects: { } effects } && effects.Any(e => e.Target == ObjectEffectTargets.Hunger && e.Magnitude > 0);
}
