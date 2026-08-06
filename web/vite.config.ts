import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Fase 15, T8: proxy de dev pra API real (mesma origem do browser, sem CORS) — em prod o
// cliente é servido pela própria API ou aponta pra VITE_API_BASE_URL.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      "/visual": { target: "http://localhost:5289", ws: true },
      "/worlds": { target: "http://localhost:5289" },
    },
  },
  test: {
    environment: "jsdom",
    setupFiles: ["./tests/setup.ts"],
    globals: true,
  },
});
