import { describe, expect, it } from "vitest";
import { npcVisualScale, tokenRadiusPx } from "../../src/map-engine/tokenSize";

// Feedback do usuário (2026-08-21, 2ª rodada): fator/teto originais (0.35 / 60) deixavam o token
// GIGANTE já no zoom padrão de uma cidade pequena numa tela grande (computeFitZoom facilmente
// passa de escala 50-90) — o piso/teto atuais mantêm o tamanho padrão perto do fixo antigo (~8px)
// e só crescem de verdade além disso.
describe("tokenRadiusPx", () => {
  it("stays near the old fixed size (~6-8px) at typical city fit-to-screen scales", () => {
    expect(tokenRadiusPx(50)).toBeLessThanOrEqual(8);
    expect(tokenRadiusPx(90)).toBeLessThanOrEqual(8);
  });

  it("grows as scale increases, within its floor and ceiling", () => {
    expect(tokenRadiusPx(200)).toBeGreaterThan(tokenRadiusPx(100));
    expect(tokenRadiusPx(100)).toBeGreaterThan(tokenRadiusPx(50));
  });

  it("never exceeds a sane ceiling even at extreme zoom", () => {
    expect(tokenRadiusPx(10_000)).toBe(22);
  });

  it("never drops below a visible floor even at very low zoom", () => {
    expect(tokenRadiusPx(0)).toBe(6);
  });
});

// Feedback do usuário (2026-08-21, 2ª rodada): o raio de acerto do clique (MapView) usava uma
// folga fixa (1.3x) em vez do multiplicador exato do espaço — clicar num NPC de cidade/prédio
// quase nunca "pegava" porque o token desenhado (1.65x/2.2x) crescia mais que o círculo de
// clique. `npcVisualScale` é a única fonte do multiplicador agora (renderer.ts e MapView.tsx).
describe("npcVisualScale", () => {
  it("matches the exact per-space multiplier renderer.ts draws the pawn at", () => {
    expect(npcVisualScale("World")).toBe(1);
    expect(npcVisualScale("City")).toBe(1.65);
    expect(npcVisualScale("Building")).toBe(2.2);
  });
});
