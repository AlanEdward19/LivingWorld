using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Objects;

namespace LivingWorld.Simulation.Players;

// Parte de PlayerInteractions: o transporte do móvel pago (#265). Fora do modo comprar (que só abre dentro de casa) o corpo precisa poder levar, carregar, dirigir, descarregar e pedir a entrega
// onde as coisas estão: a loja, o carro, a porta de casa. A roda oferece só o que o motor diz que vale agora (a mesma validação do comando de casa), com o texto dele.
public static partial class PlayerInteractions
{
    public const string FreightKind = "freight";

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
        if (uses.Count > 0) offers.Add(Offer(FreightKind, building.Id.Value, "Transporte", uses.Select(u => u.Id).ToList(), at) with { Uses = uses });
    }
}
