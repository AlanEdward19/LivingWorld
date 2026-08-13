/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string;
  /** Fase 15.1, T31: liga o composition root (main.tsx) nos `Mock*Source` em vez dos `Real*Source`
   *  — modo de demo offline (spec.md T27), sem backend nenhum rodando. Ausente/"false" = real. */
  readonly VITE_DEMO_MODE?: string;
}
