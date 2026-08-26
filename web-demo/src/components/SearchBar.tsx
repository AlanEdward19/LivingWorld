import { useState } from "react";
import type { WorldFixture } from "../fixture/types";
import type { NavigationStore } from "../nav/NavigationStore";
import { search } from "../search/SearchIndex";

export interface SearchBarProps {
  fixture: WorldFixture;
  nav: NavigationStore;
}

/**
 * Campo de busca global, resultados agrupados por People/Places/Households/Events/Threads
 * (doc#138). Clique num resultado navega direto pra entidade.
 */
export function SearchBar({ fixture, nav }: SearchBarProps) {
  const [query, setQuery] = useState("");
  const results = search(query, fixture);
  const hasQuery = query.trim().length > 0;

  return (
    <div data-testid="search-bar">
      <input
        type="text"
        placeholder="Search Oakbridge…"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        data-testid="search-input"
      />

      {hasQuery && (
        <div data-testid="search-results">
          <section data-testid="search-group-people">
            <h3>People</h3>
            {results.people.length === 0 ? (
              <p>No people found.</p>
            ) : (
              <ul>
                {results.people.map((agent) => (
                  <li key={agent.id}>
                    <button type="button" onClick={() => nav.push({ kind: "agent", id: agent.id })}>
                      {agent.name}
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section data-testid="search-group-places">
            <h3>Places</h3>
            {results.places.length === 0 ? (
              <p>No places found.</p>
            ) : (
              <ul>
                {results.places.map((settlement) => (
                  <li key={settlement.id}>
                    <button type="button" onClick={() => nav.push({ kind: "settlement", id: settlement.id })}>
                      {settlement.name}
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section data-testid="search-group-households">
            <h3>Households</h3>
            {results.households.length === 0 ? (
              <p>No households found.</p>
            ) : (
              <ul>
                {results.households.map((household) => (
                  <li key={household.id}>
                    <button type="button" onClick={() => nav.push({ kind: "household", id: household.id })}>
                      {household.name}
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section data-testid="search-group-events">
            <h3>Events</h3>
            {results.events.length === 0 ? (
              <p>No events found.</p>
            ) : (
              <ul>
                {results.events.map((event) => (
                  <li key={event.eventId}>
                    <button type="button" onClick={() => nav.push({ kind: "causal", eventId: event.eventId })}>
                      {event.summary}
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section data-testid="search-group-threads">
            <h3>Threads</h3>
            {results.threads.length === 0 ? (
              <p>No threads found.</p>
            ) : (
              <ul>
                {results.threads.map((thread) => (
                  <li key={thread.id}>
                    <button type="button" onClick={() => nav.push({ kind: "thread", id: thread.id })}>
                      {thread.title}
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </div>
      )}
    </div>
  );
}
