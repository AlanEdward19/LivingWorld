using System.Globalization;
using System.Text.Json.Nodes;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Objects;

/// <summary>#263: lê do cenário o bloco <c>Careers</c> ({rules, careers, bills}; ver <see cref="CareerCatalog"/>) e o junta ao catálogo padrão
/// <c>scenarios/catalog/careers.json</c> que o cenário estende (<c>CatalogExtends</c>). O código não conhece nenhuma carreira nem conta: tudo é dado.</summary>
public static class CareerLoader
{
    /// <summary>Junta o bloco do catálogo-base ao do cenário: carreira e conta de mesmo id ficam como o cenário as tem; o resto entra no fim.</summary>
    public static void Merge(JsonObject root, JsonObject catalog)
    {
        if (catalog["Careers"] is not JsonObject baseBlock) return;
        if (root["Careers"] is not JsonObject own) { root["Careers"] = baseBlock.DeepClone(); return; }
        if (own["rules"] is null && baseBlock["rules"] is { } rules) own["rules"] = rules.DeepClone();
        foreach (var key in new[] { "careers", "bills" })
        {
            if (baseBlock[key] is not JsonArray baseList) continue;
            if (own[key] is not JsonArray list) { own[key] = baseList.DeepClone(); continue; }
            var have = list.Select(o => o?["id"]?.ToString()).ToHashSet();
            foreach (var item in baseList)
                if (item is JsonObject o && have.Add(o["id"]?.ToString())) list.Add(o.DeepClone());
        }
    }

    public static Result<CareerCatalog?> Parse(JsonNode? node, NeedCatalog? needs, IReadOnlyList<ObjectDescriptor> objects)
    {
        if (node is null) return Result<CareerCatalog?>.Ok(null);
        if (node is not JsonObject block) return Fail("Careers: precisa ser um objeto");
        bool KnownNeed(string id) => NeedIds.IsBuiltin(id) || needs?.Find(id) is not null;
        var rules = ParseRules(block["rules"]);
        if (!rules.IsSuccess) return Fail(rules.Error!);

        var careers = new List<CareerDef>();
        if (block["careers"] is JsonArray list)
            for (int i = 0; i < list.Count; i++)
            {
                var at = $"Careers.careers[{i}]";
                var parsed = ParseCareer(list[i], at, KnownNeed);
                if (!parsed.IsSuccess) return Fail(parsed.Error!);
                if (careers.Any(c => c.Id == parsed.Value!.Id)) return Fail($"{at}.id: '{parsed.Value!.Id}' duplicado");
                careers.Add(parsed.Value!);
            }

        var useIds = objects.SelectMany(o => o.Uses ?? []).SelectMany(u => new[] { u.Id, u.Toggle ?? "" }).ToHashSet();
        var bills = new List<BillDef>();
        if (block["bills"] is JsonArray billList)
            for (int i = 0; i < billList.Count; i++)
            {
                var at = $"Careers.bills[{i}]";
                var parsed = ParseBill(billList[i], at, useIds);
                if (!parsed.IsSuccess) return Fail(parsed.Error!);
                if (bills.Any(c => c.Id == parsed.Value!.Id)) return Fail($"{at}.id: '{parsed.Value!.Id}' duplicado");
                bills.Add(parsed.Value!);
            }

        return careers.Count == 0 && bills.Count == 0 ? Result<CareerCatalog?>.Ok(null) : Result<CareerCatalog?>.Ok(new CareerCatalog(rules.Value!, careers, bills));
    }

    private static Result<CareerRules> ParseRules(JsonNode? node)
    {
        var d = new CareerRules();
        if (node is null) return Result<CareerRules>.Ok(d);
        if (node is not JsonObject o) return Result<CareerRules>.Fail("Careers.rules: precisa ser um objeto");
        int Int(string key, int fallback, int min, int max, out string? error)
        {
            error = null;
            if (o[key] is null) return fallback;
            if (!Number(o[key], out var v) || v < min || v > max || v != Math.Floor(v)) { error = $"Careers.rules.{key}: inteiro de {min} a {max}"; return fallback; }
            return (int)v;
        }

        int week = Int("weekDays", d.WeekDays, 1, 14, out var e1), lead = Int("noticeLeadHours", d.NoticeLeadHours, 0, 12, out var e2), grace = Int("graceHours", d.GraceHours, 0, 12, out var e3),
            fire = Int("absencesToFire", d.AbsencesToFire, 1, 30, out var e4), billHour = Int("billHour", d.BillHour, 0, 23, out var e5), keep = Int("noticeKeep", d.NoticeKeep, 4, 400, out var e6),
            valid = Int("searchValidHours", d.SearchValidHours, 1, 720, out var e7), open = Int("applyOpenHour", d.ApplyOpenHour, 0, 23, out var e8), close = Int("applyCloseHour", d.ApplyCloseHour, 1, 24, out var e9);
        foreach (var error in new[] { e1, e2, e3, e4, e5, e6, e7, e8, e9 }) if (error is not null) return Result<CareerRules>.Fail(error);
        double idle = d.IdleSearchPerDay;
        if (o["idleSearchPerDay"] is not null && (!Number(o["idleSearchPerDay"], out idle) || idle is < 0 or > 1)) return Result<CareerRules>.Fail("Careers.rules.idleSearchPerDay: número de 0 a 1");
        List<string>? names = null;
        if (o["dayNames"] is not null)
        {
            if (o["dayNames"] is not JsonArray nameArray || nameArray.Count != week) return Result<CareerRules>.Fail("Careers.rules.dayNames: lista de textos, um por dia da semana (weekDays)");
            names = [];
            foreach (var item in nameArray)
            {
                if (!Text(item, out var n) || n.Length is 0 or > 8) return Result<CareerRules>.Fail("Careers.rules.dayNames: textos de 1 a 8 caracteres");
                names.Add(n);
            }
        }

        bool atEmployer = o["applyAtEmployer"] is JsonValue av && av.TryGetValue(out bool ab) && ab;
        double reach = d.ApplyReachM;
        if (o["applyReachM"] is not null && (!Number(o["applyReachM"], out reach) || reach is < 1 or > 200)) return Result<CareerRules>.Fail("Careers.rules.applyReachM: metros de 1 a 200");
        if (close <= open) return Result<CareerRules>.Fail("Careers.rules.applyCloseHour: depois de applyOpenHour");
        List<string>? stop = null; // #267: os tipos de aviso que pedem tempo normal
        if (o["stopKinds"] is not null)
        {
            if (o["stopKinds"] is not JsonArray stopArray) return Result<CareerRules>.Fail("Careers.rules.stopKinds: lista de textos");
            stop = [];
            foreach (var item in stopArray)
            {
                if (!Text(item, out var kind) || kind.Length is 0 or > 24) return Result<CareerRules>.Fail("Careers.rules.stopKinds: textos de 1 a 24 caracteres");
                stop.Add(kind);
            }
        }

        return Result<CareerRules>.Ok(new CareerRules(week, lead, grace, fire, billHour, keep, valid, idle, names, stop, atEmployer, reach, open, close));
    }

    private static Result<CareerDef> ParseCareer(JsonNode? node, string at, Func<string, bool> knownNeed)
    {
        if (node is not JsonObject o || !Text(o["id"], out var id) || id.Length == 0) return Result<CareerDef>.Fail($"{at}.id: texto obrigatório");
        if (!Text(o["name"], out var name) || name.Length is 0 or > 32) return Result<CareerDef>.Fail($"{at}.name: texto de 1 a 32 caracteres");
        if (!Number(o["employerType"], out var type) || type < 0) return Result<CareerDef>.Fail($"{at}.employerType: id do tipo de local (LocationType) do empregador");
        if (!Number(o["commuteMin"], out var commute) || commute is < 0 or > 240) return Result<CareerDef>.Fail($"{at}.commuteMin: minutos de 0 a 240");
        int? skill = null;
        string? skillName = null;
        if (o["skillId"] is not null)
        {
            if (!Number(o["skillId"], out var s) || s < 0) return Result<CareerDef>.Fail($"{at}.skillId: id de habilidade");
            skill = (int)s;
            if (o["skillName"] is not null && !Text(o["skillName"], out skillName)) return Result<CareerDef>.Fail($"{at}.skillName: texto");
        }

        if (o["levels"] is not JsonArray levelArray || levelArray.Count == 0) return Result<CareerDef>.Fail($"{at}.levels: lista com ao menos um nível");
        var levels = new List<CareerLevel>();
        for (int i = 0; i < levelArray.Count; i++)
        {
            var path = $"{at}.levels[{i}]";
            if (levelArray[i] is not JsonObject l || !Text(l["id"], out var lid) || lid.Length == 0 || !Text(l["title"], out var title) || title.Length is 0 or > 40)
                return Result<CareerDef>.Fail($"{path}: {{id, title (1 a 40 caracteres)}}");
            if (levels.Any(x => x.Id == lid)) return Result<CareerDef>.Fail($"{path}.id: '{lid}' duplicado");
            if (!Number(l["wagePerHour"], out var wage) || wage < 0 || wage != Math.Floor(wage)) return Result<CareerDef>.Fail($"{path}.wagePerHour: moeda inteira por hora, 0 ou mais");
            if (!Number(l["startHour"], out var start) || start is < 0 or > 23 || start != Math.Floor(start)) return Result<CareerDef>.Fail($"{path}.startHour: hora de 0 a 23");
            if (!Number(l["hours"], out var hours) || hours is < 1 or > 16 || hours != Math.Floor(hours)) return Result<CareerDef>.Fail($"{path}.hours: horas de 1 a 16");
            var days = new List<int>();
            if (l["days"] is not JsonArray dayArray || dayArray.Count == 0) return Result<CareerDef>.Fail($"{path}.days: lista dos dias da semana (0 a 13)");
            foreach (var day in dayArray)
            {
                if (!Number(day, out var dv) || dv is < 0 or > 13 || dv != Math.Floor(dv)) return Result<CareerDef>.Fail($"{path}.days: dia da semana de 0 a 13");
                days.Add((int)dv);
            }

            double minSkill = 0, promoteAt = 101, demoteBelow = -1;
            int promoteAfter = 1, demoteAfter = 1;
            if (l["minSkill"] is not null && (!Number(l["minSkill"], out minSkill) || minSkill < 0)) return Result<CareerDef>.Fail($"{path}.minSkill: número maior ou igual a 0");
            if (l["promoteAt"] is not null && (!Number(l["promoteAt"], out promoteAt) || promoteAt is < 0 or > 101)) return Result<CareerDef>.Fail($"{path}.promoteAt: desempenho de 0 a 100");
            if (l["demoteBelow"] is not null && (!Number(l["demoteBelow"], out demoteBelow) || demoteBelow is < -1 or > 101)) return Result<CareerDef>.Fail($"{path}.demoteBelow: desempenho de 0 a 100 (101 rebaixa sempre)");
            if (l["promoteAfter"] is not null)
            {
                if (!Number(l["promoteAfter"], out var pa) || pa < 1 || pa > 100) return Result<CareerDef>.Fail($"{path}.promoteAfter: turnos de 1 a 100");
                promoteAfter = (int)pa;
            }

            if (l["demoteAfter"] is not null)
            {
                if (!Number(l["demoteAfter"], out var da) || da < 1 || da > 100) return Result<CareerDef>.Fail($"{path}.demoteAfter: turnos de 1 a 100");
                demoteAfter = (int)da;
            }

            var away = new List<UseEffect>();
            if (l["awayEffects"] is JsonArray awayArray)
                foreach (var item in awayArray)
                {
                    if (item is not JsonObject a || !Text(a["need"], out var need) || !knownNeed(need) || !Number(a["perHour"], out var per))
                        return Result<CareerDef>.Fail($"{path}.awayEffects: {{need (de sempre ou do catálogo), perHour}}");
                    away.Add(new UseEffect(need, per));
                }

            levels.Add(new CareerLevel(lid, title, (long)wage, (int)start, (int)hours, days, minSkill, promoteAt, promoteAfter, demoteBelow, demoteAfter, away));
        }

        var needs = new Dictionary<string, double>();
        double skillWeight = 0, practice = 0;
        if (o["performance"] is JsonObject perf)
        {
            if (perf["needs"] is JsonObject weights)
                foreach (var (key, value) in weights)
                {
                    if (!knownNeed(key) || !Number(value, out var w) || w < 0) return Result<CareerDef>.Fail($"{at}.performance.needs.{key}: necessidade conhecida e peso maior ou igual a 0");
                    needs[key] = w;
                }

            if (perf["skill"] is not null && (!Number(perf["skill"], out skillWeight) || skillWeight < 0)) return Result<CareerDef>.Fail($"{at}.performance.skill: peso maior ou igual a 0");
            if (perf["practice"] is not null && (!Number(perf["practice"], out practice) || practice < 0)) return Result<CareerDef>.Fail($"{at}.performance.practice: número maior ou igual a 0");
        }
        else return Result<CareerDef>.Fail($"{at}.performance: {{needs, skill?, practice?}} obrigatório");

        double baseChance = 0.5, perSkill = 0;
        if (o["search"] is JsonObject search)
        {
            if (search["baseChance"] is not null && (!Number(search["baseChance"], out baseChance) || baseChance is < 0 or > 1)) return Result<CareerDef>.Fail($"{at}.search.baseChance: de 0 a 1");
            if (search["perSkill"] is not null && !Number(search["perSkill"], out perSkill)) return Result<CareerDef>.Fail($"{at}.search.perSkill: número");
        }

        return Result<CareerDef>.Ok(new CareerDef(id, name, (int)type, (int)commute, levels, new CareerPerformance(needs, skillWeight, practice), new CareerSearch(baseChance, perSkill), skill, skillName));
    }

    private static Result<BillDef> ParseBill(JsonNode? node, string at, HashSet<string> useIds)
    {
        if (node is not JsonObject o || !Text(o["id"], out var id) || id.Length == 0) return Result<BillDef>.Fail($"{at}.id: texto obrigatório");
        if (!Text(o["name"], out var name) || name.Length is 0 or > 32) return Result<BillDef>.Fail($"{at}.name: texto de 1 a 32 caracteres");
        double fixedAmount = 0, perHour = 0, fee = 0, firstDay = 1;
        if (o["fixed"] is not null && (!Number(o["fixed"], out fixedAmount) || fixedAmount < 0 || fixedAmount != Math.Floor(fixedAmount))) return Result<BillDef>.Fail($"{at}.fixed: moeda inteira, 0 ou mais");
        if (!Number(o["everyDays"], out var every) || every < 1 || every > 365 || every != Math.Floor(every)) return Result<BillDef>.Fail($"{at}.everyDays: dias de 1 a 365");
        if (!Number(o["dueDays"], out var due) || due < 0 || due > 365 || due != Math.Floor(due)) return Result<BillDef>.Fail($"{at}.dueDays: dias de 0 a 365");
        if (o["perHour"] is not null && (!Number(o["perHour"], out perHour) || perHour < 0)) return Result<BillDef>.Fail($"{at}.perHour: moeda por hora de uso, 0 ou mais");
        if (o["lateFeeRate"] is not null && (!Number(o["lateFeeRate"], out fee) || fee is < 0 or > 1)) return Result<BillDef>.Fail($"{at}.lateFeeRate: de 0 a 1");
        double cut = 999;
        if (o["cutAfterDays"] is not null && (!Number(o["cutAfterDays"], out cut) || cut < 0)) return Result<BillDef>.Fail($"{at}.cutAfterDays: dias, 0 ou mais");
        if (o["firstIssueDay"] is not null && (!Number(o["firstIssueDay"], out firstDay) || firstDay < 0)) return Result<BillDef>.Fail($"{at}.firstIssueDay: dia, 0 ou mais");
        var meter = new List<string>();
        if (o["meterUses"] is JsonArray meterArray)
            foreach (var item in meterArray)
            {
                if (!Text(item, out var u) || !useIds.Contains(u)) return Result<BillDef>.Fail($"{at}.meterUses: '{item}' não é uso nem toggle de nenhum objeto");
                meter.Add(u);
            }

        var blocks = new List<string>();
        if (o["blocks"] is JsonArray blockArray)
            foreach (var item in blockArray)
            {
                if (!Text(item, out var u) || !useIds.Contains(u)) return Result<BillDef>.Fail($"{at}.blocks: '{item}' não é uso nem toggle de nenhum objeto");
                blocks.Add(u);
            }

        if (perHour > 0 && meter.Count == 0) return Result<BillDef>.Fail($"{at}.perHour: sem meterUses não há o que medir");
        return Result<BillDef>.Ok(new BillDef(id, name, (long)fixedAmount, (int)every, (int)due, meter, perHour, fee, (int)cut, blocks, (int)firstDay));
    }

    private static Result<CareerCatalog?> Fail(string error) => Result<CareerCatalog?>.Fail(error);

    private static bool Text(JsonNode? node, out string value)
    {
        value = "";
        if (node is JsonValue v && v.TryGetValue(out string? s) && s is not null) { value = s; return true; }
        return false;
    }

    private static bool Number(JsonNode? node, out double value)
    {
        value = 0;
        if (node is not JsonValue v) return false;
        if (v.TryGetValue(out double d)) value = d;
        else if (v.TryGetValue(out int n)) value = n;
        else if (v.TryGetValue(out string? s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) value = parsed;
        else return false;
        return double.IsFinite(value);
    }
}
