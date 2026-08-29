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

describe("WorldCreatorShell — World group (Geography/Climate/Resources)", () => {
  it("doc §32 — nav switches into each section, no longer 'coming later'", () => {
    renderShell();

    openWorldGroupSection("Geography");
    expect(screen.getByTestId("geography-section")).toBeInTheDocument();

    openWorldGroupSection("Climate");
    expect(screen.getByTestId("climate-section")).toBeInTheDocument();

    openWorldGroupSection("Resources");
    expect(screen.getByTestId("resources-section")).toBeInTheDocument();
  });

  it("Geography: ocean coverage slider and terrain style edit the draft", () => {
    renderShell();
    openWorldGroupSection("Geography");

    const ocean = screen.getByTestId("field-ocean-coverage").querySelector("input")! as HTMLInputElement;
    fireEvent.change(ocean, { target: { value: "35" } });
    expect(screen.getByTestId("field-ocean-coverage")).toHaveTextContent("35%");

    const terrain = screen.getByTestId("field-terrain-style").querySelector("select")! as HTMLSelectElement;
    fireEvent.change(terrain, { target: { value: "Archipelago" } });
    expect(terrain.value).toBe("Archipelago");
  });

  it("Climate: a locked field survives Randomize Climate", () => {
    renderShell();
    openWorldGroupSection("Climate");

    const zoneSelect = screen.getByTestId("field-climate-zone").querySelector("select")! as HTMLSelectElement;
    fireEvent.change(zoneSelect, { target: { value: "Polar" } });
    fireEvent.click(within(screen.getByTestId("field-climate-zone")).getByRole("button", { name: "🔓" }));

    fireEvent.click(screen.getByTestId("randomize-climate"));
    expect(zoneSelect.value).toBe("Polar");
  });

  it("Resources fields edit the draft and show up in Review", () => {
    renderShell();
    openWorldGroupSection("Resources");

    fireEvent.change(screen.getByTestId("field-mineral-abundance").querySelector("select")!, { target: { value: "Abundant" } });
    fireEvent.change(screen.getByTestId("field-fertility").querySelector("select")!, { target: { value: "Rich" } });

    fireEvent.click(screen.getByTestId("creator-review"));
    const review = screen.getByTestId("review-section");
    expect(review).toHaveTextContent("Abundant");
    expect(review).toHaveTextContent("Rich");
  });

  it("Review reflects Geography/Climate edits (doc §42-43)", () => {
    renderShell();

    openWorldGroupSection("Geography");
    fireEvent.change(screen.getByTestId("field-ocean-coverage").querySelector("input")!, { target: { value: "80" } });
    fireEvent.change(screen.getByTestId("field-terrain-style").querySelector("select")!, { target: { value: "Mountainous" } });

    openWorldGroupSection("Climate");
    fireEvent.change(screen.getByTestId("field-climate-zone").querySelector("select")!, { target: { value: "Tropical" } });

    fireEvent.click(screen.getByTestId("creator-review"));
    const review = screen.getByTestId("review-section");
    expect(review).toHaveTextContent("80%");
    expect(review).toHaveTextContent("Mountainous");
    expect(review).toHaveTextContent("Tropical");
  });
});
