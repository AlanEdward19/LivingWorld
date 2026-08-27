import { describe, expect, it, vi } from "vitest";
import { FollowStore } from "../../src/state/followStore";

describe("FollowStore", () => {
  it("starts with nothing followed", () => {
    const store = new FollowStore();
    expect(store.isFollowed("mira-valen")).toBe(false);
  });

  it("toggleFollow marks an entity as followed", () => {
    const store = new FollowStore();
    store.toggleFollow("mira-valen");
    expect(store.isFollowed("mira-valen")).toBe(true);
  });

  it("toggleFollow on an already-followed entity un-follows it (toggle, not duplicate)", () => {
    const store = new FollowStore();
    store.toggleFollow("mira-valen");
    store.toggleFollow("mira-valen");
    expect(store.isFollowed("mira-valen")).toBe(false);
  });

  it("does not affect other entities' followed state", () => {
    const store = new FollowStore();
    store.toggleFollow("mira-valen");
    expect(store.isFollowed("oakbridge")).toBe(false);
  });

  it("follow state persists across separate reads (survives 'navigating away and back' since it's outside React lifecycle)", () => {
    const store = new FollowStore();
    store.toggleFollow("mira-valen");
    // Simula "sair da tela e voltar" — nada aqui reseta o store, só leituras subsequentes.
    expect(store.isFollowed("mira-valen")).toBe(true);
    expect(store.isFollowed("mira-valen")).toBe(true);
  });

  it("never mutates the fixture — FollowStore holds no fixture reference at all", () => {
    const store = new FollowStore();
    expect(Object.keys(store)).not.toContain("fixture");
  });

  it("notifies subscribers on toggle", () => {
    const store = new FollowStore();
    const listener = vi.fn();
    store.subscribe(listener);
    store.toggleFollow("mira-valen");
    expect(listener).toHaveBeenCalledTimes(1);
  });

  it("followedIds() lists every currently-followed id", () => {
    const store = new FollowStore();
    store.toggleFollow("mira-valen");
    store.toggleFollow("oakbridge");
    expect(store.followedIds().sort()).toEqual(["mira-valen", "oakbridge"].sort());
  });

  it("followedIds() drops an id once it's un-followed", () => {
    const store = new FollowStore();
    store.toggleFollow("mira-valen");
    store.toggleFollow("mira-valen");
    expect(store.followedIds()).toEqual([]);
  });
});
