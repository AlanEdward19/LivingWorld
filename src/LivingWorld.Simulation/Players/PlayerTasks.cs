using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Works;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

/// <summary>#241: a ação em andamento que ocupa o corpo do jogador, como o cliente a vê: o quê (<c>Action</c>), quando acaba (<c>UntilMs</c>,
/// ms de mundo), o tempo total e o que falta (<c>RemainingMs</c>, para o cliente não depender do relógio do mundo).
/// <c>Accelerated</c> (aditivo): o mundo corre acelerado até a ação acabar (o jogador está sozinho: o salto de tempo). <c>Units</c>,
/// <c>Done</c> e <c>ResourceId</c> (#243, aditivos, só na coleta): quantas unidades a sessão planejou, quantas já saíram até agora e o recurso.</summary>
public sealed record BusyView(string Action, long UntilMs, long TotalMs, long RemainingMs, bool Accelerated = false, long? Units = null, long? Done = null, int? ResourceId = null)
{
    /// <summary>#255 (aditivos, só no uso de objeto): o uso do catálogo (<c>Use</c>) e o nome que o jogador lê (<c>Label</c>), a fase (<c>walk</c>: o corpo vai até a peça;
    /// <c>do</c>: a ação corre), a pose declarada, onde o corpo fica (<c>SlotFx</c>, <c>SlotFy</c>, em células) e para onde olha (<c>Facing</c>, graus).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? Use { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? Label { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? Phase { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? Pose { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public double? SlotFx { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public double? SlotFy { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public double? Facing { get; init; }

    /// <summary>#259 (aditivo, só na interação social): o outro corpo (<c>With</c>, id do NPC) para quem este se vira, e se foi este quem começou (<c>Initiator</c>).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public long? With { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public bool? Initiator { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? PartnerPose { get; init; }
}

/// <summary>#241/#243: tarefas que ocupam o corpo conduzido (hoje, cortar lenha na mata). O padrão de toda ação que rende item: a unidade
/// <c>k</c> sai da fonte (a árvore, que é estado do mundo e finita: <see cref="PlayerNature.Take"/>) e vai para as mãos em
/// <c>início + k × ritmo</c> (o ritmo por unidade dos NPCs da obra); parar a qualquer hora deixa o que já saiu, sem entrega adiantada e
/// sem devolução; a árvore que se esgota acaba a tarefa. O motor decide a duração e quando o corpo volta a andar.</summary>
public static class PlayerTasks
{
    public const string Gather = "gather";

    /// <summary>A tarefa em curso agora, ou nula (nunca houve, ou já venceu).</summary>
    public static BusyView? Of(WorldState world, Npc npc)
    {
        long now = world.CurrentDate.TotalMilliseconds;
        if (npc.BodyTask is not { } task || now >= task.UntilMs) return null;
        var view = new BusyView(task.Action, task.UntilMs, task.UntilMs - task.StartedAtMs, task.UntilMs - now);
        if (task.Action == LivingWorld.Simulation.Objects.ObjectUsage.Action && LivingWorld.Simulation.Objects.ObjectUsage.View(world, task) is { } use)
            return view with { Use = use.Use, Label = use.Label, Phase = use.Phase, Pose = use.Pose, SlotFx = use.Fx, SlotFy = use.Fy, Facing = use.Facing };
        if (task.Action == LivingWorld.Simulation.Objects.SocialInteractions.Action && LivingWorld.Simulation.Objects.SocialInteractions.ViewOf(world, task) is { } social)
            return view with { Use = social.Id, Label = social.Name, Phase = social.Phase, Pose = social.Pose, With = social.With, Initiator = social.Initiator, PartnerPose = social.PartnerPose };
        if (PlayerGo.View(task) is { } go) return view with { Use = PlayerGo.Action, Label = go.Label, Phase = "walk", SlotFx = go.Fx, SlotFy = go.Fy }; // #267
        return task is { Action: Gather, PerUnitMs: > 0 }
            ? view with { Units = task.Units, Done = Math.Min(task.Units, (now - task.StartedAtMs) / task.PerUnitMs), ResourceId = task.ResourceId }
            : view;
    }

    /// <summary>O que uma sessão de corte rende e dura: o tempo de cortar por unidade é o dos NPCs da obra (<c>WorkHaul.CutHours</c> da carga
    /// que o corpo aguenta, sem a viagem: o jogador anda de verdade, em ms inteiros) e a sessão é o período todo até a árvore acabar:
    /// as unidades são as da madeira que resta (<paramref name="left"/>), no limite do que cabe nas mãos e do que o jogador pediu.
    /// Ficar até o fim derruba a árvore; parar antes deixa o que já saiu.</summary>
    internal static (long Units, long PerUnitMs) CutSession(WorldState world, Npc npc, ResourceType resource, CellCoord from, CellCoord wood, long? wanted, long left)
    {
        long cap = WorkHaul.CapacityUnits(world, npc, resource);
        long perUnitMs = Math.Max(1, (long)Math.Ceiling((double)WorkHaul.CutHours(world, from, wood, cap) * WorldMillis.PerHour / cap));
        long units = Math.Clamp(Math.Max(1, left), 1, Math.Max(1, Room(world, npc, resource)));
        if (wanted is { } w) units = Math.Min(units, Math.Max(1, w));
        return (units, perUnitMs);
    }

    /// <summary>Quantas unidades ainda cabem nas mãos: a carga que o corpo aguenta menos o que já leva do mesmo recurso (0 se leva outra coisa).</summary>
    internal static long Room(WorldState world, Npc npc, ResourceType resource)
    {
        if (npc.IsCarrying && npc.CarriedResourceId != resource.Id) return 0;
        return Math.Max(0, WorkHaul.CapacityUnits(world, npc, resource) - (npc.IsCarrying ? npc.CarriedQuantity : 0));
    }

    /// <summary>Começa o corte: o corpo fica ocupado as <paramref name="units"/> unidades no ritmo <paramref name="perUnitMs"/>, e a
    /// primeira sai ao fim da primeira unidade de tempo (nada entregue de antemão).</summary>
    internal static void BeginGather(WorldState world, TickContext ctx, Npc npc, ResourceType resource, CellCoord tree, long units, long perUnitMs)
    {
        long now = world.CurrentDate.TotalMilliseconds, until = now + units * perUnitMs;
        npc.Step(until, 0, now);
        npc.SetBodyTask(new BodyTask(Gather, now, until)
        { ResourceId = resource.Id, Units = units, PerUnitMs = perUnitMs, CellX = tree.X, CellY = tree.Y });
        ScheduleNext(ctx, npc, now + perUnitMs, now);
    }

    private static void ScheduleNext(TickContext ctx, Npc npc, long atMs, long startedAtMs) =>
        ctx.ScheduleEventAtMs(atMs, BodyTaskSystem.SystemName, $"{npc.Id.Value}|{startedAtMs}");

    /// <summary>Entrega as unidades que já saíram até agora (uma a uma, da árvore para as mãos) e agenda a próxima. A árvore sem madeira
    /// (outro a cortou) ou mãos que não comportam mais acabam a tarefa aí; a última unidade a encerra e libera o corpo.</summary>
    internal static void Settle(WorldState world, TickContext ctx, Npc npc, long startedAtMs)
    {
        if (npc.BodyTask is not { Action: Gather } task || task.StartedAtMs != startedAtMs || task.PerUnitMs <= 0) return; // evento velho: a ação foi interrompida
        long now = world.CurrentDate.TotalMilliseconds;
        if (npc.ControllerId is null)
        {
            npc.SetBodyTask(null); // ninguém conduz mais o corpo (saiu do personagem): o corte para onde está, o que saiu fica
            return;
        }
        long due = Math.Min(task.Units, (now - task.StartedAtMs) / task.PerUnitMs);
        var resource = new ResourceType(task.ResourceId);
        var tree = new CellCoord(task.CellX, task.CellY);
        long done = task.Done;
        bool ended = false;
        for (; done < due && !ended; done++)
        {
            if (PlayerNature.Take(world, tree, now, 1) < 1) { ended = true; break; } // a árvore acabou
            if (!npc.AddCarried(resource, 1, WorkHaul.CapacityUnits(world, npc, resource)).IsSuccess)
            {
                PlayerNature.Give(world, tree, now, 1); // não coube nas mãos: a madeira volta para a árvore
                ended = true;
                break;
            }
            world.RecordResourceProduced(resource, 1);
        }
        if (ended || done >= task.Units)
        {
            npc.Step(now, 0, now); // o corpo volta a andar
            npc.SetBodyTask(null);
            return;
        }
        npc.SetBodyTask(task with { Done = done });
        ScheduleNext(ctx, npc, task.StartedAtMs + (done + 1) * task.PerUnitMs, task.StartedAtMs);
    }

    /// <summary>Para o que o corpo faz agora: o que já saiu da árvore (até este instante) fica nas mãos, o resto não existiu. Sem
    /// tarefa em curso, recusa (409) e o mundo não muda.</summary>
    public static Result<BusyView> Interrupt(WorldState world, TickContext ctx, Npc npc)
    {
        if (Of(world, npc) is not { } busy || npc.BodyTask is not { } task)
            return Result<BusyView>.Fail($"{PlayerCommands.Conflict} interrupt: não há ação em andamento");
        long now = world.CurrentDate.TotalMilliseconds;
        if (task.Action == LivingWorld.Simulation.Objects.ObjectUsage.Action) // #255: o que já subiu fica; a fila vai embora com o uso
        {
            LivingWorld.Simulation.Objects.ObjectUsage.Stop(world, ctx, npc);
            return Result<BusyView>.Ok(busy);
        }
        if (task.Action == LivingWorld.Simulation.Objects.SocialInteractions.Action) // #259: a fração vivida vale; o outro corpo é solto junto
        {
            LivingWorld.Simulation.Objects.SocialInteractions.Stop(world, ctx, npc);
            return Result<BusyView>.Ok(busy);
        }
        if (task.Action == PlayerGo.Action) // #267: parou onde está
        {
            PlayerGo.Stop(world, npc);
            return Result<BusyView>.Ok(busy);
        }
        if (task.Action == Gather) Settle(world, ctx, npc, task.StartedAtMs); // o que já devia ter saído entre dois ticks sai agora
        npc.Step(now, 0, now);
        npc.SetBodyTask(null);
        return Result<BusyView>.Ok(busy);
    }
}
