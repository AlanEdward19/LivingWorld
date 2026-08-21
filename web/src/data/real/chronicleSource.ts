import { fetchChronicle } from "../../api";
import type { ChronicleSource } from "../sources";

export class RealChronicleSource implements ChronicleSource {
  load(cityId: string, periodStart: number, periodEnd: number) {
    return fetchChronicle(cityId, periodStart, periodEnd);
  }
}
