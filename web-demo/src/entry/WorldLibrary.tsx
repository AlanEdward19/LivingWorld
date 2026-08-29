import { useEffect, useState } from "react";
import { useEntryServices } from "./EntryContext";
import { WorldCard } from "./WorldCard";
import type { WorldDraft, WorldSummary } from "./repository/types";

/** Doc §52-64 — "Your Worlds", not a save-file manager: search, Worlds/Drafts tabs, empty states. */
export function WorldLibrary({ onNavigate }: { onNavigate: (path: string) => void }) {
  const { worlds, drafts } = useEntryServices();
  const [worldList, setWorldList] = useState<WorldSummary[] | null>(null);
  const [draftList, setDraftList] = useState<WorldDraft[]>([]);
  const [tab, setTab] = useState<"worlds" | "drafts">("worlds");
  const [query, setQuery] = useState("");
  const [selectedId, setSelectedId] = useState<string | null>(null);

  useEffect(() => {
    Promise.all([worlds.listWorlds(), drafts.listDrafts()]).then(([w, d]) => {
      setWorldList(w);
      setDraftList(d);
    });
  }, [worlds, drafts]);

  if (worldList === null) return null;

  const filtered = worldList.filter((w) => w.name.toLowerCase().includes(query.trim().toLowerCase()));
  const noWorldsAtAll = worldList.length === 0;

  return (
    <div data-testid="world-library">
      <header data-testid="world-library-header">
        <button type="button" data-testid="library-back" onClick={() => onNavigate("/")}>
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

      {noWorldsAtAll ? (
        <div data-testid="world-library-empty">
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
        <>
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
                  onContinue={() => onNavigate(`/worlds/${w.id}`)}
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
        </>
      )}
    </div>
  );
}
