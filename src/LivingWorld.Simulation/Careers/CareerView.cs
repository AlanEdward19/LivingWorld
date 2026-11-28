using System.Text.Json.Serialization;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Careers;

/// <summary>#263: o emprego de quem o corpo é, como o cartão e o painel de carreira o leem. Tudo vem do motor (o cliente só desenha): cargo, salário, horário, desempenho, o turno
/// de agora ou o próximo, o que falta para subir, as contas da casa, os avisos novos e as vagas que a última busca achou. Omitido (nulo) sem carreira nem conta no mundo.</summary>
public sealed record CareerView(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JobView? Job,
    IReadOnlyList<BillView> Bills,
    IReadOnlyList<NoticeView> Notices,
    IReadOnlyList<ListingView> Listings,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? DayNames = null);

public sealed record JobView(
    string Career, string CareerName, string Title, int LevelIndex, int Levels, long WagePerHour, int StartHour, int Hours, IReadOnlyList<int> Days, int Score, int Shifts, long Earned,
    int Good, int PromoteAfter, double PromoteAt, double DemoteBelow, int Bad, int DemoteAfter, int Absences, int AbsencesToFire,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? NextTitle,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? NextWagePerHour,
    bool Away, long ReturnMs, long WorkEndMs, long ArriveMs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? NextShiftMs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? NextLeaveMs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? NextShiftText,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CannotGo,
    bool CanGo, string? SkillName, double Skill,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ReturnText = null);

public sealed record BillView(string Id, string Name, long Open, long Fee, long DueMs, bool Cut, bool Late, double Usage, string DueText);

public sealed record NoticeView(long Id, long Ms, string Kind, string Text, long Amount, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Verb);

public sealed record ListingView(string Career, string CareerName, long Employer, string Title, long WagePerHour, int StartHour, int Hours, int Free, double Chance);

public static class CareerViews
{
    /// <summary>A carreira da pessoa no instante atual. Nula quando o mundo não tem carreira nem conta (cenário sem o catálogo) ou o livro está vazio.</summary>
    public static CareerView? Of(WorldState world, Npc npc)
    {
        if (CareerWork.CatalogOf(world) is not { } catalog || world.Careers is not { } ledger) return null;
        long nowMs = world.CurrentDate.TotalMilliseconds;
        JobView? job = null;
        if (ledger.JobOf(npc.Id.Value) is { } j && catalog.Find(j.Career) is { } def)
        {
            var level = def.Levels[Math.Clamp(j.Level, 0, def.Levels.Count - 1)];
            var next = j.Level + 1 < def.Levels.Count ? def.Levels[j.Level + 1] : null;
            var shift = CareerWork.NextShift(world, catalog, j, nowMs);
            string? cannot = j.ReturnMs > 0 ? null : CareerWork.CannotGo(world, npc, nowMs);
            double skill = def.SkillId is { } sid ? npc.Skills.Get(new LivingWorld.Domain.Population.Skills.SkillType(sid)) : 0;
            job = new JobView(def.Id, def.Name, level.Title, j.Level, def.Levels.Count, level.WagePerHour, level.StartHour, level.Hours, level.Days, j.Score, j.Shifts, j.Earned,
                j.Good, level.PromoteAfter, level.PromoteAt, level.DemoteBelow, j.Bad, level.DemoteAfter, j.Absences, catalog.Rules.AbsencesToFire,
                next?.Title, next?.WagePerHour, j.ReturnMs > 0, j.ReturnMs, j.WorkEndMs, j.ArriveMs,
                shift?.Start, shift?.Leave, shift is { } s ? CareerWork.Clock(world, s.Start) : null, cannot, j.ReturnMs == 0 && cannot is null, def.SkillName, skill, j.ReturnMs > 0 ? CareerWork.Clock(world, j.ReturnMs) : null);
        }

        var bills = new List<BillView>();
        if (npc.Household is { } home)
            foreach (var state in ledger.Bills)
                if (state.Household == home.Value && state.Open > 0 && catalog.FindBill(state.Bill) is { } bill)
                    bills.Add(new BillView(bill.Id, bill.Name, state.Open, state.Fee, state.DueMs, state.Cut, nowMs >= state.DueMs, Math.Round(state.Usage, 2), CareerWork.Clock(world, state.DueMs)));

        var notices = ledger.Notices.Where(n => n.Npc == npc.Id.Value || n.Npc == 0 && npc.Household is { } h && n.Household == h.Value).TakeLast(12)
            .Select(n => new NoticeView(n.Id, n.Ms, n.Kind, n.Text, n.Amount, n.Verb)).ToList();
        var listings = new List<ListingView>();
        if (job is null && CareerWork.SearchIsValid(world, catalog, npc, nowMs))
            foreach (var v in CareerWork.Vacancies(world, npc))
                listings.Add(new ListingView(v.Career.Id, v.Career.Name, v.Employer.Id.Value, v.Entry.Title, v.Entry.WagePerHour, v.Entry.StartHour, v.Entry.Hours, v.Free, Math.Round(v.Chance, 2)));
        return job is null && bills.Count == 0 && notices.Count == 0 && listings.Count == 0 && !ledger.HasAccount(npc.Household?.Value ?? -1) ? null : new CareerView(job, bills, notices, listings, catalog.Rules.DayNames);
    }
}
