import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { WorldCreatorShell } from "../../src/entry/creator/WorldCreatorShell";
import { EntryServicesProvider } from "../../src/entry/EntryContext";
import { MockWorldRepository } from "../../src/entry/repository/MockWorldRepository";
import { LocalStorageDraftRepository } from "../../src/entry/repository/LocalStorageDraftRepository";
import { MockWorldGenerationService } from "../../src/entry/repository/MockWorldGenerationService";

beforeEach(() => {
  vi.useFakeTimers();
});

afterEach(() => {
  cleanup();
  window.localStorage.clear();
  vi.useRealTimers();
});

function renderShell(onNavigate = vi.fn()) {
  render(
    <EntryServicesProvider
      services={{ worlds: new MockWorldRepository(), drafts: new LocalStorageDraftRepository(), generation: new MockWorldGenerationService() }}
    >
      <WorldCreatorShell onNavigate={onNavigate} />
    </EntryServicesProvider>,
  );
  return { onNavigate };
}

function openWorldGroupSection(label: string) {
  fireEvent.click(within(screen.getByTestId("creator-nav-group-world")).getByText(label));
}

describe("WorldCreatorShell — World group (backend-aligned fields)", () => {
  it("Climate/Resources are disabled — no real backend field for them", () => {
    renderShell();
    const worldGroup = screen.getByTestId("creator-nav-group-world");
    expect(within(worldGroup).getByText("Climate").closest("button")).toBeDisabled();
    expect(within(worldGroup).getByText("Resources").closest("button")).toBeDisabled();
    expect(within(worldGroup).getByText("Geography").closest("button")).not.toBeDisabled();
  });

  it("Geography edits the real Width/Height/RegionSize fields, reflected in Review", () => {
    renderShell();
    openWorldGroupSection("Geography");

    fireEvent.change(screen.getByTestId("field-width").querySelector("input")!, { target: { value: "300" } });
    fireEvent.change(screen.getByTestId("field-height").querySelector("input")!, { target: { value: "150" } });

    fireEvent.click(screen.getByTestId("creator-review"));
    expect(screen.getByTestId("review-section")).toHaveTextContent("300 × 150");
  });

  it("Geography exposes the real (required) CostWeights fields, defaulted to what every shipped period uses", () => {
    renderShell();
    openWorldGroupSection("Geography");

    expect((screen.getByTestId("field-cost-base").querySelector("input") as HTMLInputElement).value).toBe("1");
    expect((screen.getByTestId("field-cost-altitude-weight").querySelector("input") as HTMLInputElement).value).toBe("0.5");
    expect((screen.getByTestId("field-terrain-weight-1").querySelector("input") as HTMLInputElement).value).toBe("1");
    expect((screen.getByTestId("field-terrain-weight-2").querySelector("input") as HTMLInputElement).value).toBe("1.5");
    expect((screen.getByTestId("field-terrain-weight-3").querySelector("input") as HTMLInputElement).value).toBe("3");

    fireEvent.change(screen.getByTestId("field-cost-base").querySelector("input")!, { target: { value: "2.5" } });
    fireEvent.click(screen.getByTestId("creator-review"));
    expect(screen.getByTestId("review-section")).toHaveTextContent("base 2.5, altitude ×0.5");
    expect(screen.getByTestId("review-section")).toHaveTextContent("1 / 1.5 / 3");
  });

  it("World Size preset fills in Width/Height/RegionSize in one step (undo restores all three together)", () => {
    renderShell();
    const sizeSelect = screen.getByTestId("field-size").querySelector("select")! as HTMLSelectElement;
    fireEvent.change(sizeSelect, { target: { value: "Huge" } });

    openWorldGroupSection("Geography");
    expect((screen.getByTestId("field-width").querySelector("input") as HTMLInputElement).value).toBe("512");

    fireEvent.keyDown(window, { key: "z", ctrlKey: true });
    expect((screen.getByTestId("field-width").querySelector("input") as HTMLInputElement).value).toBe("128");
  });

  it("Extraordinary is Enabled + Prevalence%, matching the real backend shape (not an invented enum)", () => {
    renderShell();
    const enabledCheckbox = screen.getByTestId("field-extraordinary-enabled").querySelector("input")! as HTMLInputElement;
    expect(enabledCheckbox.checked).toBe(true);
    expect(screen.getByTestId("field-extraordinary-prevalence")).toBeInTheDocument();

    fireEvent.click(enabledCheckbox);
    expect(screen.queryByTestId("field-extraordinary-prevalence")).not.toBeInTheDocument();
  });

  it("Seed is numeric-only (real backend seed is a ulong)", () => {
    renderShell();
    const seedInput = screen.getByTestId("field-seed").querySelector("input")! as HTMLInputElement;
    fireEvent.change(seedInput, { target: { value: "abc123xyz" } });
    expect(seedInput.value).toBe("123");
  });

  it("the Creator preview planet reacts live to Width and Extraordinary prevalence", () => {
    renderShell();
    const planetBefore = screen.getByTestId("creator-preview").querySelector('[data-testid="planet-scene"]') as HTMLElement;
    const scaleBefore = planetBefore.style.getPropertyValue("--planet-size-scale");
    const glowBefore = planetBefore.style.getPropertyValue("--planet-glow");

    const sizeSelect = screen.getByTestId("field-size").querySelector("select")! as HTMLSelectElement;
    fireEvent.change(sizeSelect, { target: { value: "Huge" } });
    const prevalence = screen.getByTestId("field-extraordinary-prevalence").querySelector("input")! as HTMLInputElement;
    fireEvent.change(prevalence, { target: { value: "90" } });

    const planetAfter = screen.getByTestId("creator-preview").querySelector('[data-testid="planet-scene"]') as HTMLElement;
    expect(planetAfter.style.getPropertyValue("--planet-size-scale")).not.toBe(scaleBefore);
    expect(planetAfter.style.getPropertyValue("--planet-glow")).not.toBe(glowBefore);
  });

  it("the tile map preview reflects Width/Height live, alongside the planet backdrop (user request)", () => {
    renderShell();

    expect(screen.getByTestId("tile-map-caption")).toHaveTextContent("128 × 128 tiles");
    expect(screen.getByTestId("creator-preview-planet-backdrop").querySelector('[data-testid="planet-scene"]')).toBeInTheDocument();

    const sizeSelect = screen.getByTestId("field-size").querySelector("select")! as HTMLSelectElement;
    fireEvent.change(sizeSelect, { target: { value: "Small" } });
    expect(screen.getByTestId("tile-map-caption")).toHaveTextContent("64 × 64 tiles");
  });

  it("the tile map supports zoom (buttons + wheel) and drag-pan, clamped to the map's own edge, resettable", () => {
    renderShell();

    expect(screen.getByTestId("tile-map-zoom")).toHaveTextContent("100%");
    fireEvent.click(screen.getByLabelText("Zoom in"));
    expect(screen.getByTestId("tile-map-zoom")).not.toHaveTextContent("100%");

    const viewport = screen.getByTestId("tile-map-viewport");
    fireEvent.wheel(viewport, { deltaY: -100 });

    const canvas = viewport.querySelector("canvas")!;
    // jsdom does no real layout (clientWidth/Height are always 0) — stub both elements to a
    // fixed 300x300 box so the pan-clamp math (which reads these) has real numbers to clamp
    // against, otherwise every offset would clamp to 0 regardless of what's being tested.
    Object.defineProperty(viewport, "clientWidth", { value: 300, configurable: true });
    Object.defineProperty(viewport, "clientHeight", { value: 300, configurable: true });
    Object.defineProperty(canvas, "clientWidth", { value: 300, configurable: true });
    Object.defineProperty(canvas, "clientHeight", { value: 300, configurable: true });

    // Force a known zoom (2x) so the expected clamp bound is predictable: at 300x300 base in a
    // 300x300 viewport, max pan on each axis is (300*2 - 300) / 2 = 150px.
    fireEvent.click(screen.getByText("Reset"));
    fireEvent.click(screen.getByLabelText("Zoom in"));
    fireEvent.click(screen.getByLabelText("Zoom in"));
    expect(screen.getByTestId("tile-map-zoom")).toHaveTextContent("196%"); // 1.4 * 1.4, close enough to 2x for the point

    // jsdom has no PointerEvent at all — React dispatches by event.type string, so a MouseEvent
    // typed "pointerdown"/"pointermove" still reaches the onPointerDown/onPointerMove handlers,
    // with `pointerId` bolted on manually (MouseEvent's init dict doesn't have one).
    function firePointer(type: string, clientX: number, clientY: number) {
      const event = new MouseEvent(type, { clientX, clientY, bubbles: true });
      Object.defineProperty(event, "pointerId", { value: 1 });
      fireEvent(viewport, event);
    }

    // A modest drag stays within bounds and passes through untouched.
    firePointer("pointerdown", 0, 0);
    firePointer("pointermove", 40, 20);
    expect(canvas.style.transform).toContain("translate(40px, 20px)");
    firePointer("pointerup", 40, 20);

    // User report: dragging far enough used to fling the map fully off-screen. A huge drag must
    // now clamp to the computed bound instead of applying the raw offset.
    // Same expression the component evaluates (two 1.4x zoom-in clicks compounded) — written
    // this way rather than a "1.96" literal so it's bit-identical to what the component computes.
    const compoundedZoom = 1 * 1.4 * 1.4;
    const maxOffset = (300 * compoundedZoom - 300) / 2;
    firePointer("pointerdown", 0, 0);
    firePointer("pointermove", 9999, 9999);
    expect(canvas.style.transform).toContain(`translate(${maxOffset}px, ${maxOffset}px)`);
    firePointer("pointerup", 9999, 9999);

    fireEvent.click(screen.getByText("Reset"));
    expect(screen.getByTestId("tile-map-zoom")).toHaveTextContent("100%");
    expect(canvas.style.transform).toContain("translate(0px, 0px)");
  });
});
