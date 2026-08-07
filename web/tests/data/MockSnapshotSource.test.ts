import { describe, expect, it } from "vitest";
import { MockSnapshotSource } from "../../src/data/mock/MockSnapshotSource";
import { snapshotsByScope } from "../../src/data/mock/fixtures";
import { VisualScopeKind } from "../../src/types";

describe("MockSnapshotSource", () => {
  const source = new MockSnapshotSource(snapshotsByScope);

  it("resolves the World scope fixture", async () => {
    const envelope = await source.load({ kind: "World" });
    expect(envelope.scope.kind).toBe(VisualScopeKind.World);
    expect(envelope.payload).not.toBeNull();
  });

  it("resolves the City scope fixture by cityId", async () => {
    const envelope = await source.load({ kind: "City", cityId: "city-a" });
    expect(envelope.scope.kind).toBe(VisualScopeKind.City);
    expect(envelope.scope.refId).toBe("city-a");
  });

  it("resolves the Building scope fixture by buildingId", async () => {
    const envelope = await source.load({ kind: "Building", buildingId: "2000", cityId: "city-a" });
    expect(envelope.scope.kind).toBe(VisualScopeKind.Interior);
  });

  it("rejects a scope with no matching fixture", async () => {
    await expect(source.load({ kind: "City", cityId: "does-not-exist" })).rejects.toThrow();
  });
});
