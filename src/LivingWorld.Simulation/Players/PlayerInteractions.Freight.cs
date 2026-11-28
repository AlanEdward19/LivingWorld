using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

// Parte de PlayerInteractions: o transporte do móvel pago (#265). Fora do modo comprar (que só abre dentro de casa) o corpo precisa poder levar, carregar, dirigir, descarregar e pedir a entrega
// onde as coisas estão: a loja, o carro, a porta de casa. A roda oferece só o que o motor diz que vale agora (a mesma validação do comando de casa), com o texto dele.
public static partial class PlayerInteractions
{
    public const string FreightKind = "freight";

    public const string GoShopVerb = "goto:shop";

    /// <summary>A soleira da loja de móveis do mundo (a que vende o catálogo de compra), ou nula.</summary>
    private static CellCoord? ShopDoorstep(WorldState world) =>
        world.ObjectCatalog?.Home is { } home && HomeCommands.Shop(world, home) is { Building: { } id } && world.FindBuilding(id) is { } place && PlayerGround.DoorOf(place) is { } door ? door.Doorstep : null;

    /// <summary>#268: "Ir à loja" pela roda do E: o mesmo "ir até" do jogo, com o caminho do motor; a loja abre a roda do balcão ao chegar.</summary>
    private static Result<InteractionResult> GoShop(WorldState world, TickContext ctx, Npc npc)
    {
        if (ShopDoorstep(world) is not { } doorstep) return Fail($"{PlayerCommands.NotFound} loja: não há loja de móveis no bairro");
        var went = PlayerGo.Begin(world, ctx, npc, new PlayerGo.Target(doorstep, null), "Ir à loja");
        return went.IsSuccess ? Result<InteractionResult>.Ok(new InteractionResult(FreightKind, 0, GoShopVerb, 0, 0, "going", "Indo à loja de móveis")) : Fail(went.Error!);
    }

    private static void FreightOffers(WorldState world, Npc npc, CellCoord at, List<InteractionOffer> offers)
    {
        if (world.ObjectCatalog?.Home?.Haul is null || npc.Household is not { } h || world.FindHousehold(h)?.Dwelling is not { } dwelling || world.FindBuilding(dwelling) is not { } building) return;
        var uses = new List<OfferUse>();
        void Try(string verb)
        {
            var check = HomeCommands.Check(world, npc, building.Id.Value, verb);
            if (check.IsSuccess) uses.Add(new OfferUse(verb, check.Value!.Note, 0, null, false));
        }

        foreach (var p in building.Edit?.Parcels ?? [])
            foreach (var verb in p.Where switch { "shop" => new[] { "fetch", "fetchhelp", "load", "deliver" }, "hands" => ["drop"], "car" => ["unload"], _ => [] })
                Try($"{verb}:{p.Seq}");
        Try("drive:shop");
        Try("drive:home");
        if (ShopDoorstep(world) is { } doorstep && Chebyshev(PlayerGround.PoseOf(world, npc), doorstep) > 3)
            uses.Add(new OfferUse(GoShopVerb, "Ir à loja de móveis", 0, null, false)); // #268: de qualquer lugar da cidade, o corpo vai até a porta da loja
        if (uses.Count > 0) offers.Add(Offer(FreightKind, building.Id.Value, "Transporte", uses.Select(u => u.Id).ToList(), at) with { Uses = uses });
    }
}
