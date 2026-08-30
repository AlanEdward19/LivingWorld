import { useEffect, useMemo, useRef, useState } from "react";

const MAX_DIM = 48;
const MIN_ZOOM = 1;
const MAX_ZOOM = 10;

// Exact port of `WorldRng` (src/LivingWorld.Domain/WorldRng.cs) — splitmix64, seeded directly
// from the real ulong Seed (no truncation/hashing on the backend side, confirmed via
// MapScenarioLoader.cs:82). This is the ONLY RNG the real map generator uses (never
// System.Random — the engine's own doc comment explains why: no cross-runtime stability
// guarantee). Ported with BigInt for exact 64-bit wraparound arithmetic; a plain JS Number
// would silently lose precision on the multiplies and desync from the real sequence.
const GOLDEN64 = 0x9e3779b97f4a7c15n;
const MIX1 = 0xbf58476d1ce4e5b9n;
const MIX2 = 0x94d049bb133111ebn;
const MASK64 = 0xffffffffffffffffn;
const TWO_POW_53 = Math.pow(2, 53);

/** `NextDouble()` result for the i-th draw (0-based) from a `WorldRng(seed)` stream, computed
    directly from `seed` without replaying draws 0..i-1 — the SplitMix64 state only ever
    accumulates `+= GOLDEN64`, so the state before draw i is always `seed + (i+1)*GOLDEN64`.
    This is what makes sampling a handful of cells out of a 512x512 map cheap: each sampled
    cell's real value is computed in O(1), not by generating the whole map to throw most of it
    away. Only valid because the real per-cell draw count is currently constant (see CATALOG
    below) — a data-dependent draw (a real ResourceIds catalog) would break the O(1) jump and
    need sequential replay instead. */
/** Exported for `TileMapRng.test.ts` — cross-checked against a standalone run of the real C#
    `WorldRng` (same seed, same draw sequence) to confirm this port is bit-for-bit faithful. */
export function rngDoubleAt(seed: bigint, drawIndex: number): number {
  let state = (seed + BigInt(drawIndex + 1) * GOLDEN64) & MASK64;
  let z = state;
  z = ((z ^ (z >> 30n)) * MIX1) & MASK64;
  z = ((z ^ (z >> 27n)) * MIX2) & MASK64;
  z = z ^ (z >> 31n);
  return Number(z >> 11n) / TWO_POW_53;
}

// The real catalog every shipped period scenario uses today (scenarios/periods/*.json, all 5
// identical on this point): 3 terrain ids, 1 biome id, 0 resource ids. `MapGenerator.cs`'s
// per-cell draw order is terrain -> biome (skipped if BiomeIds is empty) -> altitude -> water ->
// resource (skipped entirely if ResourceIds is empty, via C#'s && short-circuit) — with
// ResourceIds always empty right now, resources never spawn, so this preview doesn't draw any.
// If a period ever ships a non-empty ResourceIds/BiomeIds catalog, this constant and the
// DRAWS_PER_CELL below need updating to match, and the O(1) jump above stops being valid.
const CATALOG = { terrainCount: 3, biomeCount: 1, resourceCount: 0 };
const DRAWS_PER_CELL = 1 /* terrain */ + (CATALOG.biomeCount > 0 ? 1 : 0) + 1 /* altitude */ + 1; /* water */

const TERRAIN_COLORS = ["#3a4a2e", "#5a6b3f", "#6b5a3a"];
const WATER_COLOR = "#24404f";

/** The real backend cell at (x, y) for a `width`x`height` map with this `seed` — computed
    without generating any other cell. Mirrors `MapGenerator.cs`'s per-cell field order exactly. */
function realCellAt(seed: bigint, width: number, x: number, y: number): { terrainIdx: number; isWater: boolean } {
  const base = (y * width + x) * DRAWS_PER_CELL;
  const terrainIdx = Math.floor(rngDoubleAt(seed, base) * CATALOG.terrainCount);
  const waterOffset = 1 /* terrain */ + (CATALOG.biomeCount > 0 ? 1 : 0) + 1; /* altitude */
  const isWater = rngDoubleAt(seed, base + waterOffset) < 0.1;
  return { terrainIdx, isWater };
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}

/**
 * Doc-requested (user): the backend map is really a grid of tiles, not a sphere — downsampled to
 * `MAX_DIM` cells per side for display (doesn't need to match Width×Height 1:1, per the user's
 * own ask), but each sampled cell's terrain/water is the REAL value `MapGenerator` would produce
 * at that coordinate for the current Seed — not an approximation drawn from a different RNG.
 * Wheel to zoom, drag to pan (user request) — the canvas is genuinely low-res (tile-per-pixel,
 * `image-rendering: pixelated`), so zooming in shows the actual tiles as blocks, not a blur.
 */
export function TileMapPreview({ width, height, seed }: { width: number; height: number; seed: string }) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const viewportRef = useRef<HTMLDivElement>(null);
  const [zoom, setZoom] = useState(1);
  const [pan, setPan] = useState({ x: 0, y: 0 });
  const dragRef = useRef<{ pointerId: number; startX: number; startY: number; panX: number; panY: number } | null>(null);

  // Never let the map drag past its own edge (user report: could throw the whole map off-screen
  // and be left looking at empty space). Bounds are half the overscan on each axis — at
  // zoom<=1 the canvas doesn't exceed the viewport at all, so bounds collapse to 0 and no pan is
  // possible, which is correct: nothing to reveal.
  function clampPanValue(next: { x: number; y: number }, z: number): { x: number; y: number } {
    const canvas = canvasRef.current;
    const viewport = viewportRef.current;
    if (!canvas || !viewport) return next;
    const maxX = Math.max(0, (canvas.clientWidth * z - viewport.clientWidth) / 2);
    const maxY = Math.max(0, (canvas.clientHeight * z - viewport.clientHeight) / 2);
    return { x: clamp(next.x, -maxX, maxX), y: clamp(next.y, -maxY, maxY) };
  }

  const seedBig = useMemo(() => {
    try {
      return BigInt(seed || "0");
    } catch {
      return 0n;
    }
  }, [seed]);

  useEffect(() => {
    setZoom(1);
    setPan({ x: 0, y: 0 });
  }, [width, height, seed]);

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
    if (!ctx || width <= 0 || height <= 0) return;

    const aspect = width / height;
    const cols = Math.max(4, Math.round(aspect >= 1 ? MAX_DIM : MAX_DIM * aspect));
    const rows = Math.max(4, Math.round(aspect >= 1 ? MAX_DIM / aspect : MAX_DIM));
    canvas.width = cols;
    canvas.height = rows;

    for (let py = 0; py < rows; py++) {
      // Nearest-neighbor sample into the real coordinate space — the point of this map is the
      // real generator's actual output at that spot, not a resampled/blended average of it.
      const ry = Math.min(height - 1, Math.floor((py / rows) * height));
      for (let px = 0; px < cols; px++) {
        const rx = Math.min(width - 1, Math.floor((px / cols) * width));
        const cell = realCellAt(seedBig, width, rx, ry);
        ctx.fillStyle = cell.isWater ? WATER_COLOR : TERRAIN_COLORS[cell.terrainIdx];
        ctx.fillRect(px, py, 1, 1);
      }
    }
  }, [width, height, seedBig]);

  function applyZoom(nextZoom: number) {
    const z = clamp(nextZoom, MIN_ZOOM, MAX_ZOOM);
    setZoom(z);
    // Zooming out shrinks the allowed range — re-clamp so a previously-valid pan doesn't leave
    // the map hanging off-edge at the new, smaller zoom.
    setPan((p) => clampPanValue(p, z));
  }

  function onWheel(event: React.WheelEvent<HTMLDivElement>) {
    event.preventDefault();
    applyZoom(zoom * (event.deltaY < 0 ? 1.15 : 1 / 1.15));
  }

  function onPointerDown(event: React.PointerEvent<HTMLDivElement>) {
    dragRef.current = { pointerId: event.pointerId, startX: event.clientX, startY: event.clientY, panX: pan.x, panY: pan.y };
    // jsdom (this repo's test runner) doesn't implement pointer capture at all.
    event.currentTarget.setPointerCapture?.(event.pointerId);
  }

  function onPointerMove(event: React.PointerEvent<HTMLDivElement>) {
    const drag = dragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) return;
    const next = { x: drag.panX + (event.clientX - drag.startX), y: drag.panY + (event.clientY - drag.startY) };
    setPan(clampPanValue(next, zoom));
  }

  function endDrag() {
    dragRef.current = null;
  }

  function resetView() {
    setZoom(1);
    setPan({ x: 0, y: 0 });
  }

  return (
    <div data-testid="tile-map-preview">
      <div
        ref={viewportRef}
        data-testid="tile-map-viewport"
        onWheel={onWheel}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={endDrag}
        onPointerLeave={endDrag}
        onPointerCancel={endDrag}
      >
        <canvas
          ref={canvasRef}
          aria-hidden="true"
          style={{
            aspectRatio: `${width || 1} / ${height || 1}`,
            transform: `translate(${pan.x}px, ${pan.y}px) scale(${zoom})`,
          }}
        />
      </div>

      <div data-testid="tile-map-controls">
        <button type="button" onClick={() => applyZoom(zoom / 1.4)} aria-label="Zoom out">
          −
        </button>
        <span data-testid="tile-map-zoom">{Math.round(zoom * 100)}%</span>
        <button type="button" onClick={() => applyZoom(zoom * 1.4)} aria-label="Zoom in">
          +
        </button>
        <button type="button" onClick={resetView}>
          Reset
        </button>
      </div>

      <span data-testid="tile-map-caption">
        {width} × {height} tiles
      </span>
    </div>
  );
}
