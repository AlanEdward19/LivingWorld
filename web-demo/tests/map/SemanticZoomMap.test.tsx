import { describe, expect, it, vi } from "vitest";
import { fireEvent, render } from "@testing-library/react";
import { SemanticZoomMap } from "../../src/map/SemanticZoomMap";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";
import { toScreen } from "../../src/map/IsoProjection";
import { TILE_HEIGHT, TILE_WIDTH } from "../../src/map/IsoTileRenderer";

const OAKBRIDGE = WORLD_FIXTURE.settlements.find((s) => s.id === "oakbridge")!;
const OAKBRIDGE_AGENTS = WORLD_FIXTURE.agents.filter((a) => a.settlementId === "oakbridge");

describe("SemanticZoomMap — world zoom level", () => {
  it("renders no building IsoTile (polygon) at world level", () => {
    const { container } = render(
      <SemanticZoomMap fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={() => {}} />,
    );
    expect(container.querySelectorAll("polygon")).toHaveLength(0);
  });

  it("renders no NpcToken (img) at world level", () => {
    const { container } = render(
      <SemanticZoomMap fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={() => {}} />,
    );
    expect(container.querySelectorAll("img")).toHaveLength(0);
  });

  it("renders one marker per settlement in the fixture", () => {
    const { getAllByTestId } = render(
      <SemanticZoomMap fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={() => {}} />,
    );
    expect(getAllByTestId("settlement-marker")).toHaveLength(WORLD_FIXTURE.settlements.length);
  });

  it("clicking Oakbridge's marker calls onSelectSettlement with its id", () => {
    const onSelectSettlement = vi.fn();
    const { getAllByTestId } = render(
      <SemanticZoomMap fixture={WORLD_FIXTURE} onSelectSettlement={onSelectSettlement} onSelectNpc={() => {}} />,
    );
    const oakbridgeIndex = WORLD_FIXTURE.settlements.findIndex((s) => s.id === "oakbridge");
    fireEvent.click(getAllByTestId("settlement-marker")[oakbridgeIndex]);
    expect(onSelectSettlement).toHaveBeenCalledWith("oakbridge");
  });
});

describe("SemanticZoomMap — district zoom level", () => {
  it("shows the selected settlement's buildings, no NPC token", () => {
    const { container } = render(
      <SemanticZoomMap
        fixture={WORLD_FIXTURE}
        level="district"
        settlementId="oakbridge"
        onSelectSettlement={() => {}}
        onSelectNpc={() => {}}
      />,
    );
    expect(container.querySelectorAll("polygon")).toHaveLength(OAKBRIDGE.buildings.length * 3);
    expect(container.querySelectorAll("img")).toHaveLength(0);
  });
});

describe("SemanticZoomMap — agent zoom level", () => {
  it("shows individual NPCs for the selected settlement, clickable", () => {
    const onSelectNpc = vi.fn();
    const { container, getAllByTestId } = render(
      <SemanticZoomMap
        fixture={WORLD_FIXTURE}
        level="agent"
        settlementId="oakbridge"
        onSelectSettlement={() => {}}
        onSelectNpc={onSelectNpc}
      />,
    );
    expect(container.querySelectorAll("img")).toHaveLength(OAKBRIDGE_AGENTS.length);
    const miraIndex = OAKBRIDGE_AGENTS.findIndex((a) => a.id === "mira-valen");
    fireEvent.click(getAllByTestId("agent-marker")[miraIndex]);
    expect(onSelectNpc).toHaveBeenCalledWith("mira-valen");
  });
});

describe("SemanticZoomMap — information density changes across zoom levels", () => {
  it("renders a different element count at each of the 3 levels for the same settlement", () => {
    const worldRender = render(
      <SemanticZoomMap fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={() => {}} />,
    );
    const worldCount = worldRender.getAllByTestId("settlement-marker").length;

    const districtRender = render(
      <SemanticZoomMap
        fixture={WORLD_FIXTURE}
        level="district"
        settlementId="oakbridge"
        onSelectSettlement={() => {}}
        onSelectNpc={() => {}}
      />,
    );
    const districtCount = districtRender.container.querySelectorAll("polygon").length;

    const agentRender = render(
      <SemanticZoomMap
        fixture={WORLD_FIXTURE}
        level="agent"
        settlementId="oakbridge"
        onSelectSettlement={() => {}}
        onSelectNpc={() => {}}
      />,
    );
    const agentCount = agentRender.getAllByTestId("agent-marker").length;

    expect(worldCount).toBe(WORLD_FIXTURE.settlements.length);
    expect(districtCount).toBe(OAKBRIDGE.buildings.length * 3);
    expect(agentCount).toBe(OAKBRIDGE_AGENTS.length);
    expect(new Set([worldCount, districtCount, agentCount]).size).toBe(3);
  });
});

describe("SemanticZoomMap — camera centered on content (doc §192 Map QA)", () => {
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

describe("SemanticZoomMap — event markers for notable events (doc §103)", () => {
  it("shows an event marker on Oakbridge (has Story Thread events) at world level", () => {
    const { getAllByTestId } = render(<SemanticZoomMap fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={() => {}} />);
    expect(getAllByTestId("event-marker")).toHaveLength(1);
  });

  it("shows an event marker on Mira (affected by Story Thread events) at agent level", () => {
    const { container, getAllByTestId } = render(
      <SemanticZoomMap fixture={WORLD_FIXTURE} level="agent" settlementId="oakbridge" onSelectSettlement={() => {}} onSelectNpc={() => {}} />,
    );
    const oakbridgeAgents = WORLD_FIXTURE.agents.filter((a) => a.settlementId === "oakbridge");
    const notableCount = oakbridgeAgents.filter((a) =>
      WORLD_FIXTURE.events.some(
        (e) => WORLD_FIXTURE.storyThreads.some((t) => t.eventIds.includes(e.eventId)) && e.affectedAgentIds.includes(a.id),
      ),
    ).length;
    expect(getAllByTestId("event-marker")).toHaveLength(notableCount);
    expect(container.querySelectorAll("[data-testid='agent-marker']").length).toBe(oakbridgeAgents.length);
  });
});
