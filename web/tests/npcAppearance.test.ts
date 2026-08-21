import { describe, expect, it } from "vitest";
import { appearanceForNpc, npcPawnSvg } from "../src/npcAppearance";

describe("npcAppearance", () => {
  it("produces the same layered SVG for the same identity", () => {
    const first = npcPawnSvg({ id: "42" });
    const second = npcPawnSvg({ id: "42" });

    expect(second).toBe(first);
    expect(first).toContain('data-layer="shadow"');
    expect(first).toContain('data-layer="body"');
    expect(first).toContain('data-layer="head"');
    expect(first).toContain('data-layer="hair"');
  });

  it("does not invent demographic or profession data in the SVG", () => {
    const svg = npcPawnSvg({ id: "7" });

    expect(svg).not.toMatch(/age|profession|condition/i);
  });

  it("keeps the phenotype stable across repeated lookups of the same identity", () => {
    expect(appearanceForNpc("42")).toEqual(appearanceForNpc("42"));
  });

  // Feedback do usuário (2026-08-21): a ação virou um badge desenhado à parte (canvas:
  // `map-engine/actionIcon.ts`; DOM: `ActionBadge.tsx`) — este SVG de identidade nunca mais
  // varia por ação, então não há mais glifo/estado pra testar aqui.
  it("never varies by action — the pawn SVG is identity-only", () => {
    expect(npcPawnSvg({ id: "9" })).not.toContain("data-layer=\"state\"");
  });
});
