using System.Collections.Concurrent;
using LivingWorld.Api.Admin;
using LivingWorld.Api.Visual.Scope;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Api.World;

/// <summary>Etapa da criação para a tela de carregamento (AD-175): id estável, frase para o jogador e posição.</summary>
public sealed record WorldCreationStage(string Id, string Label);

/// <summary>Estado de uma criação em segundo plano: etapa atual (<see cref="StageIndex"/> de <see cref="StageCount"/>),
/// fim (<see cref="Done"/>), falha (<see cref="Status"/> 400/409 e <see cref="Error"/>) e, ao terminar bem,
/// <see cref="Result"/> igual à resposta de <c>POST /worlds/create</c>.</summary>
public sealed record WorldCreationJobResponse(
    Guid JobId, string Stage, string StageLabel, int StageIndex, int StageCount, bool Done, bool Failed, int Status, string Error,
    CreateWorldResponse? Result, bool Running);

public sealed record WorldCreationAccepted(Guid JobId, string StatusUrl, IReadOnlyList<WorldCreationStage> Stages);

/// <summary>Criação de mundo compartilhada pela rota síncrona de sempre e pela assíncrona (AD-175). As etapas são as reais:
/// as de <see cref="ScenarioLoadStages"/> avisadas pelo loader, depois gravar e pronto.</summary>
public static class WorldCreation
{
    public static readonly IReadOnlyList<WorldCreationStage> Stages =
    [
        new(ScenarioLoadStages.Terrain, "Medindo as terras e os rios"),
        new(ScenarioLoadStages.Settlements, "Fundando as vilas e as oficinas"),
        new(ScenarioLoadStages.Families, "Chamando as famílias"),
        new(ScenarioLoadStages.Economy, "Abrindo os ofícios e o comércio"),
        new("saving", "Guardando o mundo"),
        new("ready", "Pronto!"),
    ];

    /// <summary>Monta, grava e troca o mundo aberto. Devolve (status, erro, resposta): 200, 400 (cenário) ou 409 (seed).</summary>
    public static (int Status, string Error, CreateWorldResponse? Response) Run(
        CreateWorldRequest request, WorldHost host, SavedWorldLibrary library, SimulationHost simulation, Action<string>? progress = null)
    {
        var result = ScenarioLoaderV2.LoadWorld(request.ScenarioJson, progress: progress);
        if (!result.IsSuccess) return (400, result.Error!, null);

        var (world, clock) = result.Value!;
        world.Rename(request.Name);
        world.AssignPeriod(request.PeriodId?.Trim() ?? "");
        progress?.Invoke("saving");
        if (!library.Create(host, simulation, world, clock))
            return (409, "Já existe um mundo salvo com esta seed. Carregue o mundo existente.", null);

        var initialScope = new VisualScope(VisualScopeKind.World, "").ScopeKey;
        progress?.Invoke("ready");
        return (200, "", new CreateWorldResponse(
            world.Npcs.Count, WorldIdentity.WorldIdFor(world.Seed), world.Name, world.CurrentDate.TotalHours, initialScope));
    }

    /// <summary><c>startRunning=true</c> mexe no relógio, que é do admin (ADR-0015): exige o mesmo token de
    /// <c>/simulation/resume</c>. Devolve false quando o pedido quer rodar sem token válido (a rota responde 403).</summary>
    public static bool MayStart(bool? startRunning, HttpContext http, IConfiguration config, out AdminAccess? access)
    {
        access = startRunning == true ? AdminEndpoints.Authorize(http, config) : null;
        return startRunning != true || access is not null;
    }

    /// <summary>Solta o relógio do mundo recém-aberto, auditado como <c>/simulation/resume</c>.</summary>
    public static void Start(AdminAccess access, SimulationHost simulation, WorldHost host, AdminAuditStore audit)
    {
        simulation.Resume();
        audit.Append(BranchId.Root, host.Current.CurrentDate.TotalHours, access.Actor, "world.resume", "world", "startRunning");
    }
}

/// <summary>Criações em segundo plano desta API (memória do processo; as 16 mais recentes).</summary>
public sealed class WorldCreationJobs
{
    private const int Kept = 16;
    private readonly ConcurrentDictionary<Guid, WorldCreationJobResponse> _jobs = new();
    private readonly ConcurrentQueue<Guid> _order = new();

    public WorldCreationJobResponse Begin()
    {
        var first = WorldCreation.Stages[0];
        var job = new WorldCreationJobResponse(Guid.NewGuid(), first.Id, first.Label, 0, WorldCreation.Stages.Count,
            false, false, 202, "", null, false);
        _jobs[job.JobId] = job;
        _order.Enqueue(job.JobId);
        while (_order.Count > Kept && _order.TryDequeue(out var old)) _jobs.TryRemove(old, out _);
        return job;
    }

    public WorldCreationJobResponse? Find(Guid id) => _jobs.TryGetValue(id, out var job) ? job : null;

    public void Advance(Guid id, string stage)
    {
        int index = WorldCreation.Stages.ToList().FindIndex(s => s.Id == stage);
        if (index < 0 || !_jobs.TryGetValue(id, out var job)) return;
        _jobs[id] = job with { Stage = stage, StageLabel = WorldCreation.Stages[index].Label, StageIndex = index };
    }

    public void Finish(Guid id, int status, string error, CreateWorldResponse? result, bool running)
    {
        if (!_jobs.TryGetValue(id, out var job)) return;
        _jobs[id] = job with { Done = true, Failed = status != 200, Status = status, Error = error, Result = result, Running = running };
    }
}
