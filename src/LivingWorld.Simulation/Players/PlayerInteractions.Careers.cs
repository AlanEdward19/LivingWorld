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

    private static void CareerOffers(WorldState world, Npc npc, CellCoord at, BuildingId? inside, Household? household, List<InteractionOffer> offers)
    {
        if (CareerWork.CatalogOf(world) is not { } catalog || world.Careers is not { } ledger) return;
        long nowMs = world.CurrentDate.TotalMilliseconds;
        var verbs = new List<string>();
        string label = "";
        if (ledger.JobOf(npc.Id.Value) is { } job && catalog.Find(job.Career) is { } def)
        {
            label = def.Levels[Math.Clamp(job.Level, 0, def.Levels.Count - 1)].Title;
            if (CareerWork.CannotGo(world, npc, nowMs) is null) verbs.Add("go");
        }
        else if (CareerWork.SearchIsValid(world, catalog, npc, nowMs))
        {
            foreach (var vacancy in CareerWork.Vacancies(world, npc)) verbs.Add($"apply:{vacancy.Career.Id}@{vacancy.Employer.Id.Value}");
            label = "Vagas";
        }

        if (verbs.Count > 0) offers.Add(Offer(JobKind, npc.Id.Value, label, verbs, at));
        if (household is null || inside is null || household.Dwelling != inside) return;
        var open = ledger.Bills.Where(b => b.Household == household.Id.Value && b.Open > 0).Select(b => $"pay:{b.Bill}").ToList();
        if (open.Count > 0) offers.Add(Offer(BillsKind, household.Id.Value, "Contas", open, at));
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
        var applied = CareerWork.Apply(world, npc, verb["apply:".Length..at], workplace, nowMs);
        return applied.IsSuccess
            ? Result<InteractionResult>.Ok(new InteractionResult(kind, id, verb, 0, 0, applied.Value.Hired ? "done" : "refused", applied.Value.Note))
            : Fail(applied.Error!);
    }
}
