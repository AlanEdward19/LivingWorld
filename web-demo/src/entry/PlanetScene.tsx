import type { CSSProperties } from "react";

export type PlanetVariant = "proto-world" | "inhabited";

/**
 * Doc §12-15 — stylized planet, not a 3D demo. CSS radial-gradient sphere with a slow rotation
 * highlight; `prefers-reduced-motion` disables the animation via `tokens.css`, not JS (native
 * feature, doc §15). Purely decorative — `aria-hidden` (doc §89).
 */
export function PlanetScene({
  variant,
  worldName,
  hueRotate = 0,
}: {
  variant: PlanetVariant;
  worldName?: string;
  /** Deterministic per-world color shift (see `hueForWorldId`) — lets the Worlds Library swap
      which save the planet represents without needing separate art per world. */
  hueRotate?: number;
}) {
  return (
    <div data-testid="planet-scene" data-variant={variant} aria-hidden="true" style={{ "--planet-hue": `${hueRotate}deg` } as CSSProperties}>
      {/* The idle drift animation lives on this inner layer, never on `planet-scene` itself —
          `transition`-ing a property on the same element that a CSS `animation` was JUST
          removed from (`animation: none` + `transition: transform` in one style recalc) snaps
          straight to the end value instead of interpolating. Keeping the animated and the
          transitioned transform on two different elements sidesteps that entirely. */}
      <div data-testid="planet-drift-layer">
        <div data-testid="planet-sphere" />
        <div data-testid="planet-atmosphere" />
        {worldName && <div data-testid="planet-label">{worldName}</div>}
        {/* Doc-requested "camera dive" on Continue — a color-matched wash that blooms past the
            viewport, not the sphere itself scaled up (a flat CSS gradient blown up 9x just reads
            as a blurry smear of one blown-up patch, not a planet rushing at the camera). */}
        <div data-testid="planet-zoom-flash" />
      </div>
    </div>
  );
}

/** Small stable hash so the same world id always gets the same hue. */
export function hueForWorldId(id: string): number {
  let hash = 0;
  for (let i = 0; i < id.length; i++) hash = (hash * 31 + id.charCodeAt(i)) % 360;
  return hash;
}
