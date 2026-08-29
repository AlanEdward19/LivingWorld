import { useEffect, useRef, useState, useSyncExternalStore, type MouseEvent as ReactMouseEvent } from "react";
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

/** Assentamento associado à rota atual, se houver (base do filtro People "Nearby"). */
function currentSettlementId(fixture: WorldFixture, nav: NavigationStore): string | undefined {
  const route = nav.current();
  switch (route.kind) {
    case "settlement":
      return route.id;
    case "household":
      return fixture.households.find((h) => h.id === route.id)?.settlementId;
    case "agent":
      return fixture.agents.find((a) => a.id === route.id)?.settlementId;
    default:
      return undefined;
  }
}

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

/** Duração da animação de saída (`.explorer-followed-row--removing` em tokens.css) — o `<li>`
 * some SUAVE antes de sair da lista de verdade, em vez de piscar/sumir instantâneo. */
const UNFOLLOW_ANIMATION_MS = 220;

function FollowedTab({ fixture, nav }: { fixture: WorldFixture; nav: NavigationStore }) {
  const followedIds = useSyncExternalStore(
    (listener) => followStore.subscribe(listener),
    () => followStore.followedIds(),
  );
  // Pedido do usuário 2026-08-27: animação ao remover um followed. `followStore.toggleFollow`
  // some da lista NA HORA (é a fonte da verdade) — pra dar tempo da transição CSS rodar, o `<li>`
  // continua renderizado por `UNFOLLOW_ANIMATION_MS` com a classe `--removing` aplicada, e só
  // então o toggle de verdade acontece.
  const [removingIds, setRemovingIds] = useState<Set<string>>(new Set());
  const timeoutsRef = useRef(new Map<string, ReturnType<typeof setTimeout>>());

  useEffect(() => {
    const timeouts = timeoutsRef.current;
    return () => {
      for (const timeout of timeouts.values()) clearTimeout(timeout);
    };
  }, []);

  function unfollowOnContextMenu(event: ReactMouseEvent, id: string) {
    event.preventDefault();
    if (timeoutsRef.current.has(id)) return;
    setRemovingIds((current) => new Set(current).add(id));
    const timeout = setTimeout(() => {
      followStore.toggleFollow(id);
      timeoutsRef.current.delete(id);
      setRemovingIds((current) => {
        const next = new Set(current);
        next.delete(id);
        return next;
      });
    }, UNFOLLOW_ANIMATION_MS);
    timeoutsRef.current.set(id, timeout);
  }

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
            <li key={id} className={removingIds.has(id) ? "explorer-followed-row explorer-followed-row--removing" : "explorer-followed-row"}>
              {/* Pedido do usuário 2026-08-26: com vários NPCs seguidos, clicar num nome já
               * seguido nesta lista deveria alternar QUAL DELES a câmera acompanha (só o
               * "último" ativado é rastreado, ver `followStore.activeFollowId`/AD-026) — sem
               * precisar tirar e pôr o follow de novo. */}
              <button
                type="button"
                title="Right-click to unfollow"
                onClick={() => {
                  followStore.activate(id);
                  nav.push({ kind: "agent", id });
                }}
                onContextMenu={(event) => unfollowOnContextMenu(event, id)}
              >
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
            <li key={id} className={removingIds.has(id) ? "explorer-followed-row explorer-followed-row--removing" : "explorer-followed-row"}>
              <button type="button" title="Right-click to unfollow" onClick={() => nav.push({ kind: "household", id })} onContextMenu={(event) => unfollowOnContextMenu(event, id)}>
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
            <li key={id} className={removingIds.has(id) ? "explorer-followed-row explorer-followed-row--removing" : "explorer-followed-row"}>
              <button type="button" title="Right-click to unfollow" onClick={() => nav.push({ kind: "settlement", id })} onContextMenu={(event) => unfollowOnContextMenu(event, id)}>
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
            <li key={id} className={removingIds.has(id) ? "explorer-followed-row explorer-followed-row--removing" : "explorer-followed-row"}>
              <button type="button" title="Right-click to unfollow" onClick={() => nav.push({ kind: "thread", id })} onContextMenu={(event) => unfollowOnContextMenu(event, id)}>
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
    <div data-testid="explorer-places">
      {fixture.regions.map((region) => (
        <div key={region.id}>
          <h3>{region.name}</h3>
          <ul>
            {fixture.settlements
              .filter((s) => s.regionId === region.id)
              .map((settlement) => (
                <li key={settlement.id}>
                  <button type="button" onClick={() => nav.push({ kind: "settlement", id: settlement.id })}>
                    {settlement.name}
                  </button>
                </li>
              ))}
          </ul>
        </div>
      ))}
    </div>
  );
}

type PeopleFilter = "all" | "nearby" | "notable" | "followed";

function PeopleTab({ fixture, nav }: { fixture: WorldFixture; nav: NavigationStore }) {
  const [filter, setFilter] = useState<PeopleFilter>("all");
  const followedIds = useSyncExternalStore(
    (listener) => followStore.subscribe(listener),
    () => followStore.followedIds(),
  );
  const route = useSyncExternalStore(
    (listener) => nav.subscribe(listener),
    () => nav.current(),
  );
  const nearbySettlementId = currentSettlementId(fixture, nav);

  let agents = fixture.agents;
  if (filter === "followed") agents = agents.filter((a) => followedIds.includes(a.id));
  else if (filter === "notable") agents = agents.filter((a) => a.notable);
  else if (filter === "nearby") agents = nearbySettlementId ? agents.filter((a) => a.settlementId === nearbySettlementId) : [];

  return (
    <div data-testid="explorer-people">
      <div data-testid="people-filter">
        <button type="button" aria-pressed={filter === "all"} onClick={() => setFilter("all")}>
          All
        </button>
        <button type="button" aria-pressed={filter === "nearby"} onClick={() => setFilter("nearby")} disabled={!nearbySettlementId} title={nearbySettlementId ? undefined : "Select a settlement, household or agent first"}>
          Nearby
        </button>
        <button type="button" aria-pressed={filter === "notable"} onClick={() => setFilter("notable")}>
          Notable
        </button>
        <button type="button" aria-pressed={filter === "followed"} onClick={() => setFilter("followed")}>
          Followed
        </button>
      </div>
      {filter === "nearby" && agents.length === 0 && <p>No one nearby.</p>}
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
      {/* referencia `route` só pra recalcular nearbySettlementId quando a navegação muda */}
      <span hidden>{route.kind}</span>
    </div>
  );
}

function OrganizationsTab({ fixture, nav }: { fixture: WorldFixture; nav: NavigationStore }) {
  if (fixture.organizations.length === 0) {
    return (
      <div data-testid="explorer-organizations">
        <p>No organizations in this world yet.</p>
      </div>
    );
  }

  return (
    <ul data-testid="explorer-organizations">
      {fixture.organizations.map((org) => (
        <li key={org.id}>
          <h3>{org.name}</h3>
          <p>{org.description}</p>
          <ul>
            {org.memberIds.map((memberId) => {
              const member = fixture.agents.find((a) => a.id === memberId);
              if (!member) return null;
              return (
                <li key={memberId}>
                  <button type="button" onClick={() => nav.push({ kind: "agent", id: memberId })}>
                    {member.name}
                  </button>
                </li>
              );
            })}
          </ul>
        </li>
      ))}
    </ul>
  );
}

/**
 * Explorer sidebar (doc §39-46) — navegação contextual por tabs.
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
        {activeTab === "organizations" && <OrganizationsTab fixture={fixture} nav={nav} />}
        {activeTab === "threads" && <StoryThreads fixture={fixture} nav={nav} />}
        {activeTab === "events" && <WorldFeed fixture={fixture} />}
      </div>
    </nav>
  );
}
