import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render } from "@testing-library/react";
import { SemanticZoomMap } from "../../src/map/SemanticZoomMap";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";
import { toScreen } from "../../src/map/IsoProjection";
import { TILE_HEIGHT, TILE_WIDTH } from "../../src/map/IsoTileRenderer";

const OAKBRIDGE = WORLD_FIXTURE.settlements.find((s) => s.id === "oakbridge")!;
const OAKBRIDGE_AGENTS = WORLD_FIXTURE.agents.filter((a) => a.settlementId === "oakbridge");

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(0);
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

describe("SemanticZoomMap — world level (AD-018: NPCs never disappear)", () => {
  it("renders no building IsoTile (polygon) at world level", () => {
    const { container } = render(<SemanticZoomMap fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={() => {}} />);
    expect(container.querySelectorAll("polygon")).toHaveLength(0);
  });

  it("renders one marker per settlement in the fixture", () => {
    const { getAllByTestId } = render(<SemanticZoomMap fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={() => {}} />);
    expect(getAllByTestId("settlement-marker")).toHaveLength(WORLD_FIXTURE.settlements.length);
  });

  it("also renders every agent in the fixture as a small dot — never hidden at world zoom", () => {
    const { getAllByTestId } = render(<SemanticZoomMap fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={() => {}} />);
    const markers = getAllByTestId("agent-marker");
    expect(markers).toHaveLength(WORLD_FIXTURE.agents.length);
    for (const marker of markers) expect(marker).toHaveAttribute("data-zoom-scale", "world");
  });

  it("world-level agent dots are circles (small), not the full NpcToken image", () => {
    const { container } = render(<SemanticZoomMap fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={() => {}} />);
    expect(container.querySelectorAll("img")).toHaveLength(0);
    const identityDots = container.querySelectorAll('[data-testid="agent-marker"] circle:not([data-testid="event-marker"])');
    expect(identityDots).toHaveLength(WORLD_FIXTURE.agents.length);
  });

  it("clicking Oakbridge's marker calls onSelectSettlement with its id", () => {
    const onSelectSettlement = vi.fn();
    const { getAllByTestId } = render(<SemanticZoomMap fixture={WORLD_FIXTURE} onSelectSettlement={onSelectSettlement} onSelectNpc={() => {}} />);
    const oakbridgeIndex = WORLD_FIXTURE.settlements.findIndex((s) => s.id === "oakbridge");
    fireEvent.click(getAllByTestId("settlement-marker")[oakbridgeIndex]);
    expect(onSelectSettlement).toHaveBeenCalledWith("oakbridge");
  });

  it("clicking Mira's dot at world level calls onSelectNpc — clickable at any zoom (spec P1b AC4)", () => {
    const onSelectNpc = vi.fn();
    const { getAllByTestId } = render(<SemanticZoomMap fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={onSelectNpc} />);
    const miraIndex = WORLD_FIXTURE.agents.findIndex((a) => a.id === "mira-valen");
    fireEvent.click(getAllByTestId("agent-marker")[miraIndex]);
    expect(onSelectNpc).toHaveBeenCalledWith("mira-valen");
  });
});

describe("SemanticZoomMap — settlement level (buildings AND NPCs together, no toggle)", () => {
  it("shows the settlement's buildings", () => {
    const { container } = render(
      <SemanticZoomMap fixture={WORLD_FIXTURE} level="settlement" settlementId="oakbridge" onSelectSettlement={() => {}} onSelectNpc={() => {}} />,
    );
    expect(container.querySelectorAll("polygon")).toHaveLength(OAKBRIDGE.buildings.length * 3);
  });

  it("shows every agent of the settlement in the SAME render as the buildings — never a mutually exclusive toggle", () => {
    const { container, getAllByTestId } = render(
      <SemanticZoomMap fixture={WORLD_FIXTURE} level="settlement" settlementId="oakbridge" onSelectSettlement={() => {}} onSelectNpc={() => {}} />,
    );
    expect(container.querySelectorAll("polygon")).toHaveLength(OAKBRIDGE.buildings.length * 3);
    expect(container.querySelectorAll("img")).toHaveLength(OAKBRIDGE_AGENTS.length);
    const markers = getAllByTestId("agent-marker");
    for (const marker of markers) expect(marker).toHaveAttribute("data-zoom-scale", "settlement");
  });

  it("clicking Mira's marker calls onSelectNpc with her id", () => {
    const onSelectNpc = vi.fn();
    const { getAllByTestId } = render(
      <SemanticZoomMap fixture={WORLD_FIXTURE} level="settlement" settlementId="oakbridge" onSelectSettlement={() => {}} onSelectNpc={onSelectNpc} />,
    );
    const miraIndex = OAKBRIDGE_AGENTS.findIndex((a) => a.id === "mira-valen");
    fireEvent.click(getAllByTestId("agent-marker")[miraIndex]);
    expect(onSelectNpc).toHaveBeenCalledWith("mira-valen");
  });
});

describe("SemanticZoomMap — information density changes across zoom levels", () => {
  it("world level has fewer distinguishable elements than settlement level for the same settlement", () => {
    const worldRender = render(<SemanticZoomMap fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={() => {}} />);
    const worldPolygonCount = worldRender.container.querySelectorAll("polygon").length;

    const settlementRender = render(
      <SemanticZoomMap fixture={WORLD_FIXTURE} level="settlement" settlementId="oakbridge" onSelectSettlement={() => {}} onSelectNpc={() => {}} />,
    );
    const settlementPolygonCount = settlementRender.container.querySelectorAll("polygon").length;

    expect(worldPolygonCount).toBe(0); // sem prédios no nível mundo
    expect(settlementPolygonCount).toBe(OAKBRIDGE.buildings.length * 3);
    expect(settlementPolygonCount).toBeGreaterThan(worldPolygonCount);
  });
});

describe("SemanticZoomMap — decorative patrol movement (AD-018)", () => {
  it("Mira's world-level dot position changes over (simulated) time as she patrols", () => {
    const { getAllByTestId } = render(<SemanticZoomMap fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={() => {}} />);
    const miraIndex = WORLD_FIXTURE.agents.findIndex((a) => a.id === "mira-valen");
    const dot = () => getAllByTestId("agent-marker")[miraIndex].querySelector("circle")!;
    const initialCx = dot().getAttribute("cx");

    act(() => vi.advanceTimersByTime(2000));

    expect(dot().getAttribute("cx")).not.toBe(initialCx);
  });
});

describe("SemanticZoomMap — camera centered on content (doc §46)", () => {
  it("world-level viewBox bounding box contains every settlement's screen point", () => {
    const { container } = render(<SemanticZoomMap fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={() => {}} />);
    const svg = container.querySelector("svg")!;
    const [minX, minY, width, height] = svg.getAttribute("viewBox")!.split(" ").map(Number);

    for (const settlement of WORLD_FIXTURE.settlements) {
      const { x, y } = toScreen(settlement.gridPosition.x, settlement.gridPosition.y, TILE_WIDTH, TILE_HEIGHT);
      expect(x).toBeGreaterThanOrEqual(minX);
      expect(x).toBeLessThanOrEqual(minX + width);
      expect(y).toBeGreaterThanOrEqual(minY);
      expect(y).toBeLessThanOrEqual(minY + height);
    }
  });

  it("does not use the old fixed 0 0 800 600 viewBox that left content off-center", () => {
    const { container } = render(<SemanticZoomMap fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={() => {}} />);
    expect(container.querySelector("svg")!.getAttribute("viewBox")).not.toBe("0 0 800 600");
  });
});

describe("SemanticZoomMap — event markers for notable events (doc §48)", () => {
  it("shows event markers for Oakbridge and for every notable agent at world level", () => {
    const { getAllByTestId } = render(<SemanticZoomMap fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={() => {}} />);
    const notableAgentCount = WORLD_FIXTURE.agents.filter((a) =>
      WORLD_FIXTURE.events.some(
        (e) => WORLD_FIXTURE.storyThreads.some((t) => t.eventIds.includes(e.eventId)) && e.affectedAgentIds.includes(a.id),
      ),
    ).length;
    // 1 marcador de settlement (Oakbridge) + 1 por agent notável (doc §48 se aplica a qualquer
    // entidade com localização física, não só settlements).
    expect(getAllByTestId("event-marker")).toHaveLength(1 + notableAgentCount);
  });

  it("shows an event marker on Mira (affected by Story Thread events) at settlement level", () => {
    const { getAllByTestId } = render(
      <SemanticZoomMap fixture={WORLD_FIXTURE} level="settlement" settlementId="oakbridge" onSelectSettlement={() => {}} onSelectNpc={() => {}} />,
    );
    const notableCount = OAKBRIDGE_AGENTS.filter((a) =>
      WORLD_FIXTURE.events.some(
        (e) => WORLD_FIXTURE.storyThreads.some((t) => t.eventIds.includes(e.eventId)) && e.affectedAgentIds.includes(a.id),
      ),
    ).length;
    expect(getAllByTestId("event-marker")).toHaveLength(notableCount);
  });
});

describe("SemanticZoomMap — keyboard accessibility (doc §87, obrigatório)", () => {
  it("settlement markers are focusable and activate on Enter", () => {
    const onSelectSettlement = vi.fn();
    const { getAllByTestId } = render(<SemanticZoomMap fixture={WORLD_FIXTURE} onSelectSettlement={onSelectSettlement} onSelectNpc={() => {}} />);
    const oakbridgeIndex = WORLD_FIXTURE.settlements.findIndex((s) => s.id === "oakbridge");
    const marker = getAllByTestId("settlement-marker")[oakbridgeIndex];
    expect(marker).toHaveAttribute("tabindex", "0");
    expect(marker).toHaveAttribute("aria-label", "Open Oakbridge");
    fireEvent.keyDown(marker, { key: "Enter" });
    expect(onSelectSettlement).toHaveBeenCalledWith("oakbridge");
  });

  it("settlement markers activate on Space too", () => {
    const onSelectSettlement = vi.fn();
    const { getAllByTestId } = render(<SemanticZoomMap fixture={WORLD_FIXTURE} onSelectSettlement={onSelectSettlement} onSelectNpc={() => {}} />);
    fireEvent.keyDown(getAllByTestId("settlement-marker")[0], { key: " " });
    expect(onSelectSettlement).toHaveBeenCalled();
  });

  it("does not activate on an unrelated key", () => {
    const onSelectSettlement = vi.fn();
    const { getAllByTestId } = render(<SemanticZoomMap fixture={WORLD_FIXTURE} onSelectSettlement={onSelectSettlement} onSelectNpc={() => {}} />);
    fireEvent.keyDown(getAllByTestId("settlement-marker")[0], { key: "Tab" });
    expect(onSelectSettlement).not.toHaveBeenCalled();
  });

  it("agent markers are focusable and activate on Enter, at world level too", () => {
    const onSelectNpc = vi.fn();
    const { getAllByTestId } = render(<SemanticZoomMap fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={onSelectNpc} />);
    const miraIndex = WORLD_FIXTURE.agents.findIndex((a) => a.id === "mira-valen");
    const marker = getAllByTestId("agent-marker")[miraIndex];
    expect(marker).toHaveAttribute("tabindex", "0");
    fireEvent.keyDown(marker, { key: "Enter" });
    expect(onSelectNpc).toHaveBeenCalledWith("mira-valen");
  });
});
