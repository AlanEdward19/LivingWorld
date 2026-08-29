import type { WorldGenerationService } from "./WorldGenerationService";
import type { GenerationEvent, GenerationResult, GenerationStage, WorldDraft } from "./types";

/** Doc §48 stage copy — visual contract for real backend stages, not a claim that they ran. */
const STAGES: { stage: GenerationStage; progress: number; message: string }[] = [
  { stage: "validating", progress: 5, message: "Validating configuration..." },
  { stage: "forming-terrain", progress: 20, message: "Forming terrain..." },
  { stage: "shaping-climate", progress: 35, message: "Shaping climate..." },
  { stage: "carving-rivers", progress: 50, message: "Carving rivers..." },
  { stage: "growing-ecosystems", progress: 65, message: "Growing ecosystems..." },
  { stage: "founding-settlements", progress: 80, message: "Founding settlements..." },
  { stage: "populating", progress: 90, message: "Populating the world..." },
  { stage: "simulating-history", progress: 97, message: "Advancing history..." },
  { stage: "complete", progress: 100, message: "The world is ready." },
];

function toWorldId(name: string): string {
  const slug = name
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/(^-|-$)/g, "");
  return slug || `world-${Date.now()}`;
}

export class MockWorldGenerationService implements WorldGenerationService {
  constructor(private readonly stageDelayMs = 220) {}

  async generate(draft: WorldDraft, onEvent: (event: GenerationEvent) => void, signal: AbortSignal): Promise<GenerationResult> {
    for (const step of STAGES) {
      if (signal.aborted) throw new DOMException("Generation cancelled", "AbortError");
      await new Promise<void>((resolve, reject) => {
        const timeout = setTimeout(resolve, this.stageDelayMs);
        signal.addEventListener("abort", () => {
          clearTimeout(timeout);
          reject(new DOMException("Generation cancelled", "AbortError"));
        });
      });
      onEvent({ ...step, timestamp: Date.now() });
    }
    return { worldId: toWorldId(draft.world.name), worldName: draft.world.name };
  }
}
