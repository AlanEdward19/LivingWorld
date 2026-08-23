import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { NpcTokenSvg } from "../src/components/NpcTokenSvg";

// Feedback do usuário (2026-08-21): a pista de ação virou um badge sobreposto ao token (não mais
// dentro do SVG de identidade) — este arquivo cobre o lado DOM (usado por NpcInspector); o lado
// canvas (mapa) é `map-engine/renderer.test.ts`.
describe("NpcTokenSvg", () => {
  it("shows an action badge and carries the action label in the accessible alt text", () => {
    render(<NpcTokenSvg npcId="7" currentAction={1} />);

    expect(document.querySelector(".npc-action-badge")).not.toBeNull();
    expect(screen.getByRole("img")).toHaveAttribute("alt", "Aparência visual do NPC 7 — Dormindo");
  });

  it("omits the badge entirely for Travel — walking around isn't worth a badge", () => {
    render(<NpcTokenSvg npcId="7" currentAction={4} />);

    expect(document.querySelector(".npc-action-badge")).toBeNull();
  });

  it("omits the badge when there is no current action", () => {
    render(<NpcTokenSvg npcId="7" />);

    expect(document.querySelector(".npc-action-badge")).toBeNull();
    expect(screen.getByRole("img")).toHaveAttribute("alt", "Aparência visual do NPC 7");
  });

  it("pulses catalog-animated badges including eat, sleep, work, socialize, rest, and buy", () => {
    for (const action of [0, 1, 2, 3, 5, 6]) {
      const { unmount } = render(<NpcTokenSvg npcId={String(action)} currentAction={action} />);
      expect(document.querySelector(".npc-action-badge-pulse")).not.toBeNull();
      unmount();
    }
  });
});
