import type { NarrativeProse } from "../contracts";
import type { BiographySource } from "../sources";

export class MockBiographySource implements BiographySource {
  constructor(private readonly biographies: ReadonlyMap<number, NarrativeProse>) {}

  async load(npcId: number): Promise<NarrativeProse | null> {
    return this.biographies.get(npcId) ?? null;
  }
}
