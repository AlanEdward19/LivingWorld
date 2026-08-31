import type { WorldGenerationService } from "./WorldGenerationService";
import type { GenerationEvent, GenerationResult, GenerationStage, WorldDraft } from "./types";

/** Doc §48 stage copy, but the stages themselves are the real backend pipeline order
    (`PeriodDefinitionValidator.Validate` -> `ScenarioLoaderV2.LoadWorld`) — not a claim that
    this mock actually ran them, just the real shape instead of invented flavor text. */
const STAGES: { stage: GenerationStage; progress: number; message: string }[] = [
  { stage: "validating", progress: 8, message: "Validating configuration..." },
  { stage: "loading-map", progress: 25, message: "Loading the map..." },
  { stage: "seeding-population", progress: 45, message: "Seeding initial population..." },
  { stage: "configuring-behavior-economy", progress: 60, message: "Configuring behavior and economy..." },
  { stage: "founding-cities", progress: 78, message: "Founding cities and buildings..." },
  { stage: "wiring-portals", progress: 88, message: "Wiring portals..." },
  { stage: "seeding-extraordinary", progress: 96, message: "Seeding the extraordinary..." },
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
