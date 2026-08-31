import { describe, expect, it } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { CognitionTrace, cognitionTraceOf, type CognitionTraceEntry } from "../../src/components/inspector/CognitionTrace";
import type { NpcInspection } from "../../src/data/contracts";

const SAMPLE_TRACE: CognitionTraceEntry = {
  tick: 10,
  trace: {
    wakeReason: 1,
    previousIntent: 1,
    topPressures: [{ kind: "AcquireFood", intensity: 80, factors: ["Hunger"] }],
    knownOpportunities: [{ kind: "FoodAtMarket", attractiveness: 60 }],
    winner: 0,
    winningUtility: 42.5,
    topPositiveFactors: ["Hunger"],
    topNegativeFactors: ["Distance"],
    blockingFactors: [],
    knownAlternatives: [1, 3],
  },
};

const SECOND_TRACE: CognitionTraceEntry = {
  tick: 12,
  trace: {
    wakeReason: 4,
    previousIntent: 0,
    topPressures: [{ kind: "EarnIncome", intensity: 55, factors: ["Wealth"] }],
    knownOpportunities: [],
    winner: 2,
    winningUtility: 31,
    topPositiveFactors: ["Employer"],
    topNegativeFactors: [],
    blockingFactors: [],
    knownAlternatives: [5],
  },
};

const BASE_INSPECTION: NpcInspection = {
  id: { value: 3 }, name: "Lina", sex: 1, ageYears: 27,
  culture: { id: 2 }, city: { value: "city-a" }, household: { value: 41 },
  motherId: { value: 1 }, fatherId: { value: 2 }, spouse: { value: 4 },
  profession: { id: 6 }, employer: { value: 52 }, health: 91,
  hunger: 63, thirst: 72, sleep: 81, social: 54, personality: {},
  skills: { values: {} }, currentLocation: { x: 1, y: 1 },
  currentAction: 2, actionStartedAtTick: 9,
  actionTarget: { kind: "workplace", id: "52" }, lod: 0, memories: [],
  beliefs: [], powerIds: [], currentScope: { kind: 1, cityId: { value: "city-a" } },
};

describe("CognitionTrace", () => {
  it("shows an explicit empty state when no trace entries are provided", () => {
    render(<CognitionTrace entries={[]} />);

    expect(screen.getByRole("status")).toHaveTextContent("sem rastro — fora de observação");
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
  });

  it("reads an empty trace from inspection payloads that omit cognitionTrace", () => {
    expect(cognitionTraceOf(BASE_INSPECTION)).toEqual([]);
  });

  it("renders a table row from API trace data without inventing fields", () => {
    render(<CognitionTrace entries={[SAMPLE_TRACE]} />);

    const row = screen.getByRole("row", { name: /10.*Necessidade urgente.*Dormindo.*Comendo.*42\.5.*AcquireFood/i });
    expect(within(row).getByRole("cell", { name: "10" })).toBeInTheDocument();
    expect(within(row).getByRole("cell", { name: "Comendo" })).toBeInTheDocument();
    expect(within(row).getByRole("cell", { name: "42.5" })).toBeInTheDocument();
    expect(within(row).getByRole("cell", { name: "AcquireFood (80)" })).toBeInTheDocument();
  });

  it("renders multiple entries in the order supplied by the API", () => {
    render(<CognitionTrace entries={[SAMPLE_TRACE, SECOND_TRACE]} />);

    const rows = screen.getAllByRole("row").filter((row) => row.querySelector("td"));
    expect(rows).toHaveLength(2);
    expect(within(rows[0]).getByRole("cell", { name: "10" })).toBeInTheDocument();
    expect(within(rows[1]).getByRole("cell", { name: "12" })).toBeInTheDocument();
    expect(within(rows[1]).getByRole("cell", { name: "Trabalhando" })).toBeInTheDocument();
  });

  it("lists ticks on the timeline from trace data only", () => {
    render(<CognitionTrace entries={[SAMPLE_TRACE, SECOND_TRACE]} />);

    const timeline = screen.getByRole("list", { name: "Linha do tempo de decisões" });
    expect(within(timeline).getAllByRole("listitem")).toHaveLength(2);
    expect(within(timeline).getByText("Tick 10")).toBeInTheDocument();
    expect(within(timeline).getByText("Tick 12")).toBeInTheDocument();
  });
});

describe("CognitionTrace visual flow (T12)", () => {
  it("shows the visual flow for the latest retained decision by default", () => {
    render(<CognitionTrace entries={[SAMPLE_TRACE, SECOND_TRACE]} />);

    const flow = screen.getByRole("region", { name: "Fluxo visual de decisão" });
    expect(within(flow).getByText(/Decisão 2 de 2 — tick 12/)).toBeInTheDocument();

    const decision = within(flow).getByLabelText("Decisão");
    expect(within(decision).getByText("Trabalhando")).toBeInTheDocument();
    expect(within(decision).getByText("31")).toBeInTheDocument();
  });

  it("navigates to the previous decision with the Anterior button", () => {
    render(<CognitionTrace entries={[SAMPLE_TRACE, SECOND_TRACE]} />);

    const flow = screen.getByRole("region", { name: "Fluxo visual de decisão" });
    fireEvent.click(screen.getByRole("button", { name: "Decisão anterior" }));

    expect(within(flow).getByText(/Decisão 1 de 2 — tick 10/)).toBeInTheDocument();
    const decision = within(flow).getByLabelText("Decisão");
    expect(within(decision).getByText("Comendo")).toBeInTheDocument();
    expect(within(decision).getByText("42.5")).toBeInTheDocument();
  });

  it("navigates to the next decision with the Próxima button", () => {
    render(<CognitionTrace entries={[SAMPLE_TRACE, SECOND_TRACE]} />);

    const flow = screen.getByRole("region", { name: "Fluxo visual de decisão" });
    fireEvent.click(screen.getByRole("button", { name: "Decisão anterior" }));
    fireEvent.click(screen.getByRole("button", { name: "Próxima decisão" }));

    expect(within(flow).getByText(/Decisão 2 de 2 — tick 12/)).toBeInTheDocument();
    expect(within(flow).getByLabelText("Decisão")).toHaveTextContent("Trabalhando");
  });

  it("selects a decision when its timeline tick is clicked", () => {
    render(<CognitionTrace entries={[SAMPLE_TRACE, SECOND_TRACE]} />);

    const flow = screen.getByRole("region", { name: "Fluxo visual de decisão" });
    fireEvent.click(screen.getByRole("button", { name: "Tick 10" }));

    expect(within(flow).getByText(/Decisão 1 de 2 — tick 10/)).toBeInTheDocument();
    const stimulus = within(flow).getByLabelText("Estímulo");
    expect(within(stimulus).getByText("AcquireFood (80)")).toBeInTheDocument();
    expect(within(stimulus).getByText("FoodAtMarket (60)")).toBeInTheDocument();
  });

  it("renders stimulus, ponderation and decision stages from the same trace data", () => {
    render(<CognitionTrace entries={[SAMPLE_TRACE]} />);

    const flow = screen.getByRole("region", { name: "Fluxo visual de decisão" });
    const stimulus = within(flow).getByLabelText("Estímulo");
    expect(within(stimulus).getByText("Necessidade urgente")).toBeInTheDocument();
    expect(within(stimulus).getByText("AcquireFood (80)")).toBeInTheDocument();

    const ponderation = within(flow).getByLabelText("Ponderação");
    expect(within(ponderation).getByText("Hunger")).toBeInTheDocument();
    expect(within(ponderation).getByText("Distance")).toBeInTheDocument();
    expect(within(ponderation).getByText("Dormindo, Socializando")).toBeInTheDocument();
  });

  it("disables navigation buttons at the ends of the retained window", () => {
    render(<CognitionTrace entries={[SAMPLE_TRACE, SECOND_TRACE]} />);

    const prev = screen.getByRole("button", { name: "Decisão anterior" });
    const next = screen.getByRole("button", { name: "Próxima decisão" });
    expect(prev).toBeEnabled();
    expect(next).toBeDisabled();

    fireEvent.click(prev);
    expect(prev).toBeDisabled();
    expect(next).toBeEnabled();
  });
});
