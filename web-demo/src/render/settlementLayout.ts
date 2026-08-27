import type { BuildingFixture } from "../fixture/types";

export interface Footprint {
  width: number;
  height: number;
}

/**
 * Footprint (em tiles) de um prédio no Settlement View — DERIVADO da contagem de cômodos do
 * próprio fixture, não um número inventado por prédio. Prédios sem interior modelado (ex.:
 * North Farm, `floors: []`) ganham um footprint retangular "de campo", maior que uma casinha
 * de 1 tile, em vez de virar um quadrado simbólico igual a todos os outros (doc: "cidade não
 * pode parecer um diorama").
 */
export function buildingFootprint(building: Pick<BuildingFixture, "floors">): Footprint {
  const totalRooms = building.floors.reduce((sum, floor) => sum + floor.rooms.length, 0);
  if (totalRooms === 0) return { width: 4, height: 3 };
  const side = Math.min(5, Math.max(2, Math.ceil(Math.sqrt(totalRooms)) + 1));
  return { width: side, height: side };
}

export interface RoadSegment {
  from: { x: number; y: number };
  to: { x: number; y: number };
}

/**
 * Rede de "estradas" puramente decorativa/de layout (AD-020 trade-off) — liga o centro de cada
 * prédio a um hub central (centroide dos prédios). NÃO é dado canônico de nenhum sistema, só dá
 * ao settlement uma leitura espacial coerente (doc: "market square" conectando os edifícios) em
 * vez de prédios soltos num vazio. Determinístico: mesma entrada, mesma saída sempre.
 */
export function generateRoads(buildings: { gridPosition: { x: number; y: number } }[]): RoadSegment[] {
  if (buildings.length === 0) return [];
  const hub = {
    x: buildings.reduce((sum, b) => sum + b.gridPosition.x, 0) / buildings.length,
    y: buildings.reduce((sum, b) => sum + b.gridPosition.y, 0) / buildings.length,
  };
  return buildings.map((b) => ({ from: hub, to: b.gridPosition }));
}

function fnv1a(text: string): number {
  let value = 0x811c9dc5;
  for (let index = 0; index < text.length; index += 1) {
    value ^= text.charCodeAt(index);
    value = Math.imul(value, 0x01000193);
  }
  return value >>> 0;
}

/**
 * Ruído procedural determinístico [0, 1) por tile de terreno (doc: "procedural noise, subtle
 * tile variation" em vez de terreno chapado) — mesmo `seed`+coordenada sempre dá o mesmo valor,
 * então o terreno não "pisca" a cada re-render.
 */
export function tileNoise(gridX: number, gridY: number, seed: string): number {
  return (fnv1a(`${seed}:${gridX}:${gridY}`) % 1000) / 1000;
}
