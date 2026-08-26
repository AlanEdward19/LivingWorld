import { describe, expect, it, vi } from "vitest";
import { fireEvent, render } from "@testing-library/react";
import { IsoTile } from "../../src/map/IsoTileRenderer";
import { paletteForBuildingKind } from "../../src/map/isoPalette";

describe("IsoTile", () => {
  it("renders exactly 3 polygon faces", () => {
    const { container } = render(
      <svg>
        <IsoTile gridX={0} gridY={0} kind="residence" />
      </svg>,
    );
    expect(container.querySelectorAll("polygon")).toHaveLength(3);
  });

  it("uses the palette colors matching the given kind", () => {
    const { container } = render(
      <svg>
        <IsoTile gridX={0} gridY={0} kind="forge" />
      </svg>,
    );
    const palette = paletteForBuildingKind("forge");
    const top = container.querySelector('polygon[data-face="top"]');
    const left = container.querySelector('polygon[data-face="left"]');
    const right = container.querySelector('polygon[data-face="right"]');
    expect(top?.getAttribute("fill")).toBe(palette.top);
    expect(left?.getAttribute("fill")).toBe(palette.left);
    expect(right?.getAttribute("fill")).toBe(palette.right);
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
