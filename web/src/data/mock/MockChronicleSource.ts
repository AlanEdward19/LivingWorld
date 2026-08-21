import type { NarrativeProse } from "../contracts";
import type { ChronicleSource } from "../sources";

export class MockChronicleSource implements ChronicleSource {
  constructor(private readonly chronicles: ReadonlyMap<string, NarrativeProse>) {}

  async load(cityId: string): Promise<NarrativeProse> {
    return this.chronicles.get(cityId) ?? { prose: "sem registros ancorados para este período." };
  }
}
