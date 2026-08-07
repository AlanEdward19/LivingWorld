import { describe, expect, it } from "vitest";
import { render, screen, fireEvent, act } from "@testing-library/react";
import { FollowButton } from "../../src/components/inspector/FollowButton";
import { ViewStore } from "../../src/state/viewStore";
import { MockPortalSource } from "../../src/data/mock/MockPortalSource";
import type { EntityRef } from "../../src/map-engine/types";

const NPC: EntityRef = { kind: "npc", id: "1", space: { kind: "World" } };
const OTHER_NPC: EntityRef = { kind: "npc", id: "2", space: { kind: "World" } };

describe("FollowButton", () => {
  it("starts following on click, showing the toggled label", () => {
    const viewStore = new ViewStore(new MockPortalSource([]));
    render(<FollowButton entityRef={NPC} viewStore={viewStore} />);

    fireEvent.click(screen.getByRole("button", { name: "Seguir" }));

    expect(viewStore.followedEntity()).toEqual(NPC);
    expect(screen.getByRole("button", { name: "Parar de seguir" })).toBeInTheDocument();
  });

  it("stops following on a second click", () => {
    const viewStore = new ViewStore(new MockPortalSource([]));
    render(<FollowButton entityRef={NPC} viewStore={viewStore} />);

    fireEvent.click(screen.getByRole("button", { name: "Seguir" }));
    fireEvent.click(screen.getByRole("button", { name: "Parar de seguir" }));

    expect(viewStore.followedEntity()).toBeNull();
    expect(screen.getByRole("button", { name: "Seguir" })).toBeInTheDocument();
  });

  it("reflects a follow cancelled elsewhere (e.g. by a manual pan in MapView)", () => {
    const viewStore = new ViewStore(new MockPortalSource([]));
    render(<FollowButton entityRef={NPC} viewStore={viewStore} />);
    fireEvent.click(screen.getByRole("button", { name: "Seguir" }));

    act(() => viewStore.stopFollow()); // simula o cancelamento por pan manual em MapView

    expect(screen.getByRole("button", { name: "Seguir" })).toBeInTheDocument();
  });

  it("shows 'Seguir' (not 'Parar de seguir') when a different entity is being followed", () => {
    const viewStore = new ViewStore(new MockPortalSource([]));
    viewStore.startFollow(OTHER_NPC);

    render(<FollowButton entityRef={NPC} viewStore={viewStore} />);

    expect(screen.getByRole("button", { name: "Seguir" })).toBeInTheDocument();
  });
});
