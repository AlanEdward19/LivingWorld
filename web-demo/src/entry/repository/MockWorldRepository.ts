import type { WorldRepository } from "./WorldRepository";
import type { WorldSummary } from "./types";

const now = 1_735_000_000_000; // fixed reference instant — deterministic "last opened" ordering in mocks/tests

/** Doc §82 test cases: 2 = one world, many worlds, long name, paused vs active. */
const MOCK_WORLDS: WorldSummary[] = [
  { id: "eldoria", name: "Eldoria", year: 328, season: "Spring", population: 23_481, status: "active", lastOpenedAt: now },
  { id: "mars-2149", name: "Mars 2149", year: 17, season: "Winter", population: 1_204, status: "paused", lastOpenedAt: now - 3_600_000 },
  {
    id: "first-age",
    name: "The First Age of the Sundered Kingdoms",
    year: 1,
    season: "Summer",
    population: 340,
    status: "active",
    lastOpenedAt: now - 86_400_000,
  },
];

export class MockWorldRepository implements WorldRepository {
  private worlds: WorldSummary[];

  constructor(seed: WorldSummary[] = MOCK_WORLDS) {
    this.worlds = seed;
  }

  async listWorlds(): Promise<WorldSummary[]> {
    return [...this.worlds].sort((a, b) => b.lastOpenedAt - a.lastOpenedAt);
  }

  async getWorld(id: string): Promise<WorldSummary | null> {
    return this.worlds.find((w) => w.id === id) ?? null;
  }

  async addWorld(world: WorldSummary): Promise<void> {
    this.worlds = [...this.worlds.filter((w) => w.id !== world.id), world];
  }
}

/** Empty-state mock (doc §82 case 1) — used by tests exercising the first-launch flow. */
export class EmptyMockWorldRepository extends MockWorldRepository {
  constructor() {
    super([]);
  }
}
