import type { GenerationEvent, GenerationResult, WorldDraft } from "./types";

export interface WorldGenerationService {
  generate(draft: WorldDraft, onEvent: (event: GenerationEvent) => void, signal: AbortSignal): Promise<GenerationResult>;
}
