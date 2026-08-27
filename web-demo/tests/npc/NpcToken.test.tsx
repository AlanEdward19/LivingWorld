import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { NpcToken } from "../../src/npc/NpcToken";

describe("NpcToken", () => {
  it("renders without error for a given NPC id", () => {
    const { getByRole } = render(<NpcToken id="mira-valen" />);
    expect(getByRole("img")).toBeInTheDocument();
  });

  it("names the NPC in the accessible alt text", () => {
    const { getByRole } = render(<NpcToken id="mira-valen" />);
    expect(getByRole("img").getAttribute("alt")).toContain("mira-valen");
  });
});
