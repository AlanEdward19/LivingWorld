using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Careers;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

// Parte de PlayerInteractions: o emprego e as contas da casa (#263). O corpo oferece a si mesmo "ir ao trabalho" (na janela do turno), candidatar-se às vagas que a busca listou
// e pagar as contas abertas em casa. Tudo validado contra a oferta do mesmo instante, como o resto.
public static partial class PlayerInteractions
{
    public const string JobKind = "job";
    public const string BillsKind = "bills";
    public const string WillKind = "will";

    private static Result<InteractionResult> FreeWillSwitch(WorldState world, Npc npc, string verb)
    {
        if (verb is not ("freewill:on" or "freewill:off") || Objects.CriticalNeeds.RulesOf(world) is null) return Fail("interact: will só aceita freewill:on ou freewill:off, e só com necessidades críticas no cenário");
        bool on = verb == "freewill:on";
        Objects.CriticalNeeds.SetFreeWill(world, npc, on);
        return Result<InteractionResult>.Ok(new InteractionResult(WillKind, npc.Id.Value, verb, 0, 0, "done", on ? "Livre-arbítrio ligado: o corpo sem ordem se cuida sozinho" : "Livre-arbítrio desligado: o corpo só faz o que você manda"));
    }

    private static void CareerOffers(WorldState world, Npc npc, CellCoord at, BuildingId? inside, Household? household, List<InteractionOffer> offers)
    {
        if (CareerWork.CatalogOf(world) is not { } catalog || world.Careers is not { } ledger) return;
        long nowMs = world.CurrentDate.TotalMilliseconds;
        var verbs = new List<string>();
        var uses = new List<OfferUse>(); // o texto de cada verbo, do dado: o cliente só o mostra
        string label = "";
        if (ledger.JobOf(npc.Id.Value) is { } job && catalog.Find(job.Career) is { } def)
        {
            label = def.Levels[Math.Clamp(job.Level, 0, def.Levels.Count - 1)].Title;
            if (CareerWork.CannotGo(world, npc, nowMs) is null) { verbs.Add("go"); uses.Add(new OfferUse("go", $"Ir ao trabalho · {label}", 0, null, false)); }
        }
        else if (CareerWork.SearchIsValid(world, catalog, npc, nowMs))
        {
            foreach (var vacancy in CareerWork.Vacancies(world, npc))
            {
                string verb = $"apply:{vacancy.Career.Id}@{vacancy.Employer.Id.Value}";
                verbs.Add(verb);
                uses.Add(new OfferUse(verb, $"Candidatar-se: {vacancy.Entry.Title} ({vacancy.Career.Name}, {vacancy.Entry.WagePerHour}/h)", 0, null, false));
            }

            label = "Vagas";
        }

        if (verbs.Count > 0) offers.Add(Offer(JobKind, npc.Id.Value, label, verbs, at) with { Uses = uses });
        if (household is null || inside is null || household.Dwelling != inside) return;
        var open = ledger.Bills.Where(b => b.Household == household.Id.Value && b.Open > 0).ToList();
        if (open.Count > 0)
            offers.Add(Offer(BillsKind, household.Id.Value, "Contas", open.Select(b => $"pay:{b.Bill}").ToList(), at) with
            {
                Uses = open.Select(b => new OfferUse($"pay:{b.Bill}", $"Pagar {catalog.FindBill(b.Bill)?.Name ?? b.Bill}: {b.Open + b.Fee}", 0, null, false)).ToList(),
            });
    }

    private static Result<InteractionResult> CareerInteract(WorldState world, TickContext ctx, Npc npc, string kind, long id, string verb)
    {
        var offer = Offers(world, npc).FirstOrDefault(o => o.Kind == kind && o.Verbs.Contains(verb) && (kind == JobKind ? o.Id == id : true));
        if (offer is null) return Fail($"{PlayerCommands.Conflict} interact: {kind} {verb} indisponível agora");
        long nowMs = world.CurrentDate.TotalMilliseconds;
        if (kind == BillsKind)
        {
            var paid = CareerWork.PayBill(world, ctx, npc, verb["pay:".Length..], nowMs);
            return paid.IsSuccess ? Result<InteractionResult>.Ok(new InteractionResult(kind, id, verb, 0, 0, "done", paid.Value)) : Fail(paid.Error!);
        }

        if (verb == "go")
        {
            var went = CareerWork.Depart(world, ctx, npc, nowMs);
            return went.IsSuccess ? Result<InteractionResult>.Ok(new InteractionResult(kind, id, verb, 0, 0, "done", "Foi trabalhar")) : Fail(went.Error!);
        }

        int at = verb.IndexOf('@');
        if (!verb.StartsWith("apply:", StringComparison.Ordinal) || at < 0 || !long.TryParse(verb[(at + 1)..], out long workplace)) return Fail("interact: verbo de trabalho desconhecido");
        var applied = CareerWork.BeginApply(world, ctx, npc, verb["apply:".Length..at], workplace, nowMs);
        return applied.IsSuccess ? Result<InteractionResult>.Ok(new InteractionResult(kind, id, verb, 0, 0, applied.Value.Outcome, applied.Value.Note)) : Fail(applied.Error!);
    }
}
