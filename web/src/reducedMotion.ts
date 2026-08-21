// Consulta `matchMedia` uma vez e cacheia — chamado por frame no loop de desenho do canvas
// (T8), então nunca pode custar mais que ler um booleano.
let cached: boolean | null = null;

export function prefersReducedMotion(): boolean {
  if (cached !== null) return cached;
  cached = typeof window !== "undefined" && typeof window.matchMedia === "function"
    ? window.matchMedia("(prefers-reduced-motion: reduce)").matches
    : false;
  return cached;
}
