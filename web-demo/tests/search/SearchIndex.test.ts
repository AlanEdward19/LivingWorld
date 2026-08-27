import { describe, expect, it } from "vitest";
import { search } from "../../src/search/SearchIndex";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

describe("search", () => {
  it("finds Mira in People when searching 'Mira'", () => {
    const results = search("Mira", WORLD_FIXTURE);
    expect(results.people.map((p) => p.id)).toContain("mira-valen");
  });

  it("is case-insensitive", () => {
    const results = search("mira", WORLD_FIXTURE);
    expect(results.people.map((p) => p.id)).toContain("mira-valen");
  });

  it("finds Oakbridge in Places", () => {
    const results = search("Oakbridge", WORLD_FIXTURE);
    expect(results.places.map((p) => p.id)).toContain("oakbridge");
  });

  it("finds Valen Household in Households", () => {
    const results = search("Valen", WORLD_FIXTURE);
    expect(results.households.map((h) => h.id)).toContain("valen-household");
  });

  it("finds a matching event by its summary text", () => {
    const results = search("grain prices", WORLD_FIXTURE);
    expect(results.events.map((e) => e.eventId)).toContain("evt-grain-prices-rose");
  });

  it("finds the Oakbridge Food Crisis thread by title", () => {
    const results = search("Food Crisis", WORLD_FIXTURE);
    expect(results.threads.map((t) => t.id)).toContain("oakbridge-food-crisis");
  });

  it("returns an explicit empty result set for a query with no matches", () => {
    const results = search("no-such-entity-anywhere", WORLD_FIXTURE);
    expect(results).toEqual({ people: [], places: [], households: [], events: [], threads: [] });
  });

  it("returns an explicit empty result set for an empty query", () => {
    const results = search("", WORLD_FIXTURE);
    expect(results).toEqual({ people: [], places: [], households: [], events: [], threads: [] });
  });
});
