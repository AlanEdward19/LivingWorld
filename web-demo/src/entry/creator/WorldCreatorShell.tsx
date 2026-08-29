import { useCallback, useEffect, useMemo, useReducer, useRef, useState } from "react";
import { useEntryServices } from "../EntryContext";
import { draftReducer, initDraftState, newDraft, SIZE_PRESETS } from "./draftState";
import { OverviewSection } from "./OverviewSection";
import { GeographySection } from "./GeographySection";
import { ReviewSection } from "./ReviewSection";
import { UnsavedDraftGuard } from "./UnsavedDraftGuard";
import { NotFoundScreen } from "../WorldNotFound";
import { GenerationView } from "../GenerationView";
import { StarfieldBackground } from "../StarfieldBackground";
import { PlanetScene, hueForWorldId } from "../PlanetScene";

type Section = "overview" | "geography" | "review";

type NavItem = { label: string; section?: Section; disabledReason?: string };
type NavGroup = { label: string; items: NavItem[] };

// Only Geography has a real backend field mapping today (Width/Height/RegionSize). Everything
// else here is either not yet built (doc's own "coming later", §32) or, for Climate/Resources,
// has no equivalent anywhere in the real engine at all — disabled rather than faked.
const NAV_GROUPS: NavGroup[] = [
  {
    label: "World",
    items: [
      { label: "Geography", section: "geography" },
      { label: "Climate", disabledReason: "No backend support yet" },
      { label: "Resources", disabledReason: "No backend support yet" },
    ],
  },
  {
    label: "Life",
    items: [
      { label: "Population", disabledReason: "Configured in Overview" },
      { label: "Biology", disabledReason: "Coming later" },
      { label: "Cultures", disabledReason: "Coming later" },
    ],
  },
  {
    label: "Society",
    items: [
      { label: "Settlements", disabledReason: "Coming later" },
      { label: "Economy", disabledReason: "Coming later" },
      { label: "Technology", disabledReason: "Coming later" },
      { label: "Social Rules", disabledReason: "Coming later" },
    ],
  },
  {
    label: "Extraordinary",
    items: [
      { label: "Powers", disabledReason: "Coming later" },
      { label: "Sources", disabledReason: "Coming later" },
      { label: "Social Response", disabledReason: "Coming later" },
    ],
  },
  {
    label: "Simulation",
    items: [
      { label: "History", disabledReason: "No backend support yet" },
      { label: "Detail", disabledReason: "Coming later" },
      { label: "Performance", disabledReason: "Coming later" },
    ],
  },
];

function newDraftId(): string {
  return typeof crypto !== "undefined" && "randomUUID" in crypto ? crypto.randomUUID() : `draft-${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

/** Doc §25-45 — creator shell: sections nav, preview/inspector area, draft lifecycle, review, generate. */
export function WorldCreatorShell({
  draftId,
  onNavigate,
}: {
  draftId?: string;
  onNavigate: (path: string) => void;
}) {
  const { drafts, worlds } = useEntryServices();
  const [loadState, setLoadState] = useState<"loading" | "ready" | "not-found">("loading");
  const [state, dispatch] = useReducer(draftReducer, undefined as any, () => initDraftState(newDraft(draftId ?? newDraftId())));
  const [section, setSection] = useState<Section>("overview");
  const [guardOpen, setGuardOpen] = useState(false);
  const [generating, setGenerating] = useState(false);
  const saveTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    let cancelled = false;
    if (!draftId) {
      setLoadState("ready");
      return;
    }
    drafts.getDraft(draftId).then((existing) => {
      if (cancelled) return;
      if (!existing) {
        setLoadState("not-found");
        return;
      }
      dispatch({ type: "load", draft: existing });
      setLoadState("ready");
    });
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [draftId]);

  // Doc §39 — debounced autosave, no per-save toast.
  useEffect(() => {
    if (loadState !== "ready" || !state.dirty) return;
    if (saveTimer.current) clearTimeout(saveTimer.current);
    saveTimer.current = setTimeout(() => {
      dispatch({ type: "mark-saving" });
      drafts.saveDraft(state.draft).then(() => dispatch({ type: "mark-saved" }));
    }, 500);
    return () => {
      if (saveTimer.current) clearTimeout(saveTimer.current);
    };
  }, [state.dirty, state.draft, loadState, drafts]);

  // Doc §37 — Ctrl/Cmd+Z / Shift+Z undo/redo, ignored while focus is in a text input.
  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      const target = event.target as HTMLElement | null;
      const inField = target && (target.tagName === "INPUT" || target.tagName === "TEXTAREA" || target.tagName === "SELECT");
      if (inField) return;
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "z") {
        event.preventDefault();
        dispatch({ type: event.shiftKey ? "redo" : "undo" });
      }
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  // Live planet preview — reacts to the actual draft, no separate "preview state" to fall out of
  // sync. Width feeds the sphere's size (min/max clamped to the smallest/largest size preset),
  // Extraordinary prevalence feeds the atmosphere glow, period+seed feeds the color.
  const world = state.draft.world;
  const preview = useMemo(() => {
    const minW = SIZE_PRESETS.Small.width;
    const maxW = SIZE_PRESETS.Huge.width;
    const sizeScale = 0.75 + (Math.min(maxW, Math.max(minW, world.width)) - minW) / (maxW - minW) * 0.55;
    const glowIntensity = world.extraordinaryEnabled ? 0.5 + (world.extraordinaryPrevalence / 100) * 1.5 : 0.35;
    return { sizeScale, glowIntensity, hueRotate: hueForWorldId(`${world.period}:${world.seed}`) };
  }, [world.width, world.extraordinaryEnabled, world.extraordinaryPrevalence, world.period, world.seed]);

  const leaveToMainMenu = useCallback(() => {
    if (state.dirty) {
      setGuardOpen(true);
      return;
    }
    onNavigate("/");
  }, [state.dirty, onNavigate]);

  if (loadState === "loading") return null;
  if (loadState === "not-found") return <NotFoundScreen kind="draft" onNavigate={onNavigate} />;

  if (generating) {
    return (
      <GenerationView
        draft={state.draft}
        onCancel={() => setGenerating(false)}
        onComplete={(result) => {
          drafts.deleteDraft(state.draft.id);
          worlds
            .addWorld({
              id: result.worldId,
              name: result.worldName,
              year: 1,
              season: "Spring",
              population: state.draft.world.initialPopulation,
              status: "active",
              lastOpenedAt: Date.now(),
            })
            .then(() => onNavigate(`/worlds/${result.worldId}`));
        }}
      />
    );
  }

  return (
    <div data-testid="world-creator-shell" className="entry-cosmos-bg">
      <StarfieldBackground />
      <header data-testid="creator-top-bar">
        <button type="button" data-testid="creator-back" onClick={leaveToMainMenu}>
          ← Main Menu
        </button>
        <span>Create World</span>
        <span data-testid="draft-status">
          {state.saveStatus === "saving" ? "Saving..." : state.saveStatus === "saved" ? "Saved" : ""}
        </span>
        <div data-testid="undo-redo">
          <button type="button" onClick={() => dispatch({ type: "undo" })} disabled={state.past.length === 0}>
            ↶
          </button>
          <button type="button" onClick={() => dispatch({ type: "redo" })} disabled={state.future.length === 0}>
            ↷
          </button>
        </div>
        <div data-testid="creator-mode">
          <button type="button" aria-pressed={state.draft.mode === "simple"} onClick={() => dispatch({ type: "set-mode", mode: "simple" })}>
            Simple
          </button>
          <button type="button" aria-pressed={state.draft.mode === "advanced"} onClick={() => dispatch({ type: "set-mode", mode: "advanced" })}>
            Advanced
          </button>
        </div>
        <button type="button" data-testid="creator-review" onClick={() => setSection("review")}>
          Review / Generate
        </button>
      </header>

      <div data-testid="creator-body">
        <nav data-testid="creator-nav" aria-label="Creator sections">
          <button type="button" aria-current={section === "overview"} onClick={() => setSection("overview")}>
            Overview
          </button>
          {NAV_GROUPS.map((group) => (
            <div key={group.label} data-testid={`creator-nav-group-${group.label.toLowerCase()}`}>
              <span>{group.label}</span>
              {group.items.map((item) =>
                item.section ? (
                  <button key={item.label} type="button" aria-current={section === item.section} onClick={() => setSection(item.section!)}>
                    {item.label}
                  </button>
                ) : (
                  <button key={item.label} type="button" disabled title={item.disabledReason}>
                    {item.label}
                  </button>
                ),
              )}
            </div>
          ))}
          <button type="button" aria-current={section === "review"} onClick={() => setSection("review")}>
            Review
          </button>
        </nav>

        <div data-testid="creator-preview">
          <PlanetScene
            variant={world.initialPopulation > 0 ? "inhabited" : "proto-world"}
            worldName={world.name || "Unnamed World"}
            hueRotate={preview.hueRotate}
            sizeScale={preview.sizeScale}
            glowIntensity={preview.glowIntensity}
          />
        </div>

        <div data-testid="creator-content">
          {section === "overview" && <OverviewSection draft={state.draft} dispatch={dispatch} />}
          {section === "geography" && <GeographySection draft={state.draft} dispatch={dispatch} />}
          {section === "review" && <ReviewSection draft={state.draft} />}
        </div>
      </div>

      {section === "review" && (
        <footer data-testid="creator-footer">
          <button
            type="button"
            data-testid="generate-world"
            disabled={state.draft.world.name.trim() === ""}
            onClick={() => setGenerating(true)}
          >
            Generate World
          </button>
        </footer>
      )}

      {guardOpen && (
        <UnsavedDraftGuard
          onSave={() => {
            drafts.saveDraft(state.draft).then(() => {
              setGuardOpen(false);
              onNavigate("/");
            });
          }}
          onDiscard={() => {
            drafts.deleteDraft(state.draft.id).then(() => {
              setGuardOpen(false);
              onNavigate("/");
            });
          }}
          onCancel={() => setGuardOpen(false)}
        />
      )}
    </div>
  );
}
