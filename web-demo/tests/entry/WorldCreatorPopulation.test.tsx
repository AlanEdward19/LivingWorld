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

function openPopulation() {
  fireEvent.click(within(screen.getByTestId("creator-nav-group-life")).getByText("Population"));
}

function setName(value: string) {
  fireEvent.click(screen.getByText("Overview"));
  fireEvent.change(screen.getByTestId("field-name").querySelector("input")!, { target: { value } });
}

describe("WorldCreatorShell — Population (real PopulationScenarioLoader fields)", () => {
  it("Population is enabled in the Life nav group, defaulted to what every shipped period uses", () => {
    renderShell();
    const lifeGroup = screen.getByTestId("creator-nav-group-life");
    expect(within(lifeGroup).getByText("Population").closest("button")).not.toBeDisabled();
    expect(within(lifeGroup).getByText("Biology").closest("button")).toBeDisabled();

    openPopulation();
    expect((screen.getByTestId("field-culture").querySelector("input") as HTMLInputElement).value).toBe("1");
    expect((screen.getByTestId("field-max-longevity").querySelector("input") as HTMLInputElement).value).toBe("90");
    expect((screen.getByTestId("field-fertility-min-age").querySelector("input") as HTMLInputElement).value).toBe("16");
    expect((screen.getByTestId("field-fertility-max-age").querySelector("input") as HTMLInputElement).value).toBe("45");
    expect((screen.getByTestId("field-gestation-days").querySelector("input") as HTMLInputElement).value).toBe("270");
    expect((screen.getByTestId("field-max-bytes-per-npc").querySelector("input") as HTMLInputElement).value).toBe("4000");
    // Village defaults to map center for the default Medium (128x128) preset.
    expect((screen.getByTestId("field-village-x").querySelector("input") as HTMLInputElement).value).toBe("64");
    expect((screen.getByTestId("field-village-y").querySelector("input") as HTMLInputElement).value).toBe("64");
  });

  it("World Size re-centers Village X/Y so it can't land outside a smaller map", () => {
    renderShell();
    const sizeSelect = screen.getByTestId("field-size").querySelector("select")! as HTMLSelectElement;
    fireEvent.change(sizeSelect, { target: { value: "Small" } }); // 64x64

    openPopulation();
    expect((screen.getByTestId("field-village-x").querySelector("input") as HTMLInputElement).value).toBe("32");
    expect((screen.getByTestId("field-village-y").querySelector("input") as HTMLInputElement).value).toBe("32");
  });

  it("blocks Generate when Village X/Y falls outside the map", () => {
    renderShell();
    setName("Eldoria");
    openPopulation();
    fireEvent.change(screen.getByTestId("field-village-x").querySelector("input")!, { target: { value: "9999" } });

    fireEvent.click(screen.getByTestId("creator-review"));
    expect(screen.getByTestId("review-blocked-village")).toBeInTheDocument();
    expect(screen.getByTestId("generate-world")).toBeDisabled();
  });

  it("blocks Generate when fertility min age exceeds max age", () => {
    renderShell();
    setName("Eldoria");
    openPopulation();
    fireEvent.change(screen.getByTestId("field-fertility-min-age").querySelector("input")!, { target: { value: "50" } });

    fireEvent.click(screen.getByTestId("creator-review"));
    expect(screen.getByTestId("review-blocked-fertility")).toBeInTheDocument();
    expect(screen.getByTestId("generate-world")).toBeDisabled();
  });

  it("Max Alive NPCs is optional — hidden until enabled, reflected in Review as Unlimited otherwise", () => {
    renderShell();
    setName("Eldoria");
    openPopulation();
    expect(screen.queryByTestId("field-max-alive-npcs")).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId("creator-review"));
    expect(screen.getByTestId("review-section")).toHaveTextContent("Unlimited");

    fireEvent.click(screen.getByText("Overview")); // stays on same draft
    openPopulation();
    fireEvent.click(screen.getByTestId("field-max-alive-npcs-enabled").querySelector("input")!);
    fireEvent.change(screen.getByTestId("field-max-alive-npcs").querySelector("input")!, { target: { value: "500" } });

    fireEvent.click(screen.getByTestId("creator-review"));
    expect(screen.getByTestId("review-section")).toHaveTextContent("500");
  });
});
