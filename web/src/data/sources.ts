// Fase 15.1, T0: o seam de dado (design.md "Mock Adapter / Validação offline do frontend").
// Cada interface tem exatamente uma responsabilidade e é injetada por construtor em quem a
// consome (SimulationStore, TimeControls, ViewStore). `Mock*` (Estágio 1) e `Real*` (Estágio 3)
// implementam a mesma interface; nada além do composition root (`main.tsx`) sabe qual está viva.
import type { SpaceId } from "../map-engine/types";
import type { VisualSnapshotEnvelope } from "../types";
import type { ScopeTickDelta, SimulationStatus, SpatialPortalDto } from "./contracts";

export interface SnapshotSource {
  load(space: SpaceId): Promise<VisualSnapshotEnvelope<unknown>>;
}

export interface TickStreamSource {
  subscribe(space: SpaceId, onDelta: (delta: ScopeTickDelta) => void): () => void;
}

export interface TimeControlSource {
  pause(): Promise<void>;
  resume(): Promise<void>;
  setSpeed(ticksPerSecond: number): Promise<void>;
  step(): Promise<void>;
  status(): Promise<SimulationStatus>;
}

export interface PortalSource {
  portalsOf(space: SpaceId): SpatialPortalDto[];
}
