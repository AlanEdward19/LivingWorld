import type { WorldConfig, WorldDraft } from "../repository/types";

const MAX_ULONG = 18_446_744_073_709_551_615n;

/** Real backend seed is a `ulong` — draw uniformly from its full range via BigInt, not a
    JS-precision-losing `Math.random() * Number.MAX_SAFE_INTEGER`. */
function randomSeed(): string {
  const bytes = crypto.getRandomValues(new Uint32Array(2));
  const value = ((BigInt(bytes[0]) << 32n) | BigInt(bytes[1])) % (MAX_ULONG + 1n);
  return value.toString();
}

/** UI-only convenience — the real fields are Width/Height/RegionSize (`MapScenarioLoader`);
    `size` just fills in a sensible preset for them so Quick Create doesn't need three inputs. */
export const SIZE_PRESETS: Record<WorldConfig["size"], { width: number; height: number; regionSize: number }> = {
  Small: { width: 64, height: 64, regionSize: 8 },
  Medium: { width: 128, height: 128, regionSize: 16 },
  Large: { width: 256, height: 256, regionSize: 24 },
  Huge: { width: 512, height: 512, regionSize: 32 },
};

export function defaultWorldConfig(): WorldConfig {
  return {
    name: "",
    seed: randomSeed(),
    period: "Medieval",

    size: "Medium",
    ...SIZE_PRESETS.Medium,

    // Matches every shipped period's CostWeights exactly (scenarios/periods/*.json).
    costBase: 1.0,
    costAltitudeWeight: 0.5,
    terrainWeight1: 1.0,
    terrainWeight2: 1.5,
    terrainWeight3: 3.0,

    initialPopulation: 5_000,

    extraordinaryEnabled: true,
    extraordinaryPrevalence: 20,
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
  | { type: "update-fields"; values: Partial<WorldConfig> }
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

    // Size preset -> Width/Height/RegionSize in one undo step (three separate `update-field`
    // dispatches would fragment one logical change across three undo entries).
    case "update-fields": {
      const next = { ...state.draft.world };
      let changed = false;
      for (const key of Object.keys(action.values) as (keyof WorldConfig)[]) {
        if (state.draft.lockedFields.includes(key)) continue;
        (next as any)[key] = action.values[key];
        changed = true;
      }
      return changed ? withConfig(state, next) : state;
    }

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
