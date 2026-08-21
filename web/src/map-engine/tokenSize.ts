// Feedback do usuário (2026-08-21): o token de NPC tinha raio de tela FIXO por nível de LOD
// (não acompanhava `scale`/zoom, decisão documentada em `renderer.ts` T35) — dar zoom pra ver
// o NPC de perto não fazia diferença nenhuma no tamanho dele. Agora o raio cresce com `scale`
// (pixels de tela por unidade de mundo), com piso/teto só pra evitar sumir num zoom bem afastado
// ou virar um círculo absurdo num zoom extremo. `renderer.ts` (desenho) e `MapView.tsx`
// (raio de acerto do clique) leem daqui — nunca duas fórmulas que podem desalinhar de novo.
export function tokenRadiusPx(scale: number): number {
  return Math.min(60, Math.max(6, scale * 0.35));
}
