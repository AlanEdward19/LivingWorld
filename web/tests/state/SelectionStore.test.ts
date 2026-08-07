import { describe, expect, it } from "vitest";
import { SelectionStore } from "../../src/state/selectionStore";
import { ViewStore } from "../../src/state/viewStore";
import { MockPortalSource } from "../../src/data/mock/MockPortalSource";
import { portalFixtures } from "../../src/data/mock/fixtures";
import type { EntityRef, SpaceId } from "../../src/map-engine/types";

const WORLD: SpaceId = { kind: "World" };
const CITY_A: SpaceId = { kind: "City", cityId: "city-a" };

function npcRef(id: string, space: SpaceId = WORLD): EntityRef {
  return { kind: "npc", id, space };
}

describe("SelectionStore", () => {
  it("selecting never touches the ViewStore's space or camera", () => {
    const selection = new SelectionStore();
    const view = new ViewStore(new MockPortalSource(portalFixtures));
    const spaceBefore = view.currentSpace();
    const cameraBefore = view.cameraFor(WORLD, { center: { x: 0, y: 0 }, scale: 1 });

    selection.select(npcRef("1"));

    expect(view.currentSpace()).toEqual(spaceBefore);
    expect(view.cameraFor(WORLD, { center: { x: 0, y: 0 }, scale: 1 })).toEqual(cameraBefore);
  });

  it("preserves the selection across a space change when the entity exists in the new space", () => {
    const selection = new SelectionStore();
    selection.select(npcRef("3000", WORLD));

    selection.syncWithSpace(CITY_A, [npcRef("3000", CITY_A), npcRef("3001", CITY_A)]);

    expect(selection.current()).toEqual(npcRef("3000", CITY_A));
  });

  it("clears the selection on a space change when the entity does not exist there", () => {
    const selection = new SelectionStore();
    selection.select(npcRef("3000", WORLD));

    selection.syncWithSpace(CITY_A, [npcRef("3001", CITY_A)]);

    expect(selection.current()).toBeNull();
  });

  it("clears the selection when the entity disappears from the current space's snapshot", () => {
    const selection = new SelectionStore();
    selection.select(npcRef("3000", CITY_A));

    selection.syncWithSpace(CITY_A, [npcRef("3001", CITY_A)]); // 3000 sumiu, mesmo espaço

    expect(selection.current()).toBeNull();
  });

  it("replaces the previous selection when a new entity is selected", () => {
    const selection = new SelectionStore();
    selection.select(npcRef("1"));
    selection.select(npcRef("2"));

    expect(selection.current()).toEqual(npcRef("2"));
  });

  it("clear() always empties the selection, notifying subscribers", () => {
    const selection = new SelectionStore();
    selection.select(npcRef("1"));
    let notified = 0;
    selection.subscribe(() => (notified += 1));

    selection.clear();

    expect(selection.current()).toBeNull();
    expect(notified).toBe(1);
  });

  it("syncWithSpace is a no-op when nothing is selected", () => {
    const selection = new SelectionStore();

    expect(() => selection.syncWithSpace(CITY_A, [])).not.toThrow();
    expect(selection.current()).toBeNull();
  });
});
