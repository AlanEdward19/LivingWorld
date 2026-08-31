import type { DraftRepository } from "./DraftRepository";
import type { WorldDraft } from "./types";

const STORAGE_KEY = "livingworld.drafts.v1";

// ponytail: doc prefers IndexedDB for drafts, but jsdom (this repo's test runner) has no
// IndexedDB without an extra fake-indexeddb dependency, and a draft is one small JSON object —
// localStorage covers it. Swap for a real IndexedDB/backend implementation behind this same
// interface if drafts grow large enough to need it.
export class LocalStorageDraftRepository implements DraftRepository {
  private readAll(): Record<string, WorldDraft> {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return {};
    try {
      return JSON.parse(raw) as Record<string, WorldDraft>;
    } catch {
      return {};
    }
  }

  private writeAll(drafts: Record<string, WorldDraft>): void {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(drafts));
  }

  async listDrafts(): Promise<WorldDraft[]> {
    return Object.values(this.readAll()).sort((a, b) => b.updatedAt.localeCompare(a.updatedAt));
  }

  async getDraft(id: string): Promise<WorldDraft | null> {
    return this.readAll()[id] ?? null;
  }

  async saveDraft(draft: WorldDraft): Promise<void> {
    const drafts = this.readAll();
    drafts[draft.id] = draft;
    this.writeAll(drafts);
  }

  async deleteDraft(id: string): Promise<void> {
    const drafts = this.readAll();
    delete drafts[id];
    this.writeAll(drafts);
  }
}
