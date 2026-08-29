import type { WorldConfig, WorldDraft } from "../repository/types";
import type { DraftAction } from "./draftState";

/** Shared lock/update/randomize binding for a single `WorldConfig` field — same shape every
    section (Overview, Geography, Climate, Resources, ...) wires its inputs through. */
export function createFieldBinder(draft: WorldDraft, dispatch: (action: DraftAction) => void) {
  return function field<K extends keyof WorldConfig>(key: K) {
    const locked = draft.lockedFields.includes(key);
    return {
      locked,
      update: (value: WorldConfig[K]) => dispatch({ type: "update-field", field: key, value }),
      toggleLock: () => dispatch({ type: "toggle-lock", field: key }),
      randomize: () => dispatch({ type: "randomize-field", field: key }),
    };
  };
}
