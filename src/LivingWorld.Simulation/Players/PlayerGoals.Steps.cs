using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Players;

/// <summary>O próximo passo de cada meta: a oferta que já está ao alcance (interagir), senão o lugar real mais perto onde ela estará
/// (andar até lá), senão a ação que o corpo pede (<c>act</c>). Tudo vem do estado do mundo; nada é escolhido ao acaso.</summary>
public static partial class PlayerGoals
{
    /// <summary>O corpo e o que o cerca, calculado uma vez por consulta (as ofertas só quando um passo as pede).</summary>
    private sealed class Scene
    {
        public Scene(WorldState world, Npc npc)
        {
            World = world;
            Npc = npc;
            Pose = PlayerGround.PoseOf(world, npc);
            Household = npc.Household is { } h ? world.FindHousehold(h) : null;
            Workplace = npc.Employer is { } e ? world.FindWorkplace(e) : null;
        }

        public WorldState World { get; }
        public Npc Npc { get; }
        public CellCoord Pose { get; }
        public Household? Household { get; }
        public Workplace? Workplace { get; }
        private IReadOnlyList<InteractionOffer>? _offers;
        public IReadOnlyList<InteractionOffer> Offers => _offers ??= PlayerInteractions.Offers(World, Npc);
    }

    private static int Distance(CellCoord a, CellCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private static GoalStep Go(Scene c, string label, CellCoord to) =>
        new("go", "", 0, "", label, to.X, to.Y, Distance(c.Pose, to), Here: false);

    private static GoalStep Act(Scene c, ActionType action, string label) =>
        new("act", "", 0, action.ToString(), label, c.Pose.X, c.Pose.Y, 0, Here: true);

    private static GoalStep Use(Scene c, InteractionOffer offer, string verb, string label, int? resourceId = null) =>
        new("interact", offer.Kind, offer.Id, verb, label, offer.X, offer.Y, Distance(c.Pose, new CellCoord(offer.X, offer.Y)), Here: true, resourceId);

    private static InteractionOffer? Offered(Scene c, string kind, string verb, Func<InteractionOffer, bool>? where = null) =>
        c.Offers.FirstOrDefault(o => o.Kind == kind && o.Verbs.Contains(verb) && (where?.Invoke(o) ?? true));

    private static GoalStep? FoodStep(Scene c)
    {
        var (world, npc) = (c.World, c.Npc);
        int food = world.EconomyRules.FoodResourceId;
        if (npc.IsCarrying && npc.CarriedResourceId == food) return Act(c, ActionType.Eat, "Comer o que você carrega");
        if (Offered(c, "stock", "take", o => o.Resources.Any(r => r.ResourceId == food && r.Quantity > 0)) is { } pantry)
            return Use(c, pantry, "take", "Pegar comida na despensa", food);
        long wallet = npc.Wallet.Amount;
        bool CanBuy(InteractionOffer o) => o.Resources.Any(r => r.ResourceId == food && r.Quantity > 0 && r.Price <= wallet);
        if (Offered(c, "stall", "buy", CanBuy) is { } stall) return Use(c, stall, "buy", "Comprar comida na banca", food);

        if (c.Household is { } home && home.Stock.GetValueOrDefault(new ResourceType(food)) > 0)
            return Go(c, "Despensa de casa", home.Location);
        var seller = world.Workplaces.Where(p => p.City == npc.City && !p.Closed && p.Prices.GetValueOrDefault(new ResourceType(food)) is > 0 and var price
                && price <= wallet && p.Stock.GetValueOrDefault(new ResourceType(food)) > 0)
            .OrderBy(p => Distance(c.Pose, p.Location)).ThenBy(p => p.Id.Value).FirstOrDefault();
        if (seller is not null) return Go(c, "Banca com comida", seller.Location);
        return c.Household is { } h ? Go(c, "Casa", h.Location) : Act(c, ActionType.Eat, "Comer");
    }

    /// <summary>Radius (células) em que a consulta procura água ao redor do corpo.</summary>
    private const int WaterRadius = 20;

    private static GoalStep? WaterStep(Scene c)
    {
        if (Offered(c, "water", "drink") is { } spring) return Use(c, spring, "drink", "Beber na fonte");
        CellCoord? best = null;
        int bestDistance = int.MaxValue;
        for (int dy = -WaterRadius; dy <= WaterRadius; dy++)
            for (int dx = -WaterRadius; dx <= WaterRadius; dx++)
            {
                var cell = new CellCoord(c.Pose.X + dx, c.Pose.Y + dy);
                int d = Math.Max(Math.Abs(dx), Math.Abs(dy));
                if (d >= bestDistance || !c.World.Map.TryGetCell(cell, out var water) || !water.HasWater) continue;
                (best, bestDistance) = (cell, d);
            }
        if (best is { } found) return Go(c, "Fonte de água", found);
        return c.Household is { } home ? Go(c, "Casa", home.Location) : Act(c, ActionType.Eat, "Beber e comer");
    }

    private static GoalStep? SleepStep(Scene c)
    {
        if (Offered(c, "bed", "sleep") is { } bed) return Use(c, bed, "sleep", "Dormir na sua cama");
        if (c.Household is { IsHomeless: false } home)
            return Go(c, "Sua cama", c.World.BedOf(home.Id)?.Location ?? home.Location);
        return Act(c, ActionType.Sleep, "Dormir onde der");
    }

    /// <summary>Falar com <paramref name="who"/> (ou, sem alvo, com quem estiver mais perto, a casa primeiro): a conversa se pede a quem
    /// está ao alcance; fora dele, o passo é chegar perto.</summary>
    private static GoalStep? TalkStep(Scene c, Npc? who)
    {
        var (world, npc) = (c.World, c.Npc);
        if (Offered(c, "person", "talk", o => who is null || o.Id == who.Id.Value) is { } near)
            return Use(c, near, "talk", who is null ? $"Conversar com {near.Label}" : $"Ficar com {near.Label}");
        var target = who ?? FamilyOf(c).Concat(world.AliveNpcIndex.Alive.Where(o => o.City == npc.City && o.Id != npc.Id))
            .OrderBy(o => Distance(c.Pose, PlayerGround.PoseOf(world, o))).ThenBy(o => o.Id.Value).FirstOrDefault();
        return target is null ? null : Go(c, target.Name, PlayerGround.PoseOf(world, target));
    }

    private static GoalStep? WorkStep(Scene c)
    {
        var (world, npc) = (c.World, c.Npc);
        if (c.Workplace is not { } w)
        {
            var open = world.Workplaces.Where(p => p.City == npc.City && !p.Closed && p.Employees.Count < p.MaxVacancies)
                .OrderBy(p => Distance(c.Pose, p.Location)).ThenBy(p => p.Id.Value).FirstOrDefault();
            return open is null ? null : Go(c, "Vaga de trabalho", open.Location);
        }
        var work = c.Offers.FirstOrDefault(o => o.Id == w.Id.Value && o.Kind is "workshop" or "field" && o.Verbs.Contains("work"));
        if (work is not null) return Use(c, work, "work", "Trabalhar");
        if (npc.CurrentLocation == w.Location) return Act(c, ActionType.Work, "Trabalhar");
        return Go(c, "Seu posto de trabalho", w.Location);
    }
}
