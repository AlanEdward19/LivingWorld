import type { NpcInspection } from "../contracts";
import type { NpcInspectionSource } from "../sources";

export class MockNpcInspectionSource implements NpcInspectionSource {
  constructor(private readonly inspections: ReadonlyMap<number, NpcInspection>) {}

  async load(npcId: number): Promise<NpcInspection | null> {
    return this.inspections.get(npcId) ?? null;
  }
}
