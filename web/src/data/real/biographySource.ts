import { fetchBiography } from "../../api";
import type { BiographySource } from "../sources";

export class RealBiographySource implements BiographySource {
  load(npcId: number) {
    return fetchBiography(npcId);
  }
}
