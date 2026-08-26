import { describe, expect, it, vi } from "vitest";
import { fireEvent, render } from "@testing-library/react";
import { IsoTile } from "../../src/map/IsoTileRenderer";
import { paletteForBuildingKind } from "../../src/map/isoPalette";

describe("IsoTile (top-down, AD-019)", () => {
  it("renders exactly one rect (flat top-down square, not an isometric 3-face block)", () => {
    const { container } = render(
      <svg>
        <IsoTile gridX={0} gridY={0} kind="residence" />
      </svg>,
    );
    expect(container.querySelectorAll("rect")).toHaveLength(1);
    expect(container.querySelectorAll("polygon")).toHaveLength(0);
  });

  it("uses the palette colors matching the given kind", () => {
    const { container } = render(
      <svg>
        <IsoTile gridX={0} gridY={0} kind="forge" />
      </svg>,
    );
    const palette = paletteForBuildingKind("forge");
    const rect = container.querySelector("rect");
    expect(rect?.getAttribute("fill")).toBe(palette.top);
    expect(rect?.getAttribute("stroke")).toBe(palette.right);
  });

  it("fires onClick with the tile's own gridX/gridY", () => {
    const onClick = vi.fn();
    const { getByTestId } = render(
      <svg>
        <IsoTile gridX={3} gridY={7} kind="agriculture" onClick={onClick} />
      </svg>,
    );
    fireEvent.click(getByTestId("iso-tile"));
    expect(onClick).toHaveBeenCalledWith(3, 7);
  });

  it("does not error when onClick is omitted", () => {
    const { getByTestId } = render(
      <svg>
        <IsoTile gridX={1} gridY={1} kind="generic" />
      </svg>,
    );
    expect(() => fireEvent.click(getByTestId("iso-tile"))).not.toThrow();
  });
});
