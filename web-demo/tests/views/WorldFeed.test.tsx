import { describe, expect, it } from "vitest";
import { render, screen, within } from "@testing-library/react";
import { WorldFeed } from "../../src/views/WorldFeed";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

describe("WorldFeed", () => {
  it("groups events by tick, one group header per distinct tick in chronological order", () => {
    render(<WorldFeed fixture={WORLD_FIXTURE} />);
    const groups = screen.getAllByTestId("world-feed-group");
    const expectedTicks = Array.from(new Set(WORLD_FIXTURE.events.map((e) => e.tick)));
    expect(groups).toHaveLength(expectedTicks.length);
    groups.forEach((group, index) => {
      expect(within(group).getByRole("heading").textContent).toBe(expectedTicks[index]);
    });
  });

  it("shows every event from the fixture across the groups", () => {
    render(<WorldFeed fixture={WORLD_FIXTURE} />);
    const feed = screen.getByTestId("world-feed");
    for (const event of WORLD_FIXTURE.events) {
      expect(feed).toHaveTextContent(event.summary);
    }
  });

  it("orders events within a shared tick group by relevance (more affected agents/households first)", () => {
    render(<WorldFeed fixture={WORLD_FIXTURE} />);
    const sharedTick = "Year 312 · Spring · 09";
    const sameTickEvents = WORLD_FIXTURE.events.filter((e) => e.tick === sharedTick);
    expect(sameTickEvents.length).toBeGreaterThan(1);

    const group = screen.getAllByTestId("world-feed-group").find((g) => within(g).getByRole("heading").textContent === sharedTick)!;
    const renderedOrder = within(group)
      .getAllByRole("listitem")
      .map((li) => li.textContent);

    const expectedOrder = [...sameTickEvents]
      .sort((a, b) => b.affectedAgentIds.length + b.affectedHouseholdIds.length - (a.affectedAgentIds.length + a.affectedHouseholdIds.length))
      .map((e) => e.summary);

    expect(renderedOrder).toEqual(expectedOrder);
  });
});
