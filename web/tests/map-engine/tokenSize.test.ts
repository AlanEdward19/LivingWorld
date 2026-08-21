import { describe, expect, it } from "vitest";
import { npcVisualScale, pawnHitCoverageRadius, tokenRadiusPx } from "../../src/map-engine/tokenSize";

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

// Feedback do usuário (2026-08-21, 3ª rodada, verificado ao vivo no browser): igualar o raio de
// acerto ao raio de desenho ainda não bastava — o pawn (`drawNpcPawn`, renderer.ts) desenha um
// RETÂNGULO alto (largura 2r, altura 2.4r, topo em -1.25r a partir do centro), não um círculo.
// Clicar na cabeça/torso visível (que fica ACIMA do centro) caía fora de um círculo de raio r,
// e o clique "pegava" outro NPC próximo por menor distância — parecia escolha aleatória.
describe("pawnHitCoverageRadius", () => {
  it("covers the full drawn rectangle (width 2r, height 2.4r, top offset 1.25r), not just r", () => {
    const r = 10;
    const covered = pawnHitCoverageRadius(r);

    expect(covered).toBeGreaterThan(r);
    // maior extensão vertical é o topo (1.25r) -> raio = hipotenusa(1r, 1.25r)
    expect(covered).toBeCloseTo(Math.hypot(1, 1.25) * r, 5);
  });

  it("scales linearly with the base radius", () => {
    expect(pawnHitCoverageRadius(20)).toBeCloseTo(pawnHitCoverageRadius(10) * 2, 5);
  });
});
