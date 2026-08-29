import { useCallback, useEffect, useReducer, useRef, useState } from "react";
import { useEntryServices } from "../EntryContext";
import { draftReducer, initDraftState, newDraft } from "./draftState";
import { OverviewSection } from "./OverviewSection";
import { ReviewSection } from "./ReviewSection";
import { UnsavedDraftGuard } from "./UnsavedDraftGuard";
import { NotFoundScreen } from "../WorldNotFound";
import { GenerationView } from "../GenerationView";

type Section = "overview" | "review";

const COMING_LATER_GROUPS: { label: string; items: string[] }[] = [
  { label: "World", items: ["Geography", "Climate", "Resources"] },
  { label: "Life", items: ["Population", "Biology", "Cultures"] },
  { label: "Society", items: ["Settlements", "Economy", "Technology", "Social Rules"] },
  { label: "Extraordinary", items: ["Powers", "Sources", "Social Response"] },
  { label: "Simulation", items: ["History", "Detail", "Performance"] },
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
    <div data-testid="world-creator-shell">
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
          {COMING_LATER_GROUPS.map((group) => (
            <div key={group.label} data-testid={`creator-nav-group-${group.label.toLowerCase()}`}>
              <span>{group.label}</span>
              {group.items.map((item) => (
                <button key={item} type="button" disabled title="Coming later">
                  {item}
                </button>
              ))}
            </div>
          ))}
          <button type="button" aria-current={section === "review"} onClick={() => setSection("review")}>
            Review
          </button>
        </nav>

        <div data-testid="creator-content">
          {section === "overview" ? (
            <OverviewSection draft={state.draft} dispatch={dispatch} />
          ) : (
            <ReviewSection draft={state.draft} />
          )}
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
