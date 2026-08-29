import { useEffect, useState } from "react";
import { useEntryServices } from "./EntryContext";
import { WorldCard } from "./WorldCard";
import { PlanetScene, hueForWorldId } from "./PlanetScene";
import { usePlanetZoomExit } from "./usePlanetZoomExit";
import type { WorldDraft, WorldSummary } from "./repository/types";

/** Doc §52-64 — "Your Worlds", not a save-file manager: search, Worlds/Drafts tabs, empty states. */
export function WorldLibrary({ onNavigate }: { onNavigate: (path: string) => void }) {
  const { worlds, drafts } = useEntryServices();
  const { exiting, zoomInto } = usePlanetZoomExit(onNavigate);
  const [worldList, setWorldList] = useState<WorldSummary[] | null>(null);
  const [draftList, setDraftList] = useState<WorldDraft[]>([]);
  const [tab, setTab] = useState<"worlds" | "drafts">("worlds");
  const [query, setQuery] = useState("");
  const [selectedId, setSelectedId] = useState<string | null>(null);

  useEffect(() => {
    Promise.all([worlds.listWorlds(), drafts.listDrafts()]).then(([w, d]) => {
      setWorldList(w);
      setDraftList(d);
      // Same world the Main Menu's planet shows by default — the library shouldn't open on an
      // empty scene when there's an obvious "current" save.
      setSelectedId((current) => current ?? w[0]?.id ?? null);
    });
  }, [worlds, drafts]);

  if (worldList === null) return null;

  const filtered = worldList.filter((w) => w.name.toLowerCase().includes(query.trim().toLowerCase()));
  const noWorldsAtAll = worldList.length === 0;
  const selectedWorld = worldList.find((w) => w.id === selectedId) ?? null;

  return (
    <div data-testid="world-library" className={exiting ? "zoom-exit" : undefined}>
      {noWorldsAtAll ? (
        <div data-testid="world-library-empty">
          <header data-testid="world-library-header">
            <button type="button" data-testid="library-back" onClick={() => onNavigate("/")}>
              ← Main Menu
            </button>
            <h1>Your Worlds</h1>
          </header>
          {draftList.length > 0 ? (
            <>
              <p>No completed worlds yet.</p>
              <h2>Drafts</h2>
              <ul data-testid="draft-list">
                {draftList.map((d) => (
                  <li key={d.id}>
                    {d.world.name || "Untitled world"}
                    <button type="button" onClick={() => onNavigate(`/create/${d.id}`)}>
                      Continue Editing
                    </button>
                  </li>
                ))}
              </ul>
            </>
          ) : (
            <p>No worlds yet. Create your first living world.</p>
          )}
          <button type="button" onClick={() => onNavigate("/create")}>
            Create New World
          </button>
          <button type="button" onClick={() => onNavigate("/")}>
            Back to Main Menu
          </button>
        </div>
      ) : (
        <div className="planet-frame">
          <div data-testid="world-library-content">
            <header data-testid="world-library-header">
              <button type="button" data-testid="library-back" disabled={exiting} onClick={() => onNavigate("/")}>
                ← Main Menu
              </button>
              <h1>Your Worlds</h1>
              <input
                type="text"
                data-testid="world-search"
                placeholder="Search worlds..."
                value={query}
                onChange={(e) => setQuery(e.target.value)}
              />
            </header>

            <div data-testid="world-library-tabs" role="tablist">
              <button type="button" role="tab" aria-selected={tab === "worlds"} onClick={() => setTab("worlds")}>
                Worlds
              </button>
              <button type="button" role="tab" aria-selected={tab === "drafts"} onClick={() => setTab("drafts")}>
                Drafts
              </button>
            </div>

            {tab === "worlds" ? (
              <ul data-testid="world-list">
                {filtered.map((w) => (
                  <WorldCard
                    key={w.id}
                    world={w}
                    selected={selectedId === w.id}
                    onSelect={() => setSelectedId(w.id)}
                    onContinue={() => zoomInto(`/worlds/${w.id}`)}
                  />
                ))}
              </ul>
            ) : (
              <ul data-testid="draft-list">
                {draftList.map((d) => (
                  <li key={d.id}>
                    {d.world.name || "Untitled world"}
                    <button type="button" onClick={() => onNavigate(`/create/${d.id}`)}>
                      Continue Editing
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>

          <div className="planet-frame-scene">
            {selectedWorld && <PlanetScene variant="inhabited" worldName={selectedWorld.name} hueRotate={hueForWorldId(selectedWorld.id)} />}
          </div>
        </div>
      )}

      <div data-testid="zoom-blackout" className="zoom-blackout" />
    </div>
  );
}
