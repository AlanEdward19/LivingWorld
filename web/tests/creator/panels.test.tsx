import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { defaultScenarioForm, type ScenarioFormState } from "../../src/scenarioDefaults";
import { BehaviorPanel } from "../../src/components/creator/panels/BehaviorPanel";
import { CitiesPanel } from "../../src/components/creator/panels/CitiesPanel";
import { DynamicsPanel } from "../../src/components/creator/panels/DynamicsPanel";
import { EconomyPanel } from "../../src/components/creator/panels/EconomyPanel";
import { ExtraordinaryPanel } from "../../src/components/creator/panels/ExtraordinaryPanel";
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

  it("ExtraordinaryPanel adds a descriptor through the guided power builder and a cultural response through the list editor", () => {
    const panel = props();
    render(<ExtraordinaryPanel {...panel} />);

    fireEvent.change(screen.getByLabelText("Prevalência extraordinária"), { target: { value: "0.25" } });
    expect(panel.set).toHaveBeenCalledWith("extraordinaryPrevalence", 0.25);

    fireEvent.click(screen.getByRole("button", { name: "+ Respostas culturais" }));
    expect(panel.set).toHaveBeenCalledWith("extraordinaryCulturalResponses", [
      { cultureId: 0, manifestation: "", response: "" },
    ]);

    fireEvent.click(screen.getByRole("button", { name: "+ Adicionar poder" }));
    expect(screen.getByTestId("power-builder")).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Identificador do poder"), { target: { value: "novo-poder" } });
    fireEvent.click(screen.getByRole("button", { name: "Salvar poder" }));

    expect(panel.set).toHaveBeenCalledWith("extraordinaryEnabled", true);
    expect(panel.set).toHaveBeenCalledWith("extraordinaryDescriptors", [{
      id: "novo-poder", source: "", effects: "", mode: "Active", costs: "", reliability: "Guaranteed",
      failureModes: "", intrinsicVulnerabilities: "", manifestations: "", acquisitionRules: "",
      appearanceScaleMultiplier: 1, appearanceSkinTint: "", appearanceMovementTrail: "",
      needSubstitutionReplacesNeed: "", needSubstitutionResourceId: null,
      needSubstitutionUnitsPerUse: 1, senescenceRateMultiplier: 1, manifestationCondition: "",
    }]);
  });

  it("ExtraordinaryPanel edits an existing descriptor's advanced fields through the guided builder, preserving unrecognized effects, and saves them together", () => {
    const panel = props();
    panel.form.extraordinaryDescriptors = [{
      id: "p", source: "s", effects: "movement:1", mode: "Conditional", costs: "",
      reliability: "Guaranteed", failureModes: "", intrinsicVulnerabilities: "",
      manifestations: "", acquisitionRules: "", appearanceScaleMultiplier: 1,
      appearanceSkinTint: "", appearanceMovementTrail: "", needSubstitutionReplacesNeed: "",
      needSubstitutionResourceId: null, needSubstitutionUnitsPerUse: 1,
      senescenceRateMultiplier: 1, manifestationCondition: "",
    }];
    render(<ExtraordinaryPanel {...panel} />);

    fireEvent.click(screen.getByRole("button", { name: "Editar" }));
    expect(screen.getByTestId("power-builder")).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("Escala visual"), { target: { value: "1.5" } });
    fireEvent.click(screen.getByRole("button", { name: "Tom de pele: Pálido" }));
    fireEvent.click(screen.getByRole("button", { name: "Névoa" }));
    fireEvent.click(screen.getByRole("button", { name: /Só à noite/ }));

    fireEvent.click(screen.getByRole("button", { name: /Ajustes avançados/ }));
    fireEvent.click(screen.getByLabelText("Substitui uma necessidade por um recurso"));
    fireEvent.change(screen.getByLabelText("Recurso metabólico"), { target: { value: "9" } });
    fireEvent.change(screen.getByLabelText("Unidades por uso"), { target: { value: "2" } });
    fireEvent.change(screen.getByLabelText("Multiplicador de senescência"), { target: { value: "0" } });

    fireEvent.click(screen.getByRole("button", { name: "Salvar poder" }));

    expect(panel.set).toHaveBeenCalledWith("extraordinaryDescriptors", [expect.objectContaining({
      effects: "movement:1", // token que não reconhecemos — preservado, nunca descartado
      appearanceScaleMultiplier: 1.5, appearanceSkinTint: "pale", appearanceMovementTrail: "mist",
      manifestationCondition: "world:is-night", needSubstitutionReplacesNeed: "hunger",
      needSubstitutionResourceId: 9, needSubstitutionUnitsPerUse: 2, senescenceRateMultiplier: 0,
    })]);
  });
});
