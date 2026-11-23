using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Cities.Buildings;

/// <summary>Edifício concluído de uma <see cref="City"/>. Mesmo molde de
/// <see cref="Workplace"/>: identidade + tipo do catálogo (<see cref="CityCatalog"/>). <see
/// cref="Position"/>/<see cref="Orientation"/> só existem para prédios
/// autorados no World Creator — <c>null</c> para os que o motor constrói durante a simulação
/// (<see cref="ConstructionSystem"/> ainda não escolhe posição/orientação, G4).</summary>
public sealed class Building(
    BuildingId id, CityId city, int buildingTypeId, long completedAtTick,
    CellCoord? position = null, int? orientation = null, long? clusterFoundingScheduledAtTick = null, int? croftCells = null)
{
    private static long s_layoutVersion = 1;

    /// <summary>Contador monotônico, de todo o processo, incrementado quando um prédio é criado ou muda de
    /// cidade — as únicas mudanças que alteram o conjunto de prédios de uma cidade. Serve só para invalidar
    /// caches derivados do layout (caixas de footprint, limites crescidos).</summary>
    public static long LayoutVersion => Volatile.Read(ref s_layoutVersion);

    private static long TouchLayout(long value)
    {
        Interlocked.Increment(ref s_layoutVersion);
        return value;
    }

    public BuildingId Id { get; } = id;
    public CityId City { get; private set; } = city;
    public int BuildingTypeId { get; } = buildingTypeId;
    public long CompletedAtTick { get; private set; } = TouchLayout(completedAtTick);

    /// <summary>AD-184: obra em curso neste lote (o canteiro: estacas, material e paredes subindo); nulo = prédio pronto. Enquanto
    /// não fica pronto, ninguém mora nem trabalha nele, mas o lote é dele. <see cref="CompletedAtTick"/> é o início da obra até
    /// <see cref="Complete"/>.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public long? UnderWork { get; set; }

    /// <summary>O canteiro foi desfeito (obra abandonada): o lote volta a ser livre (os caches de layout se invalidam).</summary>
    public static void TouchLayoutVersion() => Interlocked.Increment(ref s_layoutVersion);

    /// <summary>A obra terminou: o prédio passa a existir de verdade neste tick.</summary>
    public void Complete(long tick)
    {
        UnderWork = null;
        CompletedAtTick = tick;
        Interlocked.Increment(ref s_layoutVersion);
    }
    public CellCoord? Position { get; private set; } = position;
    public int? Orientation { get; private set; } = orientation;

    /// <summary>AD-192 (08d 170): o prédio derivado (sem posição gravada: snapshot antigo, obra sem lote) passa a ter o lote gravado
    /// — a posição em que já estava, a orientação com a porta para a rua e o quintal. Nada muda se já tem posição.</summary>
    public void RecordLot(CellCoord at, int orientationDegrees, int croft)
    {
        if (Position is not null) return;
        Position = at;
        Orientation = orientationDegrees;
        CroftCells = croft;
        Interlocked.Increment(ref s_layoutVersion);
    }

    /// <summary>AD-173: fundo do quintal (croft) que quem construiu escolheu, em células atrás da parede oposta à porta;
    /// 0 = sem quintal. Nulo nos prédios de antes e nos que o motor deriva (aí a escolha é recalculada pelo lugar).</summary>
    public int? CroftCells { get; private set; } = croftCells;

    /// <summary>Tick em que a fundação de um cluster de overflow contendo este prédio foi agendada
    /// — espelha <see cref="City.FoundingScheduledAtTick"/>.
    /// <c>null</c> enquanto nenhuma fundação estiver pendente; impede que o mesmo cluster seja
    /// agendado duas vezes.</summary>
    public long? ClusterFoundingScheduledAtTick { get; private set; } = clusterFoundingScheduledAtTick;

    public void MarkClusterFoundingScheduled(long tick) => ClusterFoundingScheduledAtTick = tick;

    /// <summary>Muda a cidade proprietária do prédio —
    /// espelha <see cref="Npc.JoinCity"/>/<see cref="Household.JoinCity"/>: usado quando um cluster
    /// de overflow funda uma cidade nova e este prédio é reatribuído a ela.</summary>
    public void JoinCity(CityId newCity)
    {
        City = newCity;
        Interlocked.Increment(ref s_layoutVersion);
    }
}
