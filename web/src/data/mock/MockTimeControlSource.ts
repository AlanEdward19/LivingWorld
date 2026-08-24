// Fase 15.1, T0: implementação mock de `TimeControlSource` — mesma semântica que T1 declara
// para o endpoint real (`SimulationControlEndpoints`): `step()` só faz sentido pausado.
import type { TimeControlSource } from "../sources";
import type { SimulationStatus } from "../contracts";
import type { MockClock } from "./MockClock";

export class MockTimeControlSource implements TimeControlSource {
  constructor(private readonly clock: MockClock) {}

  async pause(): Promise<void> {
    this.clock.pause();
  }

  async resume(): Promise<void> {
    this.clock.resume();
  }

  async setSpeed(ticksPerSecond: number): Promise<void> {
    this.clock.setSpeed(ticksPerSecond);
  }

  async step(): Promise<void> {
    if (!this.clock.isPaused) {
      throw new Error("step requires the simulation to be paused");
    }
    this.clock.advanceOneTick();
  }

  async advanceYear(): Promise<void> {
    if (!this.clock.isPaused) {
      throw new Error("advanceYear requires the simulation to be paused");
    }
    this.clock.advanceOneYear();
  }

  async status(): Promise<SimulationStatus> {
    return this.clock.status();
  }
}
