using System.Text.Json.Nodes;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Careers;

/// <summary>#263: o bloco opcional <c>CareerStart</c> do cenário: quem já começa empregado numa carreira do catálogo <c>Careers</c>:
/// <c>[{Member, Career, Level?, Employer}]</c> (<c>Member</c> é o nome da pessoa autorada, <c>Employer</c> o índice em <c>Workplaces[]</c>). Ausente, ninguém começa empregado
/// e quem quer emprego procura (jornal, computador). Pessoa de casa de carreira ganha as contas da casa na contratação.</summary>
public static class CareerSeeding
{
    public static Result<Unit> Seed(WorldState world, JsonObject root)
    {
        if (root["CareerStart"] is not { } node) return Result<Unit>.Ok(Unit.Value);
        if (node is not JsonArray list) return Result<Unit>.Fail("CareerStart: precisa ser uma lista");
        if (CareerWork.CatalogOf(world) is null) return Result<Unit>.Fail("CareerStart: o cenário não tem catálogo de carreiras (CatalogExtends: careers)");
        for (int i = 0; i < list.Count; i++)
        {
            var at = $"CareerStart[{i}]";
            if (list[i] is not JsonObject o || o["Member"]?.ToString() is not { Length: > 0 } member || o["Career"]?.ToString() is not { Length: > 0 } career)
                return Result<Unit>.Fail($"{at}: {{Member, Career, Employer}}");
            int level = o["Level"] is JsonValue lv && lv.TryGetValue(out int l) ? l : 0;
            if (o["Employer"] is not JsonValue ev || !ev.TryGetValue(out int employerIndex) || employerIndex < 0) return Result<Unit>.Fail($"{at}.Employer: índice (0 ou mais) em Workplaces[]");
            var npc = world.Npcs.FirstOrDefault(n => n.Name == member);
            var employer = world.Workplaces.Skip(employerIndex).FirstOrDefault();
            if (npc is null) return Result<Unit>.Fail($"{at}.Member: '{member}' não existe nas famílias autoradas");
            if (employer is null) return Result<Unit>.Fail($"{at}.Employer: Workplace {employerIndex} não existe em Workplaces[]");
            var hired = CareerWork.Hire(world, npc, career, level, employer, world.CurrentDate.TotalMilliseconds);
            if (!hired.IsSuccess) return Result<Unit>.Fail($"{at}: {hired.Error}");
        }

        return Result<Unit>.Ok(Unit.Value);
    }
}
