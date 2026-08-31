import type { WorldDraft } from "./types";

export interface DraftRepository {
  listDrafts(): Promise<WorldDraft[]>;
  getDraft(id: string): Promise<WorldDraft | null>;
  saveDraft(draft: WorldDraft): Promise<void>;
  deleteDraft(id: string): Promise<void>;
}
