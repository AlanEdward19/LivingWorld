using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;

namespace LivingWorld.Simulation.Core;

/// <summary>NPCs vivos em ordem de id, mais um índice por célula — derivado, fora do hash canônico.
/// O índice espacial é mantido por <see cref="Npc.LocationObserver"/> (único mutador de posição:
/// <see cref="Npc.MoveTo"/>), então consultas de vizinhança custam O(janela) em vez de O(população).</summary>
public sealed class AliveNpcIndex
{
    private readonly List<Npc> _alive;
    private readonly Dictionary<CellCoord, List<Npc>> _byCell = new();

    // AD-197h: no passo fino, quem viaja está na célula da rota em que a pose de agora o põe (TravelPose), não na origem: percepção,
    // testemunha e encontro (tudo que pergunta "quem está aqui?") o veem no caminho. Vazio de hora em hora: At é o de sempre.
    private readonly Dictionary<long, CellCoord> _pose = new();
    private readonly Dictionary<CellCoord, List<Npc>> _byPose = new();
    private readonly Dictionary<long, Npc> _posed = new(); // AD-197l: quem tem pose, para soltar sem varrer os vivos
    private readonly List<Npc> _unpose = [];

    private AliveNpcIndex(List<Npc> alive)
    {
        _alive = alive;
        foreach (var npc in alive)
            Track(npc);
    }

    public IReadOnlyList<Npc> Alive => _alive;

    /// <summary>Muda a cada entrada ou saída de vivo: chave dos caches derivados dos vivos (AD-156, portadores vivos).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public long Version { get; private set; }

    /// <summary>Vivos com saúde ≤ <see cref="EndangeredHealth"/> (perigo observável). Mantido por
    /// <see cref="Npc.HealthObserver"/>: zero na imensa maioria dos ticks, e então nenhuma varredura de
    /// vizinhança pode encontrar ameaça.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int EndangeredCount { get; private set; }

    public const int EndangeredHealth = 1;

    /// <summary>NPCs vivos na célula (ordem do bucket não especificada). Vazio se nenhum. AD-197h: com poses de viagem no passo fino,
    /// quem viaja conta na célula da pose, não na da origem.</summary>
    public IReadOnlyList<Npc> At(CellCoord cell)
    {
        var bucket = _byCell.TryGetValue(cell, out var found) ? found : (IReadOnlyList<Npc>)Array.Empty<Npc>();
        if (_pose.Count == 0) return bucket;
        bool moved = false;
        for (int i = 0; i < bucket.Count && !moved; i++) moved = _pose.ContainsKey(bucket[i].Id.Value);
        bool arriving = _byPose.TryGetValue(cell, out var passing);
        if (!moved && !arriving) return bucket;
        var merged = new List<Npc>(bucket.Count + (passing?.Count ?? 0));
        foreach (var npc in bucket)
            if (!_pose.ContainsKey(npc.Id.Value)) merged.Add(npc);
        if (passing is not null) merged.AddRange(passing);
        return merged;
    }

    /// <summary>Há alguma pose de viagem em vigor. Fora do JSON: o hash volátil do índice fica o de antes.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasPoses => _pose.Count > 0;

    /// <summary>A célula da pose de viagem do NPC, se houver (no passo fino, longe da origem).</summary>
    public CellCoord? PoseOf(Npc npc) => _pose.TryGetValue(npc.Id.Value, out var cell) ? cell : null;

    /// <summary>Põe (ou tira, com nulo ou a própria célula do NPC) a pose de viagem.</summary>
    public void SetPose(Npc npc, CellCoord? cell)
    {
        long id = npc.Id.Value;
        bool had = _pose.TryGetValue(id, out var old);
        if (cell is not { } at || at == npc.CurrentLocation)
        {
            if (!had) return;
            _pose.Remove(id);
            _posed.Remove(id);
            RemoveFrom(_byPose, npc, old);
            return;
        }
        if (had && old == at) return;
        if (had) RemoveFrom(_byPose, npc, old);
        _pose[id] = at;
        _posed[id] = npc;
        AddTo(_byPose, npc, at);
    }

    public void ClearPoses()
    {
        _pose.Clear();
        _posed.Clear();
        _byPose.Clear();
    }

    /// <summary>AD-197l: solta a pose de quem já não viaja com perna (chegou, mudou de ação), olhando só quem tem pose.</summary>
    public void PrunePoses()
    {
        if (_posed.Count == 0) return;
        _unpose.Clear();
        foreach (var npc in _posed.Values)
            if (!npc.IsAlive || npc.CurrentAction != LivingWorld.Domain.Behavior.ActionType.Travel || npc.TravelLegTo is null)
                _unpose.Add(npc);
        foreach (var npc in _unpose) SetPose(npc, null);
    }

    public static AliveNpcIndex RebuildFrom(WorldState world)
    {
        var alive = world.Npcs.Where(n => n.IsAlive).OrderBy(n => n.Id.Value).ToList();
        return new AliveNpcIndex(alive);
    }

    public void OnBorn(Npc npc)
    {
        if (!npc.IsAlive) return;
        int i = _alive.BinarySearch(npc, Comparer<Npc>.Create((a, b) => a.Id.Value.CompareTo(b.Id.Value)));
        if (i < 0)
        {
            _alive.Insert(~i, npc);
            Track(npc);
            Version++;
        }
    }

    public void OnDied(Npc npc)
    {
        for (int i = 0; i < _alive.Count; i++)
        {
            if (_alive[i].Id == npc.Id)
            {
                _alive.RemoveAt(i);
                Untrack(npc);
                Version++;
                return;
            }
        }
    }

    private void Track(Npc npc)
    {
        AddToBucket(npc, npc.CurrentLocation);
        npc.LocationObserver = OnMoved;
        npc.HealthObserver = OnHealthChanged;
        if (npc.Health <= EndangeredHealth) EndangeredCount++;
    }

    private void Untrack(Npc npc)
    {
        SetPose(npc, null);
        RemoveFromBucket(npc, npc.CurrentLocation);
        npc.LocationObserver = null;
        npc.HealthObserver = null;
        if (npc.Health <= EndangeredHealth) EndangeredCount--;
    }

    private void OnHealthChanged(Npc npc, int previous)
    {
        bool was = previous <= EndangeredHealth;
        bool now = npc.Health <= EndangeredHealth;
        if (was != now) EndangeredCount += now ? 1 : -1;
    }

    private void OnMoved(Npc npc, CellCoord previous)
    {
        if (_pose.Count > 0 && _pose.Remove(npc.Id.Value, out var posed)) // chegou: a pose acaba
        {
            _posed.Remove(npc.Id.Value);
            RemoveFrom(_byPose, npc, posed);
        }
        RemoveFromBucket(npc, previous);
        AddToBucket(npc, npc.CurrentLocation);
    }

    private void AddToBucket(Npc npc, CellCoord cell) => AddTo(_byCell, npc, cell);

    private void RemoveFromBucket(Npc npc, CellCoord cell) => RemoveFrom(_byCell, npc, cell);

    private static void AddTo(Dictionary<CellCoord, List<Npc>> index, Npc npc, CellCoord cell)
    {
        if (!index.TryGetValue(cell, out var bucket))
            index[cell] = bucket = new List<Npc>(2);
        bucket.Add(npc);
    }

    private static void RemoveFrom(Dictionary<CellCoord, List<Npc>> index, Npc npc, CellCoord cell)
    {
        if (!index.TryGetValue(cell, out var bucket)) return;
        int i = bucket.IndexOf(npc);
        if (i < 0) return;
        // A ordem do balde não é especificada: trocar pelo último evita deslocar centenas de referências
        // (a fazenda concentra o turno inteiro numa célula).
        int last = bucket.Count - 1;
        bucket[i] = bucket[last];
        bucket.RemoveAt(last);
        if (bucket.Count == 0)
            index.Remove(cell);
    }
}
