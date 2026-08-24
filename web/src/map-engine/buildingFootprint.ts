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
  stoneWall: "#8f8a7a",
  woodWall: "#93653e",
  door: "#5c3826",
  floor: "#6f3f35",
};

const ROOF_COLORS = ["#7f4038", "#8f4b3e", "#6f4740", "#8a583c", "#735246"] as const;

function hashString(s: string): number {
  let h = 0;
  for (let i = 0; i < s.length; i++) {
    h = (h * 31 + s.charCodeAt(i)) | 0;
  }
  return Math.abs(h);
}

export function roofColorFor(identity: string): string {
  return ROOF_COLORS[hashString(identity) % ROOF_COLORS.length];
}

/**
 * Planta determinística por prédio: retângulo ou L (nunca aleatório de verdade — mesmo
 * `buildingId` sempre gera a mesma forma), com anel de parede (material por
 * `buildingTypeId` par/ímpar) e uma porta na borda inferior. O andar observado não participa
 * da identidade física: trocar Z nunca move parede ou porta.
 */
export function generateBuildingFootprint(
  _buildingId: string,
  buildingTypeId: number,
  _floor = 0,
  orientation = 0,
): FootprintCell[] {
  const wallMaterial: BuildingMaterial = buildingTypeId % 2 === 0 ? "stoneWall" : "woodWall";
  const typeVariant = Math.abs(buildingTypeId);
  const width = buildingTypeId === -1 ? 3 : 3 + (typeVariant % 2);
  const height = width;
  const isLShape = buildingTypeId !== -1 && typeVariant > 0 && typeVariant % 7 === 0 && width === 4;

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

  const normalizedOrientation = ((orientation % 360) + 360) % 360;
  return cells.map((cell) => {
    if (normalizedOrientation === 90) return { ...cell, x: height - 1 - cell.y, y: cell.x };
    if (normalizedOrientation === 180) return { ...cell, x: width - 1 - cell.x, y: height - 1 - cell.y };
    if (normalizedOrientation === 270) return { ...cell, x: cell.y, y: width - 1 - cell.x };
    return cell;
  });
}

/**
 * Feedback do usuário (2026-08-07): cidade no mapa-múndi também não pode ser só um retângulo —
 * mesma técnica do prédio (anel de parede + um portão), escala do tamanho real da cidade
 * (`bounds`). Sem preenchimento de piso (cidade é grande, o interior é onde os prédios da
 * `CityView` aparecem) — só o contorno.
 *
 * O nível Z é somente a camada observada. Ele nunca participa da seed da muralha ou do portão.
 */
export function generateCityWallFootprint(cityId: string, width: number, height: number, _floor = 0): FootprintCell[] {
  const seed = hashString(cityId);
  const cells: FootprintCell[] = [];
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const onEdge = x === 0 || x === width - 1 || y === 0 || y === height - 1;
      const clippedCorner =
        width >= 6 && height >= 6 &&
        ((x + y < 2) || (width - 1 - x + y < 2) || (x + height - 1 - y < 2) || (width - 1 - x + height - 1 - y < 2));
      if (onEdge && !clippedCorner) {
        cells.push({ x, y, material: "stoneWall" });
      } else if (!clippedCorner) {
        const street = x === Math.floor(width / 2) || y === Math.floor(height / 2);
        const courtyard = hashString(`${cityId}:${x}:${y}`) % 11 === 0;
        if (!street && !courtyard) {
          cells.push({ x, y, material: "floor" });
        }
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
