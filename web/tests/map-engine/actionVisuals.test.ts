import { describe, expect, it } from "vitest";
import { ACTION_LABELS, actionVisualFor } from "../../src/map-engine/actionVisuals";

// Fase 15.1, T8 (LWV-02): catálogo data-driven de pistas visuais por ação existente.
describe("actionVisuals", () => {
  it("declares a distinct visual for every known ActionType (0-6, LivingWorld.Domain)", () => {
    for (const id of [0, 1, 2, 3, 4, 5, 6]) {
      const visual = actionVisualFor(id);
      expect(visual.key).not.toBe("unknown");
      expect(visual.label.length).toBeGreaterThan(0);
      expect(visual.glyph.length).toBeGreaterThan(0);
    }
  });

  it("falls back to a readable generic descriptor for an unknown action id, never a raw enum", () => {
    const visual = actionVisualFor(42);

    expect(visual.key).toBe("unknown");
    expect(visual.label).toBe("Atividade 42");
    expect(visual.animated).toBe(false);
  });

  it("marks sleep as the only animated known action", () => {
    const animated = [0, 1, 2, 3, 4, 5, 6].filter((id) => actionVisualFor(id).animated);

    expect(animated).toEqual([1]);
  });

  it("exposes ACTION_LABELS consistent with the catalog, for NpcInspector to reuse", () => {
    expect(ACTION_LABELS[1]).toBe(actionVisualFor(1).label);
    expect(Object.keys(ACTION_LABELS)).toHaveLength(7);
  });
});
