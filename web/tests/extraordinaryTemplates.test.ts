import { describe, expect, it } from "vitest";
import { EXTRAORDINARY_TEMPLATES } from "../src/extraordinaryTemplates";

describe("extraordinary authoring templates", () => {
  it("offers the requested editable archetypes and color variants without duplicate ids", () => {
    const names = EXTRAORDINARY_TEMPLATES.map((item) => item.name);
    expect(names).toEqual(expect.arrayContaining([
      "Vampiro", "Lobisomem", "Lanterna Verde", "Lanterna Azul",
      "Lanterna Amarelo", "Kryptoniano", "Velocista",
    ]));
    expect(new Set(EXTRAORDINARY_TEMPLATES.map((item) => item.descriptor.id)).size).toBe(EXTRAORDINARY_TEMPLATES.length);
    expect(EXTRAORDINARY_TEMPLATES.find((item) => item.name === "Vampiro")?.descriptor)
      .toMatchObject({ needSubstitutionReplacesNeed: "hunger", senescenceRateMultiplier: 0 });
    expect(EXTRAORDINARY_TEMPLATES.find((item) => item.name === "Lanterna Verde")?.descriptor.effects)
      .toContain("construct.create");
  });
});
