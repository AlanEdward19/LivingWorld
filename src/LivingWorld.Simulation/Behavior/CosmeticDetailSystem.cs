using LivingWorld.Domain;
using LivingWorld.Simulation.Observation;

namespace LivingWorld.Simulation.Behavior;

/// <summary>Camada cosmética observacional (Fase 28, T5, LOD-10..12) — promove/rebaixa só posição
/// exata e micro-ação; nunca eventos de vida.</summary>
public enum CosmeticDetailLayer
{
    Approximate,
    FullDetail,
}

/// <summary>Estado cosmético por NPC — mutuamente exclusivo entre camada aproximada e detalhe pleno
/// (LOD-05).</summary>
public sealed record CosmeticNpcState(
    LazyPosition LazyPosition,
    CosmeticDetailLayer Layer,
    bool PendingMicroAction,
    int MicroActionOptionCount,
    int? MicroActionChoice,
    int RngDrawCount,
    long LazyTicksWhilePendingMicroAction);

/// <summary>Promove/rebaixa a camada cosmética (posição lazy vs. exata, micro-ação) conforme
/// <see cref="ObservationRegistry"/> — primeiro consumidor real de
/// <see cref="WorldRngRegistry.StreamFor"/> com propósito <c>"cosmetic"</c>.</summary>
public sealed class CosmeticDetailSystem(ObservationRegistry observation) : ILazyPositionWorld
{
    public const string CosmeticRngPurpose = "cosmetic";

    private readonly Dictionary<NpcId, CosmeticNpcState> _states = new();
    private readonly Dictionary<RouteId, MovementRoute> _routes = new();

    public ObservationRegistry Observation => observation;

    public bool TryGetRoute(RouteId routeId, out MovementRoute route) =>
        _routes.TryGetValue(routeId, out route!);

    public void RegisterRoute(RouteId routeId, MovementRoute route) => _routes[routeId] = route;

    public bool TryGetState(NpcId npcId, out CosmeticNpcState state) => _states.TryGetValue(npcId, out state!);

    public void EnsureNpc(Npc npc, WorldState world, long tick, bool pendingMicroAction = false, int optionCount = 3)
    {
        if (_states.ContainsKey(npc.Id))
            return;

        var layer = observation.IsObserved(npc, world)
            ? CosmeticDetailLayer.FullDetail
            : CosmeticDetailLayer.Approximate;

        _states[npc.Id] = new CosmeticNpcState(
            LazyPosition.Initial(ExactPosition(npc), tick),
            layer,
            pendingMicroAction,
            Math.Max(1, optionCount),
            null,
            RngDrawCount: 0,
            LazyTicksWhilePendingMicroAction: 0);
    }

    /// <summary>Posição exata se observado; <see cref="LazyPosition.ValueAt"/> caso contrário.</summary>
    public Position ResolvePosition(Npc npc, WorldState world, long tick)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(world);

        EnsureNpc(npc, world, tick);
        var state = _states[npc.Id];

        if (observation.IsObserved(npc, world))
            return ExactPosition(npc);

        return state.LazyPosition.ValueAt(tick, this);
    }

    /// <summary>Atualiza camada conforme observação e avança micro-ação/RNG quando aplicável.</summary>
    public void SyncObservation(Npc npc, WorldState world, long tick)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(world);

        EnsureNpc(npc, world, tick);
        var state = _states[npc.Id];
        bool observed = observation.IsObserved(npc, world);

        if (observed)
        {
            if (state.Layer == CosmeticDetailLayer.Approximate)
                OnPromoted(npc, world, tick);
            else
                MaterializeExactPosition(npc, tick);

            if (_states[npc.Id].PendingMicroAction)
                AdvanceMicroAction(npc, world, tick);
            return;
        }

        if (state.Layer == CosmeticDetailLayer.FullDetail)
            OnDemoted(npc, tick);
        else if (state.PendingMicroAction)
            _states[npc.Id] = state with { LazyTicksWhilePendingMicroAction = state.LazyTicksWhilePendingMicroAction + 1 };
    }

    /// <summary>Promove para detalhe pleno: recalcula posição pela fórmula fechada e resolve
    /// micro-ação pendente via <see cref="WorldRngRegistry.StreamFor"/> (LOD-10, LOD-11).</summary>
    public void OnPromoted(Npc npc, WorldState world, long tick)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(world);

        EnsureNpc(npc, world, tick);
        var state = _states[npc.Id];

        var exact = state.LazyPosition.ValueAt(tick, this);
        npc.MoveTo(exact, tick);

        int rngDrawCount = state.RngDrawCount;

        if (state.PendingMicroAction)
            rngDrawCount += (int)state.LazyTicksWhilePendingMicroAction;

        _states[npc.Id] = state with
        {
            Layer = CosmeticDetailLayer.FullDetail,
            LazyPosition = state.LazyPosition.WithPosition(exact, tick),
            RngDrawCount = rngDrawCount,
            LazyTicksWhilePendingMicroAction = 0,
        };
    }

    /// <summary>Rebaixa para camada aproximada — nunca mantém as duas camadas ativas (LOD-05).</summary>
    public void OnDemoted(Npc npc, long tick)
    {
        ArgumentNullException.ThrowIfNull(npc);

        if (!_states.TryGetValue(npc.Id, out var state))
            return;

        _states[npc.Id] = state with
        {
            Layer = CosmeticDetailLayer.Approximate,
            LazyPosition = state.LazyPosition.WithPosition(ExactPosition(npc), tick),
            LazyTicksWhilePendingMicroAction = 0,
        };
    }

    public void SetLazyPosition(NpcId npcId, LazyPosition lazyPosition) =>
        _states[npcId] = _states.TryGetValue(npcId, out var existing)
            ? existing with { LazyPosition = lazyPosition }
            : new CosmeticNpcState(
                lazyPosition,
                CosmeticDetailLayer.Approximate,
                PendingMicroAction: false,
                MicroActionOptionCount: 1,
                MicroActionChoice: null,
                RngDrawCount: 0,
                LazyTicksWhilePendingMicroAction: 0);

    public void RequestMicroAction(NpcId npcId, int optionCount)
    {
        if (!_states.TryGetValue(npcId, out var state))
            throw new InvalidOperationException($"NPC {npcId} não registrado em {nameof(CosmeticDetailSystem)}");

        _states[npcId] = state with
        {
            PendingMicroAction = true,
            MicroActionOptionCount = Math.Max(1, optionCount),
        };
    }

    private void MaterializeExactPosition(Npc npc, long tick)
    {
        var state = _states[npc.Id];
        var exact = state.LazyPosition.ValueAt(tick, this);
        if (ExactPosition(npc) == exact)
            return;

        npc.MoveTo(exact, tick);
        _states[npc.Id] = state with { LazyPosition = state.LazyPosition.WithPosition(exact, tick) };
    }

    private void AdvanceMicroAction(Npc npc, WorldState world, long tick)
    {
        var state = _states[npc.Id];
        int drawCount = state.RngDrawCount + 1;
        int choice = RollMicroAction(world.Rng, npc.Id, drawCount, state.MicroActionOptionCount);

        _states[npc.Id] = state with
        {
            RngDrawCount = drawCount,
            MicroActionChoice = choice,
        };

        _ = tick;
    }

    internal static Position ExactPosition(Npc npc) =>
        npc.Interior is { LocalCell: var cell }
            ? new Position(cell.X, cell.Y)
            : new Position(npc.CurrentLocation.X, npc.CurrentLocation.Y);

    internal static int RollMicroAction(WorldRngRegistry rngRegistry, NpcId npcId, int drawCount, int optionCount)
    {
        var rng = rngRegistry.StreamFor(CosmeticRngPurpose, npcId.Value);
        int choice = 0;
        for (int i = 0; i < drawCount; i++)
            choice = (int)(rng.NextDouble() * optionCount);

        return choice;
    }
}
