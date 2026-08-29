import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { MapHoverCard } from "../../src/components/MapHoverCard";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

describe("MapHoverCard", () => {
  it("renders nothing when there is no hover target", () => {
    const { container } = render(<MapHoverCard fixture={WORLD_FIXTURE} hover={null} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("shows a settlement's LOD summary (population/food/employment), positioned near the cursor", () => {
    render(<MapHoverCard fixture={WORLD_FIXTURE} hover={{ kind: "settlement", id: "oakbridge", x: 100, y: 200 }} />);
    const card = screen.getByText("Oakbridge").closest("div")!;
    expect(card).toHaveTextContent("Population 42");
    expect(card).toHaveTextContent("Food scarce");
    expect(card.style.left).toBe("116px"); // x + 16
    expect(card.style.top).toBe("192px"); // y - 8
  });

  it("shows an agent's LOD summary (age/profession/current intent), not the full sidebar", () => {
    render(<MapHoverCard fixture={WORLD_FIXTURE} hover={{ kind: "agent", id: "mira-valen", x: 0, y: 0 }} />);
    const card = screen.getByText("Mira Valen").closest("div")!;
    expect(card).toHaveTextContent("34 · Baker");
    expect(card).toHaveTextContent("Looking for affordable grain");
    // Menos informação que a sidebar cheia — sem household/relationships/why aqui.
    expect(card).not.toHaveTextContent("Valen Household");
  });

  it("shows a building's LOD summary (kind, occupant count)", () => {
    render(<MapHoverCard fixture={WORLD_FIXTURE} hover={{ kind: "building", id: "bld-corvin-bakery", x: 0, y: 0 }} />);
    const card = screen.getByText("Corvin's Bakery").closest("div")!;
    expect(card).toHaveTextContent("generic");
    expect(card).toHaveTextContent("3 occupants");
  });

  it("renders nothing for an id that no longer exists in the fixture", () => {
    const { container } = render(<MapHoverCard fixture={WORLD_FIXTURE} hover={{ kind: "agent", id: "does-not-exist", x: 0, y: 0 }} />);
    expect(container).toBeEmptyDOMElement();
  });
});
