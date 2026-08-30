import { useEffect, useRef } from "react";

const MAX_DIM = 48;

// Real backend odds (`src/LivingWorld.Simulation` MapGenerator — see agent research this round):
// fixed 10% per-cell water chance, ~30% per-cell chance of a resource on land. Not configurable
// in the engine, so this preview can't diverge from them without lying about the real generator.
const WATER_CHANCE = 0.1;
const RESOURCE_CHANCE = 0.3;

// Stand-in palette for TerrainIds — the real ids are catalog-defined per scenario (arbitrary
// ints, no fixed colors anywhere in the engine), so there's nothing "real" to draw here beyond
// the shape of the rule: a uniform-random draw across a fixed-size bucket of terrain looks.
const TERRAIN_COLORS = ["#3a4a2e", "#4a5a35", "#5a6b3f", "#6b5a3a", "#4a3f2e", "#3f4a3a"];
const WATER_COLOR = "#24404f";
const RESOURCE_TINT = "rgba(230, 200, 140, 0.55)";

/** Mulberry32 — small deterministic PRNG so the same seed always draws the same map, and the
    preview visibly changes the instant the user edits Seed/Width/Height. */
function mulberry32(seed: number) {
  let a = seed >>> 0;
  return () => {
    a |= 0;
    a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function seedToUint32(seed: string): number {
  // Real backend seed is a ulong; fold it down to a uint32 PRNG seed via modulo.
  try {
    return Number(BigInt(seed || "0") % 4294967296n);
  } catch {
    return 0;
  }
}

/**
 * Doc-requested (user): the backend map is really a grid of tiles, not a sphere — this renders
 * a downsampled preview (capped at `MAX_DIM` per side, doesn't need exact Width×Height proportion
 * per the user's own ask) of what `MapGenerator` would roughly produce for the current draft.
 */
export function TileMapPreview({ width, height, seed }: { width: number; height: number; seed: string }) {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    // jsdom (this repo's test runner) has no canvas 2D backend and throws rather than
    // returning null — real browsers never throw here, but the guard costs nothing.
    let ctx: CanvasRenderingContext2D | null = null;
    try {
      ctx = canvas.getContext("2d");
    } catch {
      return;
    }
    if (!ctx) return;

    const aspect = width > 0 && height > 0 ? width / height : 1;
    const cols = Math.max(4, Math.round(aspect >= 1 ? MAX_DIM : MAX_DIM * aspect));
    const rows = Math.max(4, Math.round(aspect >= 1 ? MAX_DIM / aspect : MAX_DIM));
    canvas.width = cols;
    canvas.height = rows;

    const random = mulberry32(seedToUint32(seed));
    for (let y = 0; y < rows; y++) {
      for (let x = 0; x < cols; x++) {
        const isWater = random() < WATER_CHANCE;
        ctx.fillStyle = isWater ? WATER_COLOR : TERRAIN_COLORS[Math.floor(random() * TERRAIN_COLORS.length)];
        ctx.fillRect(x, y, 1, 1);
        if (!isWater && random() < RESOURCE_CHANCE) {
          ctx.fillStyle = RESOURCE_TINT;
          ctx.fillRect(x, y, 1, 1);
        }
      }
    }
  }, [width, height, seed]);

  return (
    <div data-testid="tile-map-preview" aria-hidden="true">
      <canvas ref={canvasRef} style={{ aspectRatio: `${width || 1} / ${height || 1}` }} />
      <span data-testid="tile-map-caption">
        {width} × {height} tiles
      </span>
    </div>
  );
}
