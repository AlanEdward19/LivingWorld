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
});
