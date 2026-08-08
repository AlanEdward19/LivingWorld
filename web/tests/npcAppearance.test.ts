import { describe, expect, it } from "vitest";
import { appearanceForNpc, npcPawnSvg } from "../src/npcAppearance";

function withoutStateLayer(svg: string): string {
  return svg.replace(/\s*<g data-layer="state">.*?<\/g>/, "").replace(/\s+/g, " ");
}

describe("npcAppearance", () => {
  it("produces the same layered SVG for the same identity and state", () => {
    const first = npcPawnSvg({ id: "42", currentAction: 3 });
    const second = npcPawnSvg({ id: "42", currentAction: 3 });

    expect(second).toBe(first);
    expect(first).toContain('data-layer="shadow"');
    expect(first).toContain('data-layer="body"');
    expect(first).toContain('data-layer="head"');
    expect(first).toContain('data-layer="hair"');
  });

  it("keeps identity layers stable when only the current action changes", () => {
    const idle = npcPawnSvg({ id: "42" });
    const acting = npcPawnSvg({ id: "42", currentAction: 9 });

    expect(appearanceForNpc("42")).toEqual(appearanceForNpc("42"));
    expect(withoutStateLayer(acting)).toBe(withoutStateLayer(idle));
    expect(acting).toContain('data-layer="state"');
  });

  it("does not invent demographic or profession data in the SVG", () => {
    const svg = npcPawnSvg({ id: "7" });

    expect(svg).not.toMatch(/age|profession|condition/i);
  });
});
