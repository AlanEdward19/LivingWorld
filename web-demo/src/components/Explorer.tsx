import { useState, useSyncExternalStore } from "react";
import type { WorldFixture } from "../fixture/types";
import type { NavigationStore } from "../nav/NavigationStore";
import { followStore } from "../state/followStore";
import { StoryThreads } from "../views/StoryThreads";
import { WorldFeed } from "../views/WorldFeed";

export interface ExplorerProps {
  fixture: WorldFixture;
  nav: NavigationStore;
}

type ExplorerTab = "overview" | "followed" | "places" | "people" | "organizations" | "threads" | "events";

const TABS: { id: ExplorerTab; label: string }[] = [
  { id: "overview", label: "Overview" },
  { id: "followed", label: "Followed" },
  { id: "places", label: "Places" },
  { id: "people", label: "People" },
  { id: "organizations", label: "Organizations" },
  { id: "threads", label: "Threads" },
  { id: "events", label: "Events" },
];

function OverviewTab({ fixture, nav }: { fixture: WorldFixture; nav: NavigationStore }) {
  const population = fixture.settlements.reduce((sum, s) => sum + s.population, 0);
  const migrationActive = fixture.settlements.filter((s) => s.migration !== "stable").length;
  // "Notable" derivado do fixture (doc §76 nota: "sem inventar métricas inexistentes") — eventos
  // que pertencem a algum Story Thread, não um contador de "conflitos" que o fixture não modela.
  const notableEventIds = new Set(fixture.storyThreads.flatMap((t) => t.eventIds));
  const recent = fixture.events.slice(-3).reverse();

  return (
    <div data-testid="explorer-overview">
      <h3>World Pulse</h3>
      <dl>
        <div>
          <dt>Population</dt>
          <dd>{population}</dd>
        </div>
        <div>
          <dt>Settlements</dt>
          <dd>{fixture.settlements.length}</dd>
        </div>
        <div>
          <dt>Migration</dt>
          <dd>{migrationActive > 0 ? "Active" : "Stable"}</dd>
        </div>
        <div>
          <dt>Notable events</dt>
          <dd>{notableEventIds.size}</dd>
        </div>
      </dl>

      <h3>Recent</h3>
      <ul>
        {recent.map((event) => (
          <li key={event.eventId}>
            <button type="button" onClick={() => nav.push({ kind: "causal", eventId: event.eventId })}>
              {event.summary}
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}

function FollowedTab({ fixture, nav }: { fixture: WorldFixture; nav: NavigationStore }) {
  const followedIds = useSyncExternalStore(
    (listener) => followStore.subscribe(listener),
    () => followStore.followedIds(),
  );

  if (followedIds.length === 0) {
    return (
      <div data-testid="explorer-followed">
        <p>Nothing followed yet.</p>
      </div>
    );
  }

  return (
    <ul data-testid="explorer-followed">
      {followedIds.map((id) => {
        const agent = fixture.agents.find((a) => a.id === id);
        if (agent) {
          return (
            <li key={id}>
              <button type="button" onClick={() => nav.push({ kind: "agent", id })}>
                {agent.name}
                <br />
                <small>
                  {agent.profession} · {agent.currentIntent}
                </small>
              </button>
            </li>
          );
        }
        const household = fixture.households.find((h) => h.id === id);
        if (household) {
          return (
            <li key={id}>
              <button type="button" onClick={() => nav.push({ kind: "household", id })}>
                {household.name}
                <br />
                <small>{household.memberIds.length} members</small>
              </button>
            </li>
          );
        }
        const settlement = fixture.settlements.find((s) => s.id === id);
        if (settlement) {
          return (
            <li key={id}>
              <button type="button" onClick={() => nav.push({ kind: "settlement", id })}>
                {settlement.name}
                <br />
                <small>Population {settlement.population}</small>
              </button>
            </li>
          );
        }
        const thread = fixture.storyThreads.find((t) => t.id === id);
        if (thread) {
          return (
            <li key={id}>
              <button type="button" onClick={() => nav.push({ kind: "thread", id })}>
                {thread.title}
              </button>
            </li>
          );
        }
        return null;
      })}
    </ul>
  );
}

function PlacesTab({ fixture, nav }: { fixture: WorldFixture; nav: NavigationStore }) {
  return (
    <ul data-testid="explorer-places">
      {fixture.settlements.map((settlement) => (
        <li key={settlement.id}>
          <button type="button" onClick={() => nav.push({ kind: "settlement", id: settlement.id })}>
            {settlement.name}
          </button>
        </li>
      ))}
    </ul>
  );
}

function PeopleTab({ fixture, nav }: { fixture: WorldFixture; nav: NavigationStore }) {
  const [filter, setFilter] = useState<"all" | "followed">("all");
  const followedIds = useSyncExternalStore(
    (listener) => followStore.subscribe(listener),
    () => followStore.followedIds(),
  );

  const agents = filter === "all" ? fixture.agents : fixture.agents.filter((a) => followedIds.includes(a.id));

  return (
    <div data-testid="explorer-people">
      <div data-testid="people-filter">
        <button type="button" aria-pressed={filter === "all"} onClick={() => setFilter("all")}>
          All
        </button>
        <button type="button" aria-pressed={filter === "followed"} onClick={() => setFilter("followed")}>
          Followed
        </button>
      </div>
      <ul>
        {agents.map((agent) => (
          <li key={agent.id}>
            <button type="button" onClick={() => nav.push({ kind: "agent", id: agent.id })}>
              {agent.name}
              <br />
              <small>
                {agent.age} · {agent.profession}
              </small>
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}

/**
 * Explorer sidebar (doc §39-46) — navegação contextual por tabs. "Organizations" mostra estado
 * vazio explícito (doc §144) porque o fixture não modela facções/organizações — inventar uma
 * lista fake violaria o mesmo princípio de "sem métricas inexistentes" do §76.
 */
export function Explorer({ fixture, nav }: ExplorerProps) {
  const [activeTab, setActiveTab] = useState<ExplorerTab>("overview");

  return (
    <nav data-testid="explorer" aria-label="Explorer">
      <div data-testid="explorer-tabs" role="tablist">
        {TABS.map((tab) => (
          <button key={tab.id} type="button" role="tab" aria-selected={activeTab === tab.id} onClick={() => setActiveTab(tab.id)}>
            {tab.label}
          </button>
        ))}
      </div>

      <div data-testid="explorer-content">
        {activeTab === "overview" && <OverviewTab fixture={fixture} nav={nav} />}
        {activeTab === "followed" && <FollowedTab fixture={fixture} nav={nav} />}
        {activeTab === "places" && <PlacesTab fixture={fixture} nav={nav} />}
        {activeTab === "people" && <PeopleTab fixture={fixture} nav={nav} />}
        {activeTab === "organizations" && (
          <div data-testid="explorer-organizations">
            <p>No organizations in this world yet.</p>
          </div>
        )}
        {activeTab === "threads" && <StoryThreads fixture={fixture} nav={nav} />}
        {activeTab === "events" && <WorldFeed fixture={fixture} />}
      </div>
    </nav>
  );
}
