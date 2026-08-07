// Fase 15.1, T0: relógio compartilhado entre `MockTickStreamSource` e `MockTimeControlSource`
// — é isto que faz pause/resume/setSpeed do TimeControls (mock, hoje; real endpoint, T32) afetar
// o fluxo de deltas sem nenhum dos dois conhecer o outro diretamente.
import type { SimulationStatus } from "../contracts";

export class MockClock {
  private paused = false;
  private tps = 1;
  private currentTick = 0;

  get isPaused(): boolean {
    return this.paused;
  }

  get ticksPerSecond(): number {
    return this.tps;
  }

  get tick(): number {
    return this.currentTick;
  }

  pause(): void {
    this.paused = true;
  }

  resume(): void {
    this.paused = false;
  }

  setSpeed(ticksPerSecond: number): void {
    if (ticksPerSecond <= 0) {
      throw new Error("ticksPerSecond must be > 0");
    }
    this.tps = ticksPerSecond;
  }

  /** Avança exatamente um tick, independente de `paused` — quem decide a regra é o chamador. */
  advanceOneTick(): void {
    this.currentTick += 1;
  }

  status(): SimulationStatus {
    return { isPaused: this.paused, ticksPerSecond: this.tps, tick: this.currentTick };
  }
}
