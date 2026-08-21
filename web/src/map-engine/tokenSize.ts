import type { SpaceId } from "./types";

// Feedback do usuário (2026-08-21): o token de NPC tinha raio de tela FIXO por nível de LOD
// (não acompanhava `scale`/zoom, decisão documentada em `renderer.ts` T35) — dar zoom pra ver
// o NPC de perto não fazia diferença nenhuma no tamanho dele. Agora o raio cresce com `scale`
// (pixels de tela por unidade de mundo), com piso/teto só pra evitar sumir num zoom bem afastado
// ou virar um círculo absurdo num zoom extremo. `renderer.ts` (desenho) e `MapView.tsx`
// (raio de acerto do clique) leem daqui — nunca duas fórmulas que podem desalinhar de novo.
//
// Feedback do usuário (2026-08-21, 2ª rodada): fator 0.35/teto 60 deixava o token GIGANTE já no
// zoom padrão de uma cidade (`computeFitZoom` de uma grade pequena numa tela grande facilmente
// passa de escala 50-90) — e, pior, o clique ficava impossível porque o raio de acerto (fórmula
// própria em MapView) não sabia do multiplicador por espaço (`npcVisualScale`: cidade 1.65x,
// prédio 2.2x), então o círculo de clique ficava bem menor que o token visível. Fator/teto
// menores mantêm o tamanho padrão parecido com o fixo antigo (~6-8px) e só crescem de verdade
// quando o usuário de fato aproxima bastante.
export function tokenRadiusPx(scale: number): number {
  return Math.min(22, Math.max(6, scale * 0.08));
}

/**
 * Multiplicador de tamanho visual do pawn de NPC por nível espacial (mundo compacto, cidade
 * aproxima, interior aproxima mais) — mesma regra em `renderer.ts` (desenho) e `MapView.tsx`
 * (raio de acerto do clique), pra nunca desalinhar de novo.
 */
export function npcVisualScale(spaceKind: SpaceId["kind"]): number {
  if (spaceKind === "Building") return 2.2;
  if (spaceKind === "City") return 1.65;
  return 1;
}

// Feedback do usuário (2026-08-21, 3ª rodada): mesmo com o raio de acerto igualando o raio de
// desenho, clicar no NPC "não pegava" e às vezes selecionava outro NPC "aleatório". Causa real:
// o pawn (`drawNpcPawn`, renderer.ts) desenha um RETÂNGULO alto (largura 2r, altura 2.4r, topo em
// -1.25r) centrado no pé/tile, não um círculo — o hit-test comparava distância até esse mesmo
// centro com raio = r, então clicar na cabeça/torso visível (que fica ACIMA do centro, fora do
// raio pequeno) sempre errava; e se outro NPC estivesse perto, a menor distância "vencia" o
// clique, parecendo escolha aleatória. O raio de acerto precisa cobrir o retângulo inteiro, não
// só metade da largura dele.
const PAWN_HALF_WIDTH_FACTOR = 1; // metade de 2r
const PAWN_TOP_OFFSET_FACTOR = 1.25;
const PAWN_BOTTOM_OFFSET_FACTOR = 2.4 - 1.25; // = 1.15

/** Raio do menor círculo (centrado no ponto de ancoragem do pawn) que cobre o retângulo inteiro
 * que `drawNpcPawn` desenha para um dado raio-base `r`. */
export function pawnHitCoverageRadius(r: number): number {
  const maxVertical = Math.max(PAWN_TOP_OFFSET_FACTOR, PAWN_BOTTOM_OFFSET_FACTOR);
  return Math.hypot(PAWN_HALF_WIDTH_FACTOR, maxVertical) * r;
}
