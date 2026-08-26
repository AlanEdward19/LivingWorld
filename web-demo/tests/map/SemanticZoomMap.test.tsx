import { describe, expect, it, vi } from "vitest";
import { fireEvent, render } from "@testing-library/react";
import { SemanticZoomMap } from "../../src/map/SemanticZoomMap";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

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
