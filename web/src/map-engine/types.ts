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
  /**
   * Forma real do footprint por célula, relativa a `position` (feedback do usuário — cidade/
   * prédio não são um retângulo uniforme, "igual wireframe" com material por célula:
   * `web/src/map-engine/buildingFootprint.ts`). Quando presente, o renderer desenha cada
   * célula pelo seu próprio material em vez do retângulo único de `size`.
   */
  footprintCells?: { x: number; y: number; color: string }[];
  /**
   * Puramente visual — desenha mas nunca participa de hit-test/seleção (feedback do usuário:
   * fundo "ambiente" ao redor do grid do interior de um prédio, mesma aparência de fora,
   * transparente escura — não pode roubar clique do piso real por ficar concêntrica com ele).
   */
  decorative?: boolean;
}
