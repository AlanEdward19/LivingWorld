// Feedback do usuário (2026-08-07): prédio precisa de forma real (não círculo, não retângulo
// uniforme) e materiais distintos ("parede de pedra uma cor, madeira outra, porta etc — igual
// wireframe"). O domínio não modela footprint de prédio nem material de parede (context.md gap
// 5) — em vez de esperar essa fase do motor, o cliente gera uma planta determinística por
// prédio agora; quando o motor tiver o dado real, este módulo é o único lugar a trocar (mesma
// interface `FootprintCell[]`, os consumidores — CityView, BuildingSpace — não mudam).
export type BuildingMaterial = "stoneWall" | "woodWall" | "door" | "floor";

export interface FootprintCell {
  x: number;
  y: number;
  material: BuildingMaterial;
}

export const MATERIAL_COLOR: Record<BuildingMaterial, string> = {
  stoneWall: "#8a8f9c",
  woodWall: "#a97c50",
  door: "#e0645a",
  floor: "#2a3142",
};

function hashString(s: string): number {
  let h = 0;
  for (let i = 0; i < s.length; i++) {
    h = (h * 31 + s.charCodeAt(i)) | 0;
  }
  return Math.abs(h);
}

/**
 * Planta determinística por prédio: retângulo ou L (nunca aleatório de verdade — mesmo
 * `buildingId`+`floor` sempre gera a mesma forma), com anel de parede (material por
 * `buildingTypeId` par/ímpar) e uma porta na borda inferior. `floor` participa da seed — cada
 * andar (feedback "quero andares, Z") tem uma planta ligeiramente diferente, determinística.
 */
export function generateBuildingFootprint(buildingId: string, buildingTypeId: number, floor = 0): FootprintCell[] {
  const seed = hashString(`${buildingId}:${floor}`);
  const wallMaterial: BuildingMaterial = buildingTypeId % 2 === 0 ? "stoneWall" : "woodWall";
  const width = 4 + (seed % 3); // 4..6
  const height = 3 + ((seed >> 3) % 3); // 3..5
  const isLShape = seed % 5 === 0;

  const inBase = (x: number, y: number) => x >= 0 && x < width && y >= 0 && y < height;
  const inNotch = (x: number, y: number) => isLShape && x >= Math.floor(width / 2) && y >= Math.floor(height / 2);
  const inShape = (x: number, y: number) => inBase(x, y) && !inNotch(x, y);

  const cells: FootprintCell[] = [];
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      if (!inShape(x, y)) {
        continue;
      }
      const isWall = !inShape(x - 1, y) || !inShape(x + 1, y) || !inShape(x, y - 1) || !inShape(x, y + 1);
      cells.push({ x, y, material: isWall ? wallMaterial : "floor" });
    }
  }

  // Porta: substitui a célula de parede do meio da borda inferior por uma abertura.
  const doorX = isLShape ? Math.floor(width / 4) : Math.floor(width / 2);
  const doorIndex = cells.findIndex((c) => c.x === doorX && c.y === height - 1);
  if (doorIndex >= 0) {
    cells[doorIndex] = { x: doorX, y: height - 1, material: "door" };
  }

  return cells;
}

/**
 * Feedback do usuário (2026-08-07): cidade no mapa-múndi também não pode ser só um retângulo —
 * mesma técnica do prédio (anel de parede + um portão), escala do tamanho real da cidade
 * (`bounds`). Sem preenchimento de piso (cidade é grande, o interior é onde os prédios da
 * `CityView` aparecem) — só o contorno.
 *
 * `floor` (2026-08-07, segunda rodada — "o Z não é só em prédio, é em tudo") participa da seed
 * igual `generateBuildingFootprint`: cada nível (subsolo/superfície/acima) tem seu próprio
 * portão determinístico, mesmo espírito de placeholder honesto client-side.
 */
export function generateCityWallFootprint(cityId: string, width: number, height: number, floor = 0): FootprintCell[] {
  const seed = hashString(`${cityId}:${floor}`);
  const cells: FootprintCell[] = [];
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const onEdge = x === 0 || x === width - 1 || y === 0 || y === height - 1;
      if (onEdge) {
        cells.push({ x, y, material: "stoneWall" });
      }
    }
  }

  // Portão: uma célula da parede vira abertura, no meio de um dos 4 lados (determinístico por cidade).
  const gateSide = seed % 4;
  const gate =
    gateSide === 0
      ? { x: Math.floor(width / 2), y: 0 }
      : gateSide === 1
        ? { x: width - 1, y: Math.floor(height / 2) }
        : gateSide === 2
          ? { x: Math.floor(width / 2), y: height - 1 }
          : { x: 0, y: Math.floor(height / 2) };
  const gateIndex = cells.findIndex((c) => c.x === gate.x && c.y === gate.y);
  if (gateIndex >= 0) {
    cells[gateIndex] = { ...gate, material: "door" };
  }

  return cells;
}
