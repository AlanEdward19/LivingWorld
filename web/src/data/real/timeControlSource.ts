// Fase 15.1, T32: implementação real de `TimeControlSource` sobre os endpoints de
// `/simulation/*` (T1). `TimeControls.tsx` continua sem conhecer HTTP — nenhuma linha do
// componente muda, só o argumento no composition root (main.tsx).
import type { TimeControlSource } from "../sources";
import type { SimulationStatus } from "../contracts";
import {
  advanceSimulationYear,
  fetchSimulationStatus,
  pauseSimulation,
  resumeSimulation,
  setSimulationSpeed,
  stepSimulation,
} from "../../api";

export class RealTimeControlSource implements TimeControlSource {
  async pause(): Promise<void> {
    await pauseSimulation();
  }

  async resume(): Promise<void> {
    await resumeSimulation();
  }

  async setSpeed(ticksPerSecond: number): Promise<void> {
    await setSimulationSpeed(ticksPerSecond);
  }

  async step(): Promise<void> {
    await stepSimulation();
  }

  async advanceYear(): Promise<void> {
    await advanceSimulationYear();
  }

  async status(): Promise<SimulationStatus> {
    return fetchSimulationStatus();
  }
}
