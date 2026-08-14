import { fetchNpcInspection } from "../../api";
import type { NpcInspectionSource } from "../sources";

export class RealNpcInspectionSource implements NpcInspectionSource {
  load(npcId: number) {
    return fetchNpcInspection(npcId);
  }
}
