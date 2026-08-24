// @vitest-environment node
import { describe, expect, it } from "vitest";
import config from "../vite.config";

describe("Vite development API proxy", () => {
  it("forwards NPC authoring requests to the real API", () => {
    expect(config).toMatchObject({
      server: {
        proxy: {
          "/authoring": { target: "http://localhost:5289" },
        },
      },
    });
  });
});
