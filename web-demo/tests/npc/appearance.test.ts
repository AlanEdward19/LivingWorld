import { describe, expect, it } from "vitest";
import { appearanceForNpc } from "../../src/npc/appearance";

// T4: cópia literal de web/src/npcAppearance.ts. Expected values abaixo foram gerados rodando
// o algoritmo original (web/src/npcAppearance.ts, hash FNV-1a determinístico por id) para os
// mesmos ids — provam que a cópia produz a MESMA saída, sem import cruzado entre os 2 projetos
// (design.md § Architecture: isolamento total).
describe("appearanceForNpc — parity with web/src/npcAppearance.ts", () => {
  it("mira-valen", () => {
    expect(appearanceForNpc("mira-valen")).toEqual({
      skin: "#f2c49b",
      hair: "#8b3030",
      hairStyle: "tuft",
      clothing: "#486b70",
      clothingAccent: "#9d8cc0",
    });
  });

  it("tomas-valen", () => {
    expect(appearanceForNpc("tomas-valen")).toEqual({
      skin: "#754331",
      hair: "#513522",
      hairStyle: "parted",
      clothing: "#8a5d45",
      clothingAccent: "#c8a96b",
    });
  });

  it("corvin", () => {
    expect(appearanceForNpc("corvin")).toEqual({
      skin: "#754331",
      hair: "#c8a46b",
      hairStyle: "parted",
      clothing: "#756844",
      clothingAccent: "#8db1a5",
    });
  });

  it("rowan", () => {
    expect(appearanceForNpc("rowan")).toEqual({
      skin: "#d99a6c",
      hair: "#c8a46b",
      hairStyle: "shaved",
      clothing: "#6f536f",
      clothingAccent: "#b8b36c",
    });
  });

  it("eli-valen", () => {
    expect(appearanceForNpc("eli-valen")).toEqual({
      skin: "#754331",
      hair: "#77706a",
      hairStyle: "parted",
      clothing: "#8a5d45",
      clothingAccent: "#9d8cc0",
    });
  });

  it("is stable across repeated calls for the same id (deterministic, not random)", () => {
    expect(appearanceForNpc("web-npc-alpha")).toEqual(appearanceForNpc("web-npc-alpha"));
  });
});
