import { useEffect, useState } from "react";
import { useEntryServices } from "./EntryContext";
import { PlanetScene } from "./PlanetScene";
import { StarfieldBackground } from "./StarfieldBackground";
import { usePlanetZoomExit } from "./usePlanetZoomExit";
import type { WorldDraft, WorldSummary } from "./repository/types";

function timeAgo(updatedAt: string): string {
  const minutes = Math.max(1, Math.round((Date.now() - new Date(updatedAt).getTime()) / 60_000));
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.round(minutes / 60);
  return `${hours}h ago`;
}

/** Doc §9-21 — the Main Menu, always mounted at `/` (doc §2), never auto-redirects. */
export function MainMenu({ onNavigate }: { onNavigate: (path: string) => void }) {
  const { worlds, drafts } = useEntryServices();
  const { exiting, zoomInto } = usePlanetZoomExit(onNavigate);
  const [worldList, setWorldList] = useState<WorldSummary[] | null>(null);
  const [draftList, setDraftList] = useState<WorldDraft[]>([]);
  const [loadError, setLoadError] = useState(false);

  useEffect(() => {
    let cancelled = false;
    Promise.all([worlds.listWorlds(), drafts.listDrafts()])
      .then(([w, d]) => {
        if (cancelled) return;
        setWorldList(w);
        setDraftList(d);
      })
      .catch(() => {
        if (!cancelled) setLoadError(true);
      });
    return () => {
      cancelled = true;
    };
  }, [worlds, drafts]);

  if (loadError) {
    return (
      <div data-testid="main-menu-error">
        <p>Could not load your worlds.</p>
        <button type="button" onClick={() => window.location.reload()}>
          Try Again
        </button>
      </div>
    );
  }

  const loading = worldList === null;
  const hasWorlds = !loading && worldList!.length > 0;
  const recentWorld = hasWorlds ? worldList![0] : null;
  const recentDraft = draftList[0];

  return (
    <div data-testid="main-menu" className={`entry-cosmos-bg${exiting ? " zoom-exit" : ""}`}>
      <StarfieldBackground />
      <div className="planet-frame">
        <div data-testid="main-menu-panel">
          <div data-testid="main-menu-identity">
            <h1>LivingWorld</h1>
            <p>A world that keeps living.</p>
          </div>

          <nav data-testid="main-menu-actions" aria-label="Main menu">
            <button type="button" data-testid="action-create" disabled={loading || exiting} onClick={() => onNavigate("/create")}>
              <span>Create New World</span>
              <small>Build a new living universe</small>
            </button>

            {recentWorld ? (
              <button type="button" data-testid="action-continue" disabled={exiting} onClick={() => zoomInto(`/worlds/${recentWorld.id}`)}>
                <span>Continue {recentWorld.name}</span>
                <small>
                  Year {recentWorld.year} · {recentWorld.season}
                </small>
              </button>
            ) : (
              <button type="button" data-testid="action-continue" disabled>
                <span>Continue</span>
                <small>{loading ? "Loading..." : "No worlds yet"}</small>
              </button>
            )}

            {hasWorlds && (
              <button type="button" data-testid="action-browse-worlds" disabled={exiting} onClick={() => onNavigate("/worlds")}>
                Browse Worlds
              </button>
            )}

            {recentDraft && (
              <button
                type="button"
                data-testid="action-continue-draft"
                disabled={exiting}
                onClick={() => onNavigate(`/create/${recentDraft.id}`)}
              >
                <span>Continue Draft</span>
                <small>
                  {recentDraft.world.name || "Untitled world"} · Edited {timeAgo(recentDraft.updatedAt)}
                </small>
              </button>
            )}

            <button type="button" data-testid="action-settings" disabled={exiting} onClick={() => onNavigate("/settings")}>
              Settings
            </button>
          </nav>

          <div data-testid="main-menu-version">v0.x</div>
        </div>

        <div className="planet-frame-scene">
          <PlanetScene variant={recentWorld ? "inhabited" : "proto-world"} worldName={recentWorld?.name} />
        </div>
      </div>

      <div data-testid="zoom-blackout" className="zoom-blackout" />
    </div>
  );
}
