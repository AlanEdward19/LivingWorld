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

  // Fase 15.1, T8 (LWV-02): pista visual data-driven por ação existente.
  it("renders a distinct readable glyph per known action, not a fixed generic marker", () => {
    const sleeping = npcPawnSvg({ id: "9", currentAction: 1 });
    const working = npcPawnSvg({ id: "9", currentAction: 2 });

    expect(sleeping).toContain(">Zzz<");
    expect(working).toContain(">Trab<");
    expect(sleeping).not.toContain(">Trab<");
  });

  it("shows a readable generic glyph for an unknown action id, never the raw number", () => {
    const svg = npcPawnSvg({ id: "9", currentAction: 99 });

    expect(svg).toContain(">?<");
    expect(svg).not.toContain(">99<");
  });

  it("animates only the sleep glyph, and declares a reduced-motion fallback that stops it without hiding it", () => {
    const sleeping = npcPawnSvg({ id: "9", currentAction: 1 });
    const eating = npcPawnSvg({ id: "9", currentAction: 0 });

    expect(sleeping).toContain('class="action-glyph-pulse"');
    expect(eating).not.toContain("action-glyph-pulse\"");
    expect(sleeping).toMatch(/prefers-reduced-motion:\s*reduce/);
    expect(sleeping).toMatch(/\.action-glyph-pulse\{animation:none\}/);
  });
});
