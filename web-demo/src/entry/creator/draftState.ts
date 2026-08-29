import type { WorldConfig, WorldDraft } from "../repository/types";

function randomSeed(): string {
  const chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  const block = (len: number) => Array.from({ length: len }, () => chars[Math.floor(Math.random() * chars.length)]).join("");
  return `${block(4)}-${block(3)}-${block(3)}`;
}

export function defaultWorldConfig(): WorldConfig {
  return {
    name: "",
    seed: randomSeed(),
    size: "Medium",
    era: "Medieval",
    preset: "Balanced",
    historyLengthYears: 100,
    initialPopulation: 5_000,
    extraordinary: "Rare",

    oceanCoverage: 60,
    terrainStyle: "Varied",

    climateZone: "Temperate",
    seasonalIntensity: "Moderate",
    rainfall: "Moderate",

    mineralAbundance: "Balanced",
    fertility: "Moderate",
  };
}

export function newDraft(id: string): WorldDraft {
  const now = new Date().toISOString();
  return { id, mode: "simple", world: defaultWorldConfig(), lockedFields: [], createdAt: now, updatedAt: now };
}

/** Doc §35-36 — randomize respects locks; a locked field survives both per-field and global randomize. */
export function randomizeConfig(config: WorldConfig, lockedFields: string[]): WorldConfig {
  const fresh = defaultWorldConfig();
  const result = { ...config };
  for (const key of Object.keys(fresh) as (keyof WorldConfig)[]) {
    if (!lockedFields.includes(key)) (result as any)[key] = fresh[key];
  }
  return result;
}

export type DraftAction =
  | { type: "load"; draft: WorldDraft }
  | { type: "set-mode"; mode: WorldDraft["mode"] }
  | { type: "update-field"; field: keyof WorldConfig; value: WorldConfig[keyof WorldConfig] }
  | { type: "toggle-lock"; field: keyof WorldConfig }
  | { type: "randomize-field"; field: keyof WorldConfig }
  | { type: "randomize-all" }
  | { type: "undo" }
  | { type: "redo" }
  | { type: "mark-saving" }
  | { type: "mark-saved" };

export type DraftState = {
  draft: WorldDraft;
  past: WorldConfig[];
  future: WorldConfig[];
  dirty: boolean;
  saveStatus: "idle" | "saving" | "saved";
};

export function initDraftState(draft: WorldDraft): DraftState {
  return { draft, past: [], future: [], dirty: false, saveStatus: "idle" };
}

function withConfig(state: DraftState, config: WorldConfig): DraftState {
  return {
    ...state,
    past: [...state.past, state.draft.world],
    future: [],
    draft: { ...state.draft, world: config, updatedAt: new Date().toISOString() },
    dirty: true,
    saveStatus: "idle",
  };
}

export function draftReducer(state: DraftState, action: DraftAction): DraftState {
  switch (action.type) {
    case "load":
      return initDraftState(action.draft);

    // Doc §30-31 — Simple/Advanced is a view mode, never resets or discards draft values/history.
    case "set-mode":
      return { ...state, draft: { ...state.draft, mode: action.mode } };

    case "update-field":
      if (state.draft.lockedFields.includes(action.field)) return state;
      return withConfig(state, { ...state.draft.world, [action.field]: action.value });

    case "toggle-lock": {
      const locked = state.draft.lockedFields.includes(action.field)
        ? state.draft.lockedFields.filter((f) => f !== action.field)
        : [...state.draft.lockedFields, action.field];
      return { ...state, draft: { ...state.draft, lockedFields: locked } };
    }

    case "randomize-field": {
      if (state.draft.lockedFields.includes(action.field)) return state;
      const fresh = defaultWorldConfig();
      return withConfig(state, { ...state.draft.world, [action.field]: fresh[action.field] });
    }

    case "randomize-all":
      return withConfig(state, randomizeConfig(state.draft.world, state.draft.lockedFields));

    case "undo": {
      if (state.past.length === 0) return state;
      const previous = state.past[state.past.length - 1];
      return {
        ...state,
        past: state.past.slice(0, -1),
        future: [state.draft.world, ...state.future],
        draft: { ...state.draft, world: previous },
        dirty: true,
      };
    }

    case "redo": {
      if (state.future.length === 0) return state;
      const [next, ...rest] = state.future;
      return {
        ...state,
        past: [...state.past, state.draft.world],
        future: rest,
        draft: { ...state.draft, world: next },
        dirty: true,
      };
    }

    case "mark-saving":
      return { ...state, saveStatus: "saving" };

    case "mark-saved":
      return { ...state, saveStatus: "saved", dirty: false };

    default:
      return state;
  }
}
