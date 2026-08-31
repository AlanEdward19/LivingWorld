import type { WorldSummary } from "./types";

export interface WorldRepository {
  listWorlds(): Promise<WorldSummary[]>;
  getWorld(id: string): Promise<WorldSummary | null>;
  /** Registers a freshly-generated world so it shows up in the library/Continue immediately. */
  addWorld(world: WorldSummary): Promise<void>;
}
