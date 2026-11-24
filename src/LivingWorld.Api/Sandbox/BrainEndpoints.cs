using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Cities.Streets;
using LivingWorld.Simulation.Hosting;

namespace LivingWorld.Api.Sandbox;

/// <summary>Uma sinapse da leitura: termo (entrada) → grupo (camada do meio) → ação (saída), o valor que somou e o ganho do NPC.</summary>
public sealed record BrainSynapseDto(ActionType Action, UtilityTerm Term, UtilityGroup Group, double Value, int GainMilli);

/// <summary>A nota de uma ação: a soma das sinapses dela.</summary>
public sealed record BrainScoreDto(ActionType Action, double Score);

/// <summary>O valor que o NPC dá ao próprio estado agora, parcela por parcela (a recompensa que ensina é a variação dele).</summary>
public sealed record BrainValueDto(double Body, double Pantry, double Money, double Mood, double Total);

/// <summary>AD-184: a decisão do NPC sobre a obra — a dele (diarista, família, parente, mutirão) ou a melhor que contrata na cidade
/// (candidato) — com os termos: jornal oferecido e o que ganha hoje por dia, a necessidade da casa (0–1), a parte da jornada que a
/// ida deixa (0–1), o valor (o termo <c>SiteWork</c>), o mínimo para aceitar e o motivo (aceita, salario, carencia, dono, longe,
/// turma-cheia, abaixo-do-minimo, sem-necessidade, idade, da-casa, outra-cidade, ou o papel de quem já está nela).</summary>
public sealed record BrainSiteWorkDto(
    long WorkId, string Role, LivingWorld.Domain.Geography.CellCoord Site, bool Joined, double OfferDay, double WageDay, double Need,
    double Share, double Value, double JoinMin, string Reason);
/// <summary>AD-183: por que um termo da decisão tem o valor que tem, quando o nome pesou: <c>xara</c> = o luto é pelo avô/avó (ou
/// pai/mãe) de quem a pessoa leva o nome (<see cref="OtherId"/> = o morto). Só termos que somaram algo na leitura.</summary>
public sealed record BrainMotiveDto(UtilityTerm Term, string Motive, long OtherId);

/// <summary>Resposta de <c>GET /npcs/{id}/brain</c> (AD-160b): a rede da decisão de agora, termo → grupo → ação; a soma das sinapses
/// de uma ação é exatamente a nota que o motor dá a ela. <see cref="Enabled"/> falso = cérebro desligado no mundo (ganhos zero).
/// <see cref="Motives"/> (AD-183): o nome por trás de um termo (vazio quando o nome não pesou).</summary>
public sealed record BrainReadingResponse(
    bool Enabled, IReadOnlyList<BrainSynapseDto> Synapses, IReadOnlyList<BrainScoreDto> Scores, BrainValueDto? Value,
    IReadOnlyList<BrainMotiveDto>? Motives = null, BrainSiteWorkDto? SiteWork = null);

/// <summary>"Ler o cérebro" (ADR-0046, opção b): projeção volátil do canônico; nunca escreve no mundo.</summary>
public static class BrainEndpoints
{
    public static void MapBrainEndpoints(this WebApplication app, WorldHost host)
    {
        app.MapGet("/npcs/{id:long}/brain", (long id) =>
        {
            var world = host.Current;
            var result = BrainReadout.Read(world, new NpcId(id));
            if (!result.IsSuccess) return Results.NotFound();
            var reading = result.Value!;
            var value = reading.Value is { } v ? new BrainValueDto(v.Body, v.Pantry, v.Money, v.Mood, v.Total) : null;
            // AD-184: aceitar ou recusar a obra, com os termos
            var site = world.FindNpc(new NpcId(id)) is { } npc && SiteWork.Explain(world, npc) is { } e
                ? new BrainSiteWorkDto(e.WorkId, e.Role, e.Site, e.Joined, e.Terms.OfferDay, e.Terms.WageDay, e.Terms.Need, e.Terms.Share,
                    e.Terms.Value, world.StreetRules.SiteJoinMin, e.Terms.Reason)
                : null;
            return Results.Ok(new BrainReadingResponse(
                world.BrainRules is { Enabled: true },
                reading.Synapses.Select(s => new BrainSynapseDto(s.Action, s.Term, s.Group, s.Value, s.GainMilli)).ToList(),
                reading.Scores.OrderBy(kv => kv.Key).Select(kv => new BrainScoreDto(kv.Key, kv.Value)).ToList(), value,
                NameMotives(world, new NpcId(id), reading.Synapses.Any(s => s.Term == UtilityTerm.Grief && s.Value != 0))
                    .Concat(CareMotives(world, new NpcId(id), reading.Synapses.Any(s => s.Action == ActionType.Socialize && s.Term == UtilityTerm.Social && s.Value != 0)))
                    .ToList(), site));
        });
    }

    /// <summary>AD-186: <c>procurar-companhia</c> no termo <c>Social</c> = está só e, se for conversar, sai atrás de alguém
    /// (<see cref="BrainMotiveDto.OtherId"/> = quem; −1 = o largo); <c>acolher-crianca</c> no termo <c>Provision</c> = a casa dele
    /// sustenta criança acolhida, sem pai nem mãe na casa (OtherId = a criança mais nova).</summary>
    public static IReadOnlyList<BrainMotiveDto> CareMotives(LivingWorld.Simulation.Core.WorldState world, NpcId id, bool socialWeighs)
    {
        if (world.FindNpc(id) is not { } npc) return [];
        var motives = new List<BrainMotiveDto>();
        if (socialWeighs && BehaviorDecisionSystem.CompanySought(world, npc) is { } other)
            motives.Add(new BrainMotiveDto(UtilityTerm.Social, "procurar-companhia", other));
        if (npc.Household is { } hh && world.FindHousehold(hh) is { } home)
        {
            var now = world.CurrentDate;
            LivingWorld.Domain.Population.Npc? youngest = null;
            foreach (var member in home.Members.OrderBy(m => m.Value))
                if (world.FindNpc(member) is { IsAlive: true } child && child.Id != npc.Id
                    && world.LifeStageRules.LifeStageOf(child.AgeYears(now)) == LivingWorld.Domain.Population.Body.LifeStage.Child
                    && (child.MotherId is not { } m || !home.Members.Contains(m)) && (child.FatherId is not { } f || !home.Members.Contains(f))
                    && (youngest is null || child.BirthDate.TotalHours > youngest.BirthDate.TotalHours))
                    youngest = child;
            if (youngest is not null) motives.Add(new BrainMotiveDto(UtilityTerm.Provision, "acolher-crianca", youngest.Id.Value));
        }
        return motives;
    }

    /// <summary>AD-183: o luto que pesa no termo <c>Grief</c> é pelo homenageado (o xará enluta o avô/avó de quem leva o nome)?
    /// Lê as lembranças de morte ainda vivas e devolve, das que são pelo homenageado, a mais forte.</summary>
    public static IReadOnlyList<BrainMotiveDto> NameMotives(LivingWorld.Simulation.Core.WorldState world, NpcId id, bool griefWeighs)
    {
        if (!griefWeighs || world.FindNpc(id) is not { Psyche: { } psyche } npc) return [];
        long tick = world.CurrentDate.TotalHours;
        var rules = world.NeedsRules.Psyche;
        BrainMotiveDto? best = null;
        double strongest = 0;
        foreach (var memory in psyche.Memories)
        {
            if (memory.Kind != LivingWorld.Domain.Cognition.LifeEventKind.FriendDied || memory.Other < 0) continue;
            if (world.FindNpc(new NpcId(memory.Other)) is not { } dead
                || !LivingWorld.Simulation.Population.Naming.NameBonds.IsNamesake(world, npc, dead)) continue;
            double strength = LivingWorld.Simulation.Cognition.PsycheMechanic.StrengthOf(rules, memory, tick, world.Calendar.HoursPerDay);
            if (strength > strongest) (strongest, best) = (strength, new BrainMotiveDto(UtilityTerm.Grief, "xara", memory.Other));
        }
        return best is null ? [] : [best];
    }
}
