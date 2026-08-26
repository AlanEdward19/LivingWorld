import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Demo isolada (fase 16.3-web) — zero rede, zero proxy pra API real (ver spec.md Out of Scope).
export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    setupFiles: ["./tests/setup.ts"],
    globals: true,
  },
});
