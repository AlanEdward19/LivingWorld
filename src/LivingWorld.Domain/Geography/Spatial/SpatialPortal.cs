using System.Text.Json.Serialization;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Geography.Spatial;

/// <summary>Espaço de origem/destino de um <see cref="PortalEndpoint"/> (Fase 15.1, T21). Anotado
/// com <see cref="JsonStringEnumConverter"/> diretamente no tipo — sem essa conversão o cliente
/// (que só entende os literais "World"/"City"/"Building" do contrato TS
/// <c>web/src/data/contracts.ts</c>) receberia um índice numérico.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PortalSpaceKind
{
    World,
    City,
    Building,
}

/// <summary>Um lado (origem ou destino) de um <see cref="SpatialPortal"/>: o espaço, a referência
/// dentro dele (vazia para <see cref="PortalSpaceKind.World"/>, que é único) e a célula local.</summary>
public sealed record PortalEndpoint(PortalSpaceKind Space, string RefId, CellCoord Cell);

/// <summary>Entrada/saída nomeada de um espaço, como dado canônico de domínio (Fase 15.1, T21,
/// OQ-2 — spec.md "SpatialPortal como conceito canônico de domínio"). Mesmo molde declarativo de
/// <see cref="SettlementAnchor"/> (<see cref="MapCell"/>): âncora nomeada, sem comportamento.
/// <see cref="Id"/> é string autorada pelo cenário (não um contador do <c>WorldState</c>, ao
/// contrário de <see cref="BuildingId"/>/<see cref="WorkplaceId"/>) — mesma razão de
/// <see cref="SettlementAnchor.Id"/>: o portal é dado descritivo de cenário, não uma entidade que
/// o motor cria em runtime.</summary>
public sealed record SpatialPortal(string Id, string Label, PortalEndpoint From, PortalEndpoint To);
