using LivingWorld.Api.Visual.Catalogs;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Api.Players;

/// <summary>#235: metas e diário do corpo controlado. Só lê, sob a trava do mundo, e só sob pedido (nunca por tick). As metas nascem do
/// estado do motor (<see cref="PlayerGoals"/>); o diário reaproveita a biografia (<see cref="PlayerDiary"/>); o texto é do catálogo de
/// apresentação, nunca da LLM.</summary>
public sealed partial class PlayerCommandGateway
{
    private PlayerReply NoBody(string playerId) =>
        new(StatusCodes.Status404NotFound, null, $"{PlayerCommands.NotFound} jogador não controla nenhum NPC");

    /// <summary>As metas do corpo, da mais urgente para a menos, cada uma com o próximo passo, e o progresso das habilidades.</summary>
    public PlayerReply Goals(string playerId)
    {
        lock (library.Gate)
        {
            var world = host.Current;
            if (PlayerCommands.ControlledBy(world, playerId) is not { } npc) return NoBody(playerId);
            var names = Names?.For(world.PeriodId);
            var rules = ScenarioRunner.DefaultSkillsRules;
            return new PlayerReply(StatusCodes.Status200OK, new
            {
                playerId,
                npcId = npc.Id.Value,
                urgencyLine = world.NeedsRules.UrgencyThreshold,
                goals = PlayerGoals.Of(world, npc).Select(g => new
                {
                    kind = g.Kind,
                    reason = g.Reason,
                    urgency = g.Urgency,
                    step = g.Step is { } s
                        ? new
                        {
                            command = s.Command,
                            kind = s.Kind,
                            targetId = s.TargetId,
                            verb = s.Verb,
                            label = s.Label,
                            x = s.X,
                            y = s.Y,
                            distance = s.Distance,
                            here = s.Here,
                            resourceId = s.ResourceId ?? -1
                        }
                        : new { command = "none", kind = "", targetId = 0L, verb = "", label = "", x = 0, y = 0, distance = 0, here = false, resourceId = -1 },
                }).ToList(),
                skills = PlayerSkills.Of(world, npc, rules).Select(k => new
                {
                    skillId = k.SkillId,
                    name = names?.Profession(rules.SkillByProfession.Where(kv => kv.Value.Id == k.SkillId).Select(kv => kv.Key).DefaultIfEmpty(-1).Min()) ?? "",
                    points = k.Points,
                    cap = k.Cap,
                    level = k.Level,
                    maxLevel = k.MaxLevel,
                    toNext = k.ToNext,
                    fraction = k.Fraction,
                    practicing = k.Practicing,
                    gainPerPractice = k.GainPerPractice,
                }).ToList(),
            }, null);
        }
    }

    /// <summary>O diário: o que o mundo registrou sobre a pessoa e o que o jogador fez com o corpo, do mais novo para o mais velho.</summary>
    public PlayerReply Diary(string playerId)
    {
        lock (library.Gate)
        {
            var world = host.Current;
            if (PlayerCommands.ControlledBy(world, playerId) is not { } npc) return NoBody(playerId);
            long? since = PlayerDiary.ControlledSince(players.Log, playerId, npc.Id.Value);
            var calendar = world.Calendar;
            return new PlayerReply(StatusCodes.Status200OK, new
            {
                playerId,
                npcId = npc.Id.Value,
                name = npc.Name,
                controlledSinceMs = since ?? 0L,
                entries = PlayerDiary.Of(world, npc, playerId, players.Log, since).Select(e =>
                {
                    long hours = e.AtMs / WorldMillis.PerHour;
                    var other = e.TargetId is { } id && world.FindNpc(new NpcId(id)) is { } o ? o.Name : "";
                    return new
                    {
                        atMs = e.AtMs,
                        year = hours / calendar.HoursPerYear + 1,
                        month = hours % calendar.HoursPerYear / calendar.HoursPerMonth + 1,
                        day = hours % calendar.HoursPerMonth / calendar.HoursPerDay + 1,
                        hour = (int)(hours % calendar.HoursPerDay),
                        source = e.Source,
                        kind = e.Kind,
                        text = DiaryText(e, other),
                        significance = e.Significance,
                        milestone = e.Milestone,
                        whileControlled = e.WhileControlled,
                        otherId = other.Length > 0 ? e.TargetId ?? -1L : -1L,
                        otherName = other,
                    };
                }).ToList(),
            }, null);
        }
    }

    private static readonly IReadOnlyDictionary<string, string> Deeds = new Dictionary<string, string>
    {
        ["Eat"] = "Comeu",
        ["Sleep"] = "Dormiu",
        ["Work"] = "Trabalhou",
        ["Socialize"] = "Foi ao convívio",
        ["Idle"] = "Ficou à toa",
        ["Buy"] = "Foi às compras",
        ["Steal"] = "Furtou",
        ["Cry"] = "Chorou",
        ["UsePower"] = "Usou um poder",
        ["Travel"] = "Pôs-se a caminho",
        ["take"] = "Pegou algo na despensa",
        ["deposit"] = "Guardou o que levava",
        ["buy"] = "Comprou na banca",
        ["sell"] = "Vendeu",
        ["talk"] = "Conversou",
        ["strike"] = "Golpeou",
        ["abandon"] = "Abandonou o filho",
        ["drink"] = "Bebeu",
        ["fill"] = "Encheu o cântaro",
        ["enter"] = "Entrou num prédio",
        ["exit"] = "Saiu de um prédio",
        ["work"] = "Foi trabalhar",
        ["steal"] = "Furtou",
        ["collect"] = "Recolheu um achado",
        ["up"] = "Subiu",
        ["down"] = "Desceu",
        ["return"] = "Devolveu o que levava",
    };

    /// <summary>Texto de uma linha do diário, em português: o catálogo seguro do log para o que o mundo registrou (nada de payload
    /// técnico), a tabela de verbos para o que o jogador fez; "com X" quando há outra pessoa.</summary>
    private static string DiaryText(DiaryEntry e, string other)
    {
        string text = e.Source == "fact" && Enum.TryParse<WorldEventKind>(e.Kind, out var kind)
            ? LivingEventPresentationCatalog.Describe(kind)
            : e.Verb is { } verb && Deeds.TryGetValue(verb, out var did) ? did
            : e.Kind switch { "Persuade" => "Tentou convencer quem o possui", "Strike" => "Golpeou", "Sleep" => "Dormiu", _ => "Agiu" };
        return other.Length > 0 ? $"{text} (com {other})" : text;
    }
}
