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

// Redesign (pedido do usuário 2026-08-27): trunk horizontal estilo git branch, agrupado por
// Year·Season — não mais uma lista vertical. Curvas de causalidade removidas depois (pedido do
// usuário): redundantes com o hover-highlight, ver describe abaixo.
describe("Timeline — git-branch-style horizontal graph", () => {
  it("draws one group divider label per distinct Year·Season present in the scoped events", () => {
    const { container } = render(<Timeline fixture={WORLD_FIXTURE} scope={{ type: "world" }} />);
    const expectedGroups = new Set(WORLD_FIXTURE.events.map((e) => e.tick.split(" · ").slice(0, 2).join(" · ")));
    const labels = container.querySelectorAll(".timeline-group-label");
    expect(labels).toHaveLength(expectedGroups.size);
  });

  it("shows critical events with the critical marker inside the node label", () => {
    render(<Timeline fixture={WORLD_FIXTURE} scope={{ type: "world" }} />);
    const critical = WORLD_FIXTURE.events.find((e) => e.severity === "critical")!;
    const node = screen.getByText((_, element) => element?.textContent?.includes(critical.summary) ?? false, { selector: ".timeline-node-label" });
    expect(within(node).getByTestId("timeline-critical-marker")).toBeInTheDocument();
  });

  it("still styles the type filter as a real <select> (fireEvent.change keeps working)", () => {
    render(<Timeline fixture={WORLD_FIXTURE} scope={{ type: "world" }} />);
    const select = screen.getByTestId("timeline-kind-filter");
    expect(select.tagName).toBe("SELECT");
    expect(select).toHaveClass("timeline-filter-select");
  });
});

// Pedido do usuário 2026-08-27: "passar mouse sobre um evento destaca os eventos que levaram a
// ele" + "clicar abre um popup de timeline só daquele evento".
describe("Timeline — hover highlights causal ancestors, click opens a single-event popup", () => {
  it("hovering a node highlights its causal ancestors, not unrelated nodes", () => {
    const effect = WORLD_FIXTURE.events.find((e) => e.eventId === "evt-grain-prices-rose")!;
    render(<Timeline fixture={WORLD_FIXTURE} scope={{ type: "world" }} />);
    const nodes = screen.getAllByTestId("timeline-node");
    const effectNode = nodes.find((n) => n.textContent?.includes(effect.summary))!;
    const cause = WORLD_FIXTURE.events.find((e) => e.eventId === effect.causeEventId)!;
    const causeNode = nodes.find((n) => n.textContent?.includes(cause.summary))!;

    // Um nó "não relacionado" de verdade: não pode estar na cadeia de causas de `effect` (senão
    // o teste ficaria frágil dependendo de QUANTOS ancestrais o fixture tem).
    const ancestorSummaries = new Set<string>();
    let walker: typeof effect | undefined = effect;
    while (walker?.causeEventId) {
      const next = WORLD_FIXTURE.events.find((e) => e.eventId === walker!.causeEventId);
      if (!next) break;
      ancestorSummaries.add(next.summary);
      walker = next;
    }
    const unrelatedNode = nodes.find(
      (n) => n !== effectNode && !n.textContent?.includes(effect.summary) && ![...ancestorSummaries].some((s) => n.textContent?.includes(s)),
    )!;

    fireEvent.mouseEnter(effectNode);

    expect(causeNode).toHaveClass("timeline-node--highlighted");
    expect(unrelatedNode).not.toHaveClass("timeline-node--highlighted");

    fireEvent.mouseLeave(effectNode);
    expect(causeNode).not.toHaveClass("timeline-node--highlighted");
  });

  it("clicking a node opens a popup with that event's causal chain, oldest first", () => {
    const effect = WORLD_FIXTURE.events.find((e) => e.eventId === "evt-grain-prices-rose")!;
    const cause = WORLD_FIXTURE.events.find((e) => e.eventId === effect.causeEventId)!;
    render(<Timeline fixture={WORLD_FIXTURE} scope={{ type: "world" }} />);
    const nodes = screen.getAllByTestId("timeline-node");
    const effectNode = nodes.find((n) => n.textContent?.includes(effect.summary))!;

    fireEvent.click(effectNode);

    const popup = screen.getByTestId("popup-panel");
    const chain = within(popup).getByTestId("timeline-event-chain");
    expect(within(chain).getByText((_, el) => el?.textContent?.includes(cause.summary) ?? false, { selector: "li" })).toBeInTheDocument();
    expect(within(chain).getByText((_, el) => el?.textContent?.includes(effect.summary) ?? false, { selector: "li" })).toBeInTheDocument();

    fireEvent.click(screen.getByTestId("popup-close"));
    expect(screen.queryByTestId("popup-panel")).not.toBeInTheDocument();
  });

  it("clicking a node with no known cause shows the empty-chain message", () => {
    const root = WORLD_FIXTURE.events.find((e) => !e.causeEventId)!;
    render(<Timeline fixture={WORLD_FIXTURE} scope={{ type: "world" }} />);
    const nodes = screen.getAllByTestId("timeline-node");
    const rootNode = nodes.find((n) => n.textContent?.includes(root.summary))!;

    fireEvent.click(rootNode);

    expect(screen.getByTestId("timeline-no-known-cause")).toBeInTheDocument();
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
    // Breadcrumb só mostra localização (World/Settlement) — agent/causal/timeline são overlays,
    // nunca entram na pilha (só World/Settlement/Building formam a hierarquia espacial).
    expect(nav.breadcrumb()).toEqual([{ kind: "world" }, { kind: "settlement", id: "oakbridge" }]);

    const route = nav.current();
    if (route.kind !== "timeline") throw new Error("expected timeline route");
    render(<Timeline fixture={WORLD_FIXTURE} scope={route.scope} />);
    expect(screen.getByTestId("timeline-view")).toBeInTheDocument();
  });
});
