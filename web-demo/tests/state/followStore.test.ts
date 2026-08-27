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

  // Pedido do usuário 2026-08-26: seguindo vários NPCs ao mesmo tempo, a câmera (fora deste
  // store, ver SettlementStage) só deve travar no ÚLTIMO seguido — não no primeiro por alguma
  // ordem arbitrária.
  describe("activeFollowId — camera-follow target with multiple bookmarks", () => {
    it("is null when nothing is followed", () => {
      const store = new FollowStore();
      expect(store.activeFollowId()).toBeNull();
    });

    it("is the most recently followed id", () => {
      const store = new FollowStore();
      store.toggleFollow("mira-valen");
      store.toggleFollow("rowan");
      expect(store.activeFollowId()).toBe("rowan");
    });

    it("falls back to the next-most-recent still-followed id once the active one is un-followed", () => {
      const store = new FollowStore();
      store.toggleFollow("mira-valen");
      store.toggleFollow("rowan");
      store.toggleFollow("rowan"); // un-follow rowan
      expect(store.activeFollowId()).toBe("mira-valen");
    });

    it("activate() switches the camera target to an already-followed id without un-following it", () => {
      const store = new FollowStore();
      store.toggleFollow("mira-valen");
      store.toggleFollow("rowan");
      store.activate("mira-valen");
      expect(store.activeFollowId()).toBe("mira-valen");
      expect(store.isFollowed("rowan")).toBe(true); // continua na lista, só não é mais o alvo
      expect(store.followedIds().sort()).toEqual(["mira-valen", "rowan"].sort());
    });

    // Bug real reportado pelo usuário: clicar num nome na lista "Followed" pra trocar o alvo da
    // câmera fazia a PRÓPRIA LISTA mudar de ordem (porque `activate()` reusava a mesma estrutura
    // usada pra derivar o alvo). A ordem visível da lista precisa ficar sempre a mesma (ordem em
    // que cada um foi seguido), não importa quantas vezes `activate()` seja chamado depois.
    it("activate() never reorders followedIds() — only toggleFollow changes list order", () => {
      const store = new FollowStore();
      store.toggleFollow("mira-valen");
      store.toggleFollow("rowan");
      store.toggleFollow("corvin");
      const originalOrder = store.followedIds();

      store.activate("mira-valen");
      expect(store.followedIds()).toEqual(originalOrder);

      store.activate("corvin");
      expect(store.followedIds()).toEqual(originalOrder);

      store.activate("rowan");
      expect(store.followedIds()).toEqual(originalOrder);
    });
  });

  // Pedido do usuário 2026-08-26: arrastar o mapa pra longe de quem a câmera segue deve
  // "desgrudar" a câmera (sem des-seguir); só reata via `activate()` (clicar o nome de novo) ou
  // seguindo outro agent (`toggleFollow`).
  describe("detachCamera — dragging away stops the camera lock without un-following", () => {
    it("clears activeFollowId() but keeps the entity in followedIds()", () => {
      const store = new FollowStore();
      store.toggleFollow("rowan");
      store.detachCamera();
      expect(store.activeFollowId()).toBeNull();
      expect(store.isFollowed("rowan")).toBe(true);
      expect(store.followedIds()).toEqual(["rowan"]);
    });

    it("activate() re-attaches the camera to a detached-but-still-followed entity", () => {
      const store = new FollowStore();
      store.toggleFollow("rowan");
      store.detachCamera();
      store.activate("rowan");
      expect(store.activeFollowId()).toBe("rowan");
    });

    it("following a different entity also re-attaches the camera (to the new one)", () => {
      const store = new FollowStore();
      store.toggleFollow("rowan");
      store.detachCamera();
      store.toggleFollow("mira-valen");
      expect(store.activeFollowId()).toBe("mira-valen");
      expect(store.isFollowed("rowan")).toBe(true); // rowan continua na lista
    });

    it("is a no-op (no notify) when nothing is attached", () => {
      const store = new FollowStore();
      const listener = vi.fn();
      store.subscribe(listener);
      store.detachCamera();
      expect(listener).not.toHaveBeenCalled();
    });
  });
});
