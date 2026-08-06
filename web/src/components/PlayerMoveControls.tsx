import { useEffect } from "react";
import { moveNpc } from "../api";
import type { CitySnapshot } from "../types";

export interface PlayerMoveControlsProps {
  snapshot: CitySnapshot;
  playerNpcId: number;
}

const KEY_TO_DELTA: Record<string, readonly [number, number]> = {
  w: [0, -1],
  ArrowUp: [0, -1],
  s: [0, 1],
  ArrowDown: [0, 1],
  a: [-1, 0],
  ArrowLeft: [-1, 0],
  d: [1, 0],
  ArrowRight: [1, 0],
};

/// Fase 15, T8 fix (VTT-07 AC1, spec.md "Modo personagem com FOW"): move via clique (botões
/// direcionais) ou WASD/setas — a validação server-side e o delta publicado já existem (T7);
/// isto só faltava chamar moveNpc() a partir de uma ação do usuário, que nenhum componente fazia.
export function PlayerMoveControls({ snapshot, playerNpcId }: PlayerMoveControlsProps) {
  const self = snapshot.residents.find((r) => r.id.value === playerNpcId);

  function move(dx: number, dy: number, inputMode: "click" | "wasd") {
    if (!self) return;
    void moveNpc(playerNpcId, {
      targetX: self.location.x + dx,
      targetY: self.location.y + dy,
      inputMode,
    });
  }

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      const delta = KEY_TO_DELTA[event.key];
      if (delta) move(delta[0], delta[1], "wasd");
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  });

  if (!self) return <p role="note">Seu personagem não está visível nesta cidade.</p>;

  return (
    <div data-testid="player-move-controls">
      <p>Mover (WASD/setas ou clique):</p>
      <button type="button" aria-label="mover-cima" onClick={() => move(0, -1, "click")}>
        ▲
      </button>
      <button type="button" aria-label="mover-esquerda" onClick={() => move(-1, 0, "click")}>
        ◀
      </button>
      <button type="button" aria-label="mover-direita" onClick={() => move(1, 0, "click")}>
        ▶
      </button>
      <button type="button" aria-label="mover-baixo" onClick={() => move(0, 1, "click")}>
        ▼
      </button>
    </div>
  );
}
