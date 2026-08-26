import type {
  AgentFixture,
  HouseholdFixture,
  SettlementFixture,
  StoryThreadFixture,
  WorldEventFixture,
  WorldFixture,
} from "../fixture/types";

export interface SearchResults {
  people: AgentFixture[];
  places: SettlementFixture[];
  households: HouseholdFixture[];
  events: WorldEventFixture[];
  threads: StoryThreadFixture[];
}

const EMPTY_RESULTS: SearchResults = { people: [], places: [], households: [], events: [], threads: [] };

/**
 * Busca client-side sobre o fixture, agrupada por People/Places/Households/Events/Threads
 * (doc#138) — fixture é pequeno o bastante pra filtro linear simples, sem indexação.
 */
export function search(query: string, fixture: WorldFixture): SearchResults {
  const q = query.trim().toLowerCase();
  if (!q) return EMPTY_RESULTS;

  return {
    people: fixture.agents.filter((agent) => agent.name.toLowerCase().includes(q)),
    places: fixture.settlements.filter((settlement) => settlement.name.toLowerCase().includes(q)),
    households: fixture.households.filter((household) => household.name.toLowerCase().includes(q)),
    events: fixture.events.filter((event) => event.summary.toLowerCase().includes(q)),
    threads: fixture.storyThreads.filter((thread) => thread.title.toLowerCase().includes(q)),
  };
}
