import { useState } from "react";
import type { WorldFixture } from "../fixture/types";
import { TILE_HEIGHT, TILE_WIDTH } from "./IsoTileRenderer";
import { toScreen } from "./IsoProjection";
import { SETTLEMENT_PALETTE } from "./isoPalette";

export type ZoomLevel = "world" | "district" | "agent";

export interface SemanticZoomMapProps {
  fixture: WorldFixture;
  onSelectSettlement: (settlementId: string) => void;
  onSelectNpc: (agentId: string) => void;
}

/**
 * Mapa com zoom semântico — cada nível troca DENSIDADE de informação renderizada, não só
 * escala (spec P1b AC1-3). Nível "mundo" (este componente, T9): só assentamentos/rótulos,
 * nenhum prédio (`IsoTile`) nem NPC (`NpcToken`) individual visível.
 */
export function SemanticZoomMap({ fixture, onSelectSettlement }: SemanticZoomMapProps) {
  const [zoomLevel] = useState<ZoomLevel>("world");

  if (zoomLevel === "world") {
    return (
      <svg data-testid="semantic-zoom-map" data-zoom-level="world" viewBox="0 0 800 600">
        {fixture.settlements.map((settlement) => {
          const { x, y } = toScreen(settlement.gridPosition.x, settlement.gridPosition.y, TILE_WIDTH, TILE_HEIGHT);
          return (
            <g
              key={settlement.id}
              data-testid="settlement-marker"
              onClick={() => onSelectSettlement(settlement.id)}
              style={{ cursor: "pointer" }}
            >
              <circle cx={x} cy={y} r={16} fill={SETTLEMENT_PALETTE.top} stroke={SETTLEMENT_PALETTE.right} strokeWidth={2} />
              <text x={x} y={y + 30} textAnchor="middle" fontSize={12}>
                {settlement.name}
              </text>
            </g>
          );
        })}
      </svg>
    );
  }

  return null;
}
