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
}
