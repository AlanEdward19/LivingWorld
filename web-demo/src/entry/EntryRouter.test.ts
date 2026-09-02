import { describe, expect, it } from "vitest";
import { EntryRouter } from "./EntryRouter";

describe("EntryRouter", () => {
  it("parses /sandbox into the sandbox screen", () => {
    window.history.pushState(null, "", "/sandbox");
    const router = new EntryRouter();
    router.start();
    expect(router.current()).toEqual({ kind: "sandbox" });
    router.stop();
  });

  it("navigate('/sandbox') updates the current screen", () => {
    window.history.pushState(null, "", "/");
    const router = new EntryRouter();
    router.start();
    router.navigate("/sandbox");
    expect(router.current()).toEqual({ kind: "sandbox" });
    router.stop();
  });
});
