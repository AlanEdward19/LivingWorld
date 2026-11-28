using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Careers;

/// <summary>#263: dono da capacidade CAREERS. De hora em hora, no dado do catálogo <c>Careers</c>: avisa quem o jogador conduz (a hora do trabalho), manda para o turno quem ninguém
/// conduz, registra a falta de quem não saiu a tempo, emite e cobra as contas da casa e faz quem está sem emprego e é de casa de carreira procurar vaga. A volta do turno é evento
/// agendado no instante exato (<see cref="HandleEvent"/>). Sem o bloco <c>Careers</c> no cenário ou sem ninguém no livro, nada roda (o hash do mundo não muda).</summary>
[LivingCapability("CAREERS", Events = [], Consumers = ["economy.wages", "player.career-card"])]
[RunsAfter(typeof(BehaviorDecisionSystem))]
public sealed class CareerSystem : ISimulationSystem
{
    public const string SystemName = "careers";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (world.Careers is not { } ledger || CareerWork.CatalogOf(world) is not { } catalog) return;
        long nowMs = world.CurrentDate.TotalMilliseconds;
        long dayMs = (long)world.Calendar.HoursPerDay * WorldMillis.PerHour;
        long day = nowMs / dayMs;
        int hod = (int)(nowMs % dayMs / WorldMillis.PerHour);

        RunDue(world, nowMs); // #246: a entrevista marcada

        foreach (var job in ledger.Jobs.ToList())
        {
            if (world.FindNpc(new NpcId(job.Npc)) is not { IsAlive: true } npc) { ledger.RemoveJob(job.Npc); world.CareersChanged(); continue; }
            if (job.ReturnMs > 0 || catalog.Find(job.Career) is not { } def) continue;
            var level = def.Levels[Math.Clamp(job.Level, 0, def.Levels.Count - 1)];
            if (!CareerWork.IsWorkday(world, catalog, level, day) || job.ShiftDay == day) continue;
            int leave = CareerWork.LeaveHour(world, npc, def, level, job.Employer);
            if (npc.ControllerId is not null)
            {
                if (hod == leave - catalog.Rules.NoticeLeadHours && catalog.Rules.NoticeLeadHours > 0)
                {
                    ledger.Notify(nowMs, npc.Id.Value, npc.Household?.Value ?? 0, "shift-soon",
                        $"Hora do trabalho em {catalog.Rules.NoticeLeadHours} h: {level.Title}, às {level.StartHour:00}:00.", 0, "go", catalog.Rules.NoticeKeep);
                    world.CareersChanged();
                }
            }
            else if (hod == leave)
            {
                CareerWork.Depart(world, ctx, npc, nowMs);
                continue;
            }

            if (hod == level.StartHour + catalog.Rules.GraceHours) CareerWork.Absent(world, ctx, npc, nowMs);
        }

        if (hod == catalog.Rules.BillHour) CareerWork.TickBills(world, ctx, nowMs);
        CareerWork.MeterLights(world);
        if (hod == 10) IdleSearch(world, ledger, catalog, nowMs);
    }

    /// <summary>#246 (AD-237): as entrevistas marcadas cuja hora chegou correm agora.</summary>
    internal static void RunDue(WorldState world, long nowMs)
    {
        if (world.Careers is not { } ledger) return;
        foreach (var search in ledger.Searches.Where(x => x.InterviewAtMs > 0 && x.InterviewAtMs <= nowMs).ToList()) CareerWork.RunInterview(world, search, nowMs);
    }

    /// <summary>Quem ninguém conduz, adulto, sem emprego e de casa de carreira, olha as vagas de vez em quando (a chance do dado por dia) e se candidata: o mesmo caminho do jogador.</summary>
    private static void IdleSearch(WorldState world, CareerLedger ledger, CareerCatalog catalog, long nowMs)
    {
        if (catalog.Rules.IdleSearchPerDay <= 0) return;
        foreach (var npc in world.AliveNpcIndex.Alive)
        {
            if (npc.ControllerId is not null || npc.Household is not { } home || !ledger.HasAccount(home.Value) || ledger.JobOf(npc.Id.Value) is not null) continue;
            if (world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) != LivingWorld.Domain.Population.Body.LifeStage.Adult) continue;
            if (world.Rng.OneShot($"job-idle-{npc.Id.Value}-{nowMs}").NextDouble() >= catalog.Rules.IdleSearchPerDay) continue;
            CareerWork.Search(world, npc, nowMs);
            if (CareerWork.Vacancies(world, npc).FirstOrDefault() is { } vacancy) CareerWork.Apply(world, npc, vacancy.Career.Id, vacancy.Employer.Id.Value, nowMs);
        }
    }

    public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt)
    {
        var parts = evt.Payload?.Split('|');
        if (parts is not { Length: 3 } || parts[0] != "r" || !long.TryParse(parts[1], out long npcId) || !long.TryParse(parts[2], out long returnMs)) return;
        if (world.Careers?.JobOf(npcId) is not { } job || job.ReturnMs != returnMs || world.FindNpc(new NpcId(npcId)) is not { IsAlive: true } npc) return;
        CareerWork.Return(world, ctx, npc, world.CurrentDate.TotalMilliseconds);
    }
}
