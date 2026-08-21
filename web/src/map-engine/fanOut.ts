// Feedback do usuário (2026-08-21): clique num NPC que divide tile com outro (posições
// espalhadas visualmente por `fanOutOffsets`) não "pegava" — o renderer desenhava a posição
// deslocada, mas o hit-test comparava contra a posição autoritativa crua, então todos os NPCs
// do mesmo tile colidiam no mesmo ponto de acerto e só o primeiro iterado era clicável. Extraído
// de `renderer.ts` para `renderer.ts` (desenho) e `hitTest.ts` (clique) usarem exatamente o
// mesmo deslocamento — nunca a posição autoritativa em si, só o desenho e o alvo do clique.
import type { AuthoritativeEntity, Vec2 } from "./types";

/** Só entidades que dividem exatamente a mesma célula ganham deslocamento — sorteio determinístico
 * (ordenado por id) num pequeno círculo, então o mesmo grupo sempre se organiza igual entre
 * frames/replays. */
export function fanOutOffsets(entities: AuthoritativeEntity[]): Map<string, Vec2> {
  const byCell = new Map<string, AuthoritativeEntity[]>();
  for (const entity of entities) {
    const key = `${Math.floor(entity.position.x)}:${Math.floor(entity.position.y)}`;
    const group = byCell.get(key);
    if (group) group.push(entity);
    else byCell.set(key, [entity]);
  }

  const offsets = new Map<string, Vec2>();
  const radius = 0.34;
  for (const group of byCell.values()) {
    if (group.length <= 1) continue;
    const sorted = [...group].sort((a, b) => a.ref.id.localeCompare(b.ref.id));
    sorted.forEach((entity, index) => {
      const angle = (2 * Math.PI * index) / sorted.length;
      offsets.set(entity.ref.id, { x: Math.cos(angle) * radius, y: Math.sin(angle) * radius });
    });
  }
  return offsets;
}
