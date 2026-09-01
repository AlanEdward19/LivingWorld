import "@testing-library/jest-dom/vitest";
import { vi } from "vitest";

// ViewStore posts observation scope on navigation (fire-and-forget). Node/jsdom fetch rejects
// relative URLs unless stubbed; individual suites override when they assert on fetch behavior.
vi.stubGlobal(
  "fetch",
  vi.fn().mockResolvedValue(new Response(null, { status: 200 })),
);

// jsdom não implementa canvas 2d — StartMenu já trata ctx null (não anima em teste), mas sem
// isso o jsdom loga "Not implemented: HTMLCanvasElement.prototype.getContext" em todo teste.
if (typeof HTMLCanvasElement !== "undefined")
  HTMLCanvasElement.prototype.getContext = () => null;
