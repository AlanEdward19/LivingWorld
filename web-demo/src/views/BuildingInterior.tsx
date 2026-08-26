import { useState } from "react";
import type { WorldFixture } from "../fixture/types";
import type { NavigationStore } from "../nav/NavigationStore";
import { NpcToken } from "../npc/NpcToken";

export interface BuildingInteriorProps {
  fixture: WorldFixture;
  nav: NavigationStore;
  buildingId: string;
}

const CELL = 40;

/**
 * Interior de um prédio (doc §29-36/§58-60) — DEPARTURE deliberada do exterior isométrico:
 * em vez de um "roof cutaway" isométrico (efeito 3D-ish caro), o interior é uma vista
 * top-down 2D separada, no estilo RimWorld real (que também é ortogonal top-down, não
 * isométrico) — troca de view completa, como o Causal Explorer substituindo o mapa, não um
 * fade sobre o exterior. Ver README/checklist pra essa simplificação disclosed.
 */
export function BuildingInterior({ fixture, nav, buildingId }: BuildingInteriorProps) {
  const building = fixture.settlements.flatMap((s) => s.buildings).find((b) => b.id === buildingId);
  const [floorIndex, setFloorIndex] = useState(0);

  if (!building || building.floors.length === 0) return <div data-testid="building-interior-empty" />;

  const floor = building.floors[floorIndex];
  const occupants = fixture.agents.filter((a) => a.indoorLocation?.buildingId === buildingId && a.indoorLocation.floorId === floor.id);
  const width = Math.max(...floor.rooms.map((r) => r.bounds.x + r.bounds.width), 6) * CELL;
  const height = Math.max(...floor.rooms.map((r) => r.bounds.y + r.bounds.height), 6) * CELL;

  return (
    <div data-testid="building-interior">
      <h1>{building.name}</h1>

      {building.floors.length > 1 && (
        <div data-testid="floor-selector">
          {building.floors.map((f, index) => (
            <button key={f.id} type="button" aria-pressed={index === floorIndex} onClick={() => setFloorIndex(index)}>
              {f.label}
            </button>
          ))}
        </div>
      )}

      <svg data-testid="building-interior-svg" width={width} height={height} viewBox={`0 0 ${width} ${height}`}>
        {floor.rooms.map((room) => (
          <g key={room.id} data-testid="interior-room">
            <rect
              x={room.bounds.x * CELL}
              y={room.bounds.y * CELL}
              width={room.bounds.width * CELL}
              height={room.bounds.height * CELL}
              fill="#e4ded0"
              stroke="#3a352c"
              strokeWidth={3}
            />
            <text x={room.bounds.x * CELL + 6} y={room.bounds.y * CELL + 16} fontSize={12}>
              {room.name}
            </text>
            {room.furniture.map((item) => (
              <rect
                key={item.id}
                data-testid="interior-furniture"
                data-furniture-kind={item.kind}
                x={item.gridPosition.x * CELL + 6}
                y={item.gridPosition.y * CELL + 6}
                width={CELL - 12}
                height={CELL - 12}
                fill="#8a6f4e"
              />
            ))}
          </g>
        ))}
        {occupants.map((agent) => (
          <foreignObject
            key={agent.id}
            data-testid="interior-npc"
            x={agent.indoorLocation!.position.x * CELL}
            y={agent.indoorLocation!.position.y * CELL}
            width={32}
            height={38}
            onClick={() => nav.push({ kind: "agent", id: agent.id })}
            role="button"
            tabIndex={0}
            aria-label={`Open ${agent.name}`}
            style={{ cursor: "pointer" }}
          >
            <NpcToken id={agent.id} size={32} />
          </foreignObject>
        ))}
      </svg>

      <ul data-testid="people-inside">
        {occupants.map((agent) => (
          <li key={agent.id}>
            <button type="button" onClick={() => nav.push({ kind: "agent", id: agent.id })}>
              {agent.name}
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
