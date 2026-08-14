import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { defaultScenarioForm, type ScenarioFormState } from "../../src/scenarioDefaults";
import { BehaviorPanel } from "../../src/components/creator/panels/BehaviorPanel";
import { CitiesPanel } from "../../src/components/creator/panels/CitiesPanel";
import { DynamicsPanel } from "../../src/components/creator/panels/DynamicsPanel";
import { EconomyPanel } from "../../src/components/creator/panels/EconomyPanel";
import { MapPanel } from "../../src/components/creator/panels/MapPanel";
import { PopulationPanel } from "../../src/components/creator/panels/PopulationPanel";

function props() {
  return {
    form: defaultScenarioForm(),
    set: vi.fn() as <K extends keyof ScenarioFormState>(key: K, value: ScenarioFormState[K]) => void,
  };
}

describe("World Creator panels", () => {
  it("MapPanel edits the map through the shared form setter", () => {
    const panel = props();
    render(<MapPanel {...panel} />);
    fireEvent.change(screen.getByLabelText("map-width"), { target: { value: "24" } });
    expect(panel.set).toHaveBeenCalledWith("width", 24);
    expect(screen.getByText(/Avançado \(custo de deslocamento/).closest("details")).not.toHaveAttribute("open");
  });

  it("PopulationPanel edits population through the shared form setter", () => {
    const panel = props();
    render(<PopulationPanel {...panel} />);
    fireEvent.change(screen.getByLabelText("population-initial"), { target: { value: "80" } });
    expect(panel.set).toHaveBeenCalledWith("initialPopulation", 80);
    expect(screen.getByText(/Avançado \(tabela de mortalidade/).closest("details")).not.toHaveAttribute("open");
  });

  it("BehaviorPanel edits behavior through the shared form setter", () => {
    const panel = props();
    render(<BehaviorPanel {...panel} />);
    fireEvent.click(screen.getByLabelText(/Histerese/));
    expect(panel.set).toHaveBeenCalledWith("hysteresisEnabled", false);
    expect(screen.getByText(/Avançado \(limiares de seleção/).closest("details")).not.toHaveAttribute("open");
  });

  it("EconomyPanel edits economy through the shared form setter", () => {
    const panel = props();
    render(<EconomyPanel {...panel} />);
    fireEvent.click(screen.getByLabelText(/Habilitada/));
    expect(panel.set).toHaveBeenCalledWith("economyEnabled", true);
    expect(screen.getByText(/Avançado \(capacidade/).closest("details")).not.toHaveAttribute("open");
  });

  it("CitiesPanel edits cities through the shared form setter", () => {
    const panel = props();
    render(<CitiesPanel {...panel} />);
    fireEvent.click(screen.getByLabelText(/Habilitadas/));
    expect(panel.set).toHaveBeenCalledWith("citiesEnabled", false);
    expect(screen.getByText(/Avançado \(limiares de escassez/).closest("details")).not.toHaveAttribute("open");
  });

  it("DynamicsPanel adds a profession bias through the shared form setter", () => {
    const panel = props();
    render(<DynamicsPanel {...panel} />);
    fireEvent.click(screen.getByRole("button", { name: "+ Vieses de profissão" }));
    expect(panel.set).toHaveBeenCalledWith("professionBiases", [
      { professionId: 0, weight: 1, name: "" },
    ]);
  });
});
