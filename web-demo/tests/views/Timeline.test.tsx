import { describe, expect, it } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { Timeline } from "../../src/views/Timeline";
import { CausalExplorer } from "../../src/views/CausalExplorer";
import { NavigationStore } from "../../src/nav/NavigationStore";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

describe("Timeline", () => {
  it("filtering by household Valen shows only the events relevant to that household (spec P2 Independent Test)", () => {
    render(<Timeline fixture={WORLD_FIXTURE} scope={{ type: "household", id: "valen-household" }} />);
    const expected = WORLD_FIXTURE.events.filter((e) => e.affectedHouseholdIds.includes("valen-household"));
    const list = screen.getByTestId("timeline-events");
    const items = within(list).getAllByRole("listitem");
    expect(items).toHaveLength(expected.length);
    for (const event of expected) {
      expect(list).toHaveTextContent(event.summary);
    }
  });

  it("world scope shows every event in the fixture", () => {
    render(<Timeline fixture={WORLD_FIXTURE} scope={{ type: "world" }} />);
    const items = within(screen.getByTestId("timeline-events")).getAllByRole("listitem");
    expect(items).toHaveLength(WORLD_FIXTURE.events.length);
  });

  it("agent scope shows only events affecting that agent", () => {
    render(<Timeline fixture={WORLD_FIXTURE} scope={{ type: "agent", id: "mira-valen" }} />);
    const expected = WORLD_FIXTURE.events.filter((e) => e.affectedAgentIds.includes("mira-valen"));
    const items = within(screen.getByTestId("timeline-events")).getAllByRole("listitem");
    expect(items).toHaveLength(expected.length);
  });

  it("filtering by event type narrows the scoped list further", () => {
    render(<Timeline fixture={WORLD_FIXTURE} scope={{ type: "household", id: "valen-household" }} />);
    fireEvent.change(screen.getByTestId("timeline-kind-filter"), { target: { value: "NeedCrisis" } });
    const items = within(screen.getByTestId("timeline-events")).getAllByRole("listitem");
    expect(items.length).toBeGreaterThan(0);
    for (const item of items) {
      expect(item.textContent).toContain("Mira Valen became very hungry.");
    }
  });
});

describe("Timeline — reached from the Causal Explorer (spec P1 AC7)", () => {
  it("clicking a consequence event navigates to a working Timeline, preserving the breadcrumb", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    nav.push({ kind: "settlement", id: "oakbridge" });
    nav.push({ kind: "agent", id: "mira-valen" });
    nav.push({ kind: "causal", eventId: "evt-grain-prices-rose" });

    render(<CausalExplorer fixture={WORLD_FIXTURE} nav={nav} eventId="evt-grain-prices-rose" />);
    fireEvent.click(screen.getByText("Mira Valen became very hungry."));

    expect(nav.current()).toMatchObject({ kind: "timeline" });
    expect(nav.breadcrumb()).toEqual([
      { kind: "world" },
      { kind: "settlement", id: "oakbridge" },
      { kind: "agent", id: "mira-valen" },
      { kind: "causal", eventId: "evt-grain-prices-rose" },
      nav.current(),
    ]);

    const route = nav.current();
    if (route.kind !== "timeline") throw new Error("expected timeline route");
    render(<Timeline fixture={WORLD_FIXTURE} scope={route.scope} />);
    expect(screen.getByTestId("timeline-view")).toBeInTheDocument();
  });
});
