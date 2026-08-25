import type { ExtraordinaryNpcVisual } from "../types";

// Fase 15.1, T0: tipos centrais do map engine (design.md "Data Models").
// SpaceId modela a hierarquia WorldSpace > CitySpace > BuildingSpace (master prompt §9);
// distinto de FocusScope (types.ts) só por nomear "Building" em vez de "Interior" — a
// conciliação dos dois vive em T9 (SpatialContext), fora do escopo desta task.

export interface Vec2 {
  x: number;
  y: number;
}

export interface Rect {
  x: number;
  y: number;
  width: number;
  height: number;
}

/** Tamanho do espaço em tiles, origem em (0,0) — usado por `Camera.clampTo`. */
export interface SpaceBounds {
  width: number;
  height: number;
}

export type SpaceId =
  | { kind: "World" }
  | { kind: "City"; cityId: string }
  | { kind: "Building"; buildingId: string; cityId: string };

export interface CameraState {
  /** centro do viewport em coordenadas de mundo do espaço atual */
  center: { x: number; y: number };
  /** pixels de tela por tile de mundo */
  scale: number;
}

export type EntityRotation = 0 | 90 | 180 | 270;

export interface EntityRef {
  kind: "npc" | "city" | "building" | "cell";
  id: string;
  space: SpaceId;
}

/** Posição autoritativa vinda do motor — nunca escrita pelo cliente. */
export interface AuthoritativeEntity {
  ref: EntityRef;
  position: { x: number; y: number };
  /** footprint em tiles do espaço; vem de `Bounds` da projeção (OQ-1), 1x1 quando ausente */
  size: { w: number; h: number };
  /** true quando `size` é derivado/aproximado e não autorado no domínio */
  sizeIsDerived: boolean;
  color: string;
  /** Tipo autoritativo usado somente para escolher a aparência arquitetônica no cliente. */
  buildingTypeId?: number;
  /** Rótulo visual opcional fornecido pela projeção/editor; fallback é kind + id. */
  label?: string;
  /** Estado visual observado; não participa de movimento, hit-test ou identidade. */
  currentAction?: number | null;
  /** Estado extraordinário projetado; renderer só o consome durante manifestação ativa. */
  extraordinary?: ExtraordinaryNpcVisual | null;
  /**
   * Forma real do footprint por célula, relativa a `position` (feedback do usuário — cidade/
   * prédio não são um retângulo uniforme, "igual wireframe" com material por célula:
   * `web/src/map-engine/buildingFootprint.ts`). Quando presente, o renderer desenha cada
   * célula pelo seu próprio material em vez do retângulo único de `size`.
   */
  footprintCells?: {
    x: number;
    y: number;
    color: string;
    material?: "stoneWall" | "woodWall" | "door" | "floor" | "roof";
  }[];
  /**
   * Puramente visual — desenha mas nunca participa de hit-test/seleção (feedback do usuário:
   * fundo "ambiente" ao redor do grid do interior de um prédio, mesma aparência de fora,
   * transparente escura — não pode roubar clique do piso real por ficar concêntrica com ele).
   */
  decorative?: boolean;
  /** O editor pode mostrar o conjunto de telhados sem a muralha/caixa externa do mapa observado. */
  showBoundary?: boolean;
  /** Orientação visual horária usada pela autoria antes de existir contrato canônico na API. */
  rotation?: EntityRotation;
  /** Stage 4 process overlay (construction scaffold, later crop/water). */
  process?: { kind: string; progress: number; accessibleLabel: string; descriptorKey?: string };
  /** Inter-city household relocation target (world map route). Absent for intra-city commute. */
  travelDestination?: { x: number; y: number };
  cityId?: string;
  /**
   * Direção do deslocamento visual em curso, vinda de `InterpolationBuffer.directionOf` (fase 17:
   * "andar é uma animação") — `null`/ausente quando parado. Puramente cosmético (espelha o pawn,
   * conduz o bob de andar); nunca participa de hit-test/seleção.
   */
  facing?: { x: number; y: number } | null;
}
