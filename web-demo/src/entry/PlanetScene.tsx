export type PlanetVariant = "proto-world" | "inhabited";

/**
 * Doc §12-15 — stylized planet, not a 3D demo. CSS radial-gradient sphere with a slow rotation
 * highlight; `prefers-reduced-motion` disables the animation via `tokens.css`, not JS (native
 * feature, doc §15). Purely decorative — `aria-hidden` (doc §89).
 */
export function PlanetScene({ variant, worldName }: { variant: PlanetVariant; worldName?: string }) {
  return (
    <div data-testid="planet-scene" data-variant={variant} aria-hidden="true">
      <div data-testid="planet-sphere" />
      <div data-testid="planet-atmosphere" />
      {worldName && <div data-testid="planet-label">{worldName}</div>}
    </div>
  );
}
