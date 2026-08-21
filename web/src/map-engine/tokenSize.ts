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
