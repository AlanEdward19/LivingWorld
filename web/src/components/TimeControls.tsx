// Fase 15.1, T16: HUD de controle de tempo (design.md "Components" -> `TimeControls`; master
// prompt §20/§21). Recebe `TimeControlSource` por construtor (T0) — mock aqui, pausando/
// acelerando o `MockTickStreamSource` compartilhado; a troca pelos endpoints reais é a T32,
// sem nenhuma linha deste componente mudando. Não conhece `SimulationStore`/`SelectionStore`/
// `TickStreamSource` — trocar de velocidade não pode reassinar stream nem limpar seleção
// porque este componente simplesmente não tem como fazer isso.
import { useEffect, useState } from "react";
import type { SimulationStatus } from "../data/contracts";
import type { TimeControlSource } from "../data/sources";

export interface TimeControlsProps {
  timeControlSource: TimeControlSource;
}

const SPEEDS = [1, 2, 4, 8];

export function TimeControls({ timeControlSource }: TimeControlsProps) {
  const [status, setStatus] = useState<SimulationStatus | null>(null);

  useEffect(() => {
    let cancelled = false;
    timeControlSource.status().then((s) => {
      if (!cancelled) {
        setStatus(s);
      }
    });
    return () => {
      cancelled = true;
    };
  }, [timeControlSource]);

  async function refresh() {
    setStatus(await timeControlSource.status());
  }

  const isPaused = status?.isPaused ?? false;

  return (
    <div className="time-controls" data-testid="time-controls">
      <button
        type="button"
        onClick={async () => {
          await timeControlSource.pause();
          await refresh();
        }}
        disabled={isPaused}
      >
        Pause
      </button>
      <button
        type="button"
        onClick={async () => {
          await timeControlSource.resume();
          await refresh();
        }}
        disabled={!isPaused}
      >
        Resume
      </button>
      {SPEEDS.map((tps) => (
        <button
          key={tps}
          type="button"
          aria-pressed={status?.ticksPerSecond === tps}
          onClick={async () => {
            await timeControlSource.setSpeed(tps);
            await refresh();
          }}
        >
          {tps}x
        </button>
      ))}
      <button
        type="button"
        onClick={async () => {
          await timeControlSource.step();
          await refresh();
        }}
        disabled={!isPaused}
      >
        +1 tick
      </button>
      <span data-testid="time-controls-status">
        {isPaused ? "Pausado" : `${status?.ticksPerSecond ?? "…"}x`}
      </span>
    </div>
  );
}
