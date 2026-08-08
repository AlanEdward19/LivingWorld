export interface GroundVisual {
  color: string;
  detail: "grass" | "soil";
  variant: number;
}

export interface CloudPuff {
  x: number;
  y: number;
  radius: number;
}

export interface CityGroundBounds {
  width: number;
  height: number;
  minX: number;
  minY: number;
}

function hash(value: string): number {
  let result = 2166136261;
  for (let index = 0; index < value.length; index += 1) {
    result ^= value.charCodeAt(index);
    result = Math.imul(result, 16777619);
  }
  return result >>> 0;
}

const GRASS = ["#566f43", "#597247", "#5d7549", "#536c40"] as const;
const SOIL = ["#765f43", "#80694a"] as const;

/** Paisagem cosmética: pura e reproduzível, sem tocar no estado canônico. */
export function cityGroundAt(spaceId: string, x: number, y: number): GroundVisual {
  const value = hash(`${spaceId}:${x}:${y}`);
  const isSoil = value % 37 === 0;
  return {
    color: isSoil ? SOIL[value % SOIL.length] : GRASS[value % GRASS.length],
    detail: isSoil ? "soil" : "grass",
    variant: value,
  };
}

/** Envelope visual finito da cidade; fora dele volta a aparecer o céu do mapa. */
export function cityGroundBounds(location: { x: number; y: number }): CityGroundBounds {
  const width = 34;
  const height = 24;
  return {
    width,
    height,
    minX: Math.floor(location.x - width / 2),
    minY: Math.floor(location.y - height / 2),
  };
}

export function cloudPuffs(spaceId: string, width: number, height: number): CloudPuff[] {
  return Array.from({ length: 5 }, (_, index) => {
    const value = hash(`${spaceId}:cloud:${index}`);
    return {
      x: ((value & 0xffff) / 0xffff) * width,
      y: (((value >>> 16) & 0xffff) / 0xffff) * height,
      radius: 18 + (value % 24),
    };
  });
}
