import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { WorldEditor } from "../../src/components/creator/WorldEditor";
import { defaultScenarioForm, scenarioFormToJson } from "../../src/scenarioDefaults";

const VIEWPORT = { width: 200, height: 200 };

function stubRect(canvas: HTMLCanvasElement) {
  vi.spyOn(canvas, "getBoundingClientRect").mockReturnValue({
    left: 0,
    top: 0,
    width: canvas.width,
    height: canvas.height,
    right: canvas.width,
    bottom: canvas.height,
    x: 0,
    y: 0,
    toJSON: () => "",
  });
}

// Câmera determinística de WorldEditor: center=(width/2,height/2), scale=10 (ver WorldEditor.tsx).
// Assentamento default fica em (5,5); worldToScreen((5.5,5.5)) com center(5,5) e viewport 200x200.
const SETTLEMENT_SCREEN_POINT = { clientX: 105, clientY: 105 };

describe("WorldEditor", () => {
  beforeEach(() => {
    HTMLCanvasElement.prototype.getContext = () => null;
  });

  it("renders exactly one map canvas — no second map implementation alongside it", () => {
    render(<WorldEditor initialForm={defaultScenarioForm()} viewport={VIEWPORT} />);
    expect(screen.getAllByTestId("map-view-canvas")).toHaveLength(1);
    expect(screen.queryByTestId("grid-canvas")).not.toBeInTheDocument();
  });

  it("shows the general world config panel when nothing is selected", () => {
    const form = defaultScenarioForm();
    render(<WorldEditor initialForm={form} viewport={VIEWPORT} />);
    const panel = screen.getByTestId("world-general-config");
    expect(panel).toHaveTextContent(`${form.width} × ${form.height}`);
    expect(panel).toHaveTextContent(String(form.seed));
    expect(screen.queryByTestId("entity-inspector")).not.toBeInTheDocument();
    const sections = panel.querySelectorAll(":scope > .world-editor-panels > details");
    expect(sections).toHaveLength(6);
    expect([...sections].every((section) => !section.hasAttribute("open"))).toBe(true);
  });

  it("selecting the settlement marker on the map swaps the config panel for the entity inspector", () => {
    render(<WorldEditor initialForm={defaultScenarioForm()} viewport={VIEWPORT} />);
    const canvas = screen.getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    fireEvent.click(canvas, SETTLEMENT_SCREEN_POINT);

    expect(screen.getByTestId("entity-inspector")).toBeInTheDocument();
    expect(screen.queryByTestId("world-general-config")).not.toBeInTheDocument();
  });

  it("clicking empty map space clears the selection and brings the general config panel back", () => {
    render(<WorldEditor initialForm={defaultScenarioForm()} viewport={VIEWPORT} />);
    const canvas = screen.getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    fireEvent.click(canvas, SETTLEMENT_SCREEN_POINT);
    expect(screen.getByTestId("entity-inspector")).toBeInTheDocument();

    fireEvent.click(canvas, { clientX: 10, clientY: 10 });
    expect(screen.queryByTestId("entity-inspector")).not.toBeInTheDocument();
    expect(screen.getByTestId("world-general-config")).toBeInTheDocument();
  });

  it("creating the world posts the same scenario shape as the wizard and reports npcCount", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn((url: string) =>
        url === "/worlds/create"
          ? Promise.resolve(new Response(JSON.stringify({ npcCount: 20 }), { status: 200 }))
          : Promise.resolve(new Response("[]", { status: 200 })),
      ),
    );
    const onCreated = vi.fn();
    render(<WorldEditor initialForm={defaultScenarioForm()} onCreated={onCreated} viewport={VIEWPORT} />);

    fireEvent.click(screen.getByRole("button", { name: "Criar mundo" }));

    await waitFor(() => expect(onCreated).toHaveBeenCalledWith(20));
    const call = (fetch as unknown as ReturnType<typeof vi.fn>).mock.calls.find(([url]) => url === "/worlds/create")!;
    const body = JSON.parse((call[1] as RequestInit).body as string) as { scenarioJson: string };
    expect(body.scenarioJson).toBe(scenarioFormToJson(defaultScenarioForm()));
  });

  it("loads readable profession labels only when the preset has a period catalog", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn((url: string) =>
        url === "/periods/cidade-media/catalog"
          ? Promise.resolve(
              new Response(JSON.stringify({ professionNames: { 1: "Ferreiro" }, skillNames: {} }), {
                status: 200,
              }),
            )
          : Promise.resolve(new Response("[]", { status: 200 })),
      ),
    );
    render(
      <WorldEditor
        initialForm={defaultScenarioForm()}
        catalogPeriodId="cidade-media"
        viewport={VIEWPORT}
      />,
    );

    fireEvent.click(screen.getByText("Comportamento"));
    fireEvent.click(screen.getByText(/Avançado \(limiares de seleção/));

    expect(await screen.findAllByRole("option", { name: "Ferreiro (#1)" })).not.toHaveLength(0);
  });

  // worldToScreen((2.5,2.5)) com center(5,5), scale 10, viewport 200x200 -> (75,75).
  const EMPTY_CELL_SCREEN_POINT = { clientX: 75, clientY: 75 };

  it("positions a settlement by tool + click, not by typing coordinates, and shows the cell as read-only", () => {
    const form = defaultScenarioForm();
    render(<WorldEditor initialForm={form} viewport={VIEWPORT} />);
    const canvas = screen.getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    fireEvent.change(screen.getByLabelText("tool-select"), { target: { value: "settlement" } });
    fireEvent.click(canvas, EMPTY_CELL_SCREEN_POINT);

    expect(screen.getByLabelText("tool-last-cell")).toHaveTextContent("Célula: (2, 2)");
    // clicar em modo ferramenta nunca abre o inspector — não é seleção, é autoria.
    expect(screen.queryByTestId("entity-inspector")).not.toBeInTheDocument();
    expect(screen.getByTestId("world-general-config")).toHaveTextContent(`${form.settlements.length + 1}`);
  });

  it("paints terrain by tool + click and submits it as an exhaustive Cells array, matching buildCells", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn((url: string) =>
        url === "/worlds/create"
          ? Promise.resolve(new Response(JSON.stringify({ npcCount: 0 }), { status: 200 }))
          : Promise.resolve(new Response("[]", { status: 200 })),
      ),
    );
    render(<WorldEditor initialForm={defaultScenarioForm()} viewport={VIEWPORT} />);
    const canvas = screen.getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    fireEvent.change(screen.getByLabelText("tool-select"), { target: { value: "terrain" } });
    fireEvent.change(screen.getByLabelText("tool-terrain"), { target: { value: "2" } });
    fireEvent.click(canvas, EMPTY_CELL_SCREEN_POINT);

    fireEvent.click(screen.getByRole("button", { name: "Criar mundo" }));
    await waitFor(() =>
      expect((fetch as unknown as ReturnType<typeof vi.fn>).mock.calls.some(([url]) => url === "/worlds/create")).toBe(
        true,
      ),
    );

    const call = (fetch as unknown as ReturnType<typeof vi.fn>).mock.calls.find(([url]) => url === "/worlds/create")!;
    const scenario = JSON.parse(JSON.parse((call[1] as RequestInit).body as string).scenarioJson);
    expect(scenario.Cells).toHaveLength(100); // default form é 10x10, Cells é exaustivo
    const painted = scenario.Cells.find((c: { X: number; Y: number }) => c.X === 2 && c.Y === 2);
    expect(painted).toMatchObject({ Terrain: 2, Water: false });
  });

  it("leaves Cells out of the submitted JSON when no cell was painted (100% procedural by seed)", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn((url: string) =>
        url === "/worlds/create"
          ? Promise.resolve(new Response(JSON.stringify({ npcCount: 0 }), { status: 200 }))
          : Promise.resolve(new Response("[]", { status: 200 })),
      ),
    );
    render(<WorldEditor initialForm={defaultScenarioForm()} viewport={VIEWPORT} />);

    fireEvent.click(screen.getByRole("button", { name: "Criar mundo" }));
    await waitFor(() =>
      expect((fetch as unknown as ReturnType<typeof vi.fn>).mock.calls.some(([url]) => url === "/worlds/create")).toBe(
        true,
      ),
    );

    const call = (fetch as unknown as ReturnType<typeof vi.fn>).mock.calls.find(([url]) => url === "/worlds/create")!;
    const scenario = JSON.parse(JSON.parse((call[1] as RequestInit).body as string).scenarioJson);
    expect(scenario.Cells).toBeUndefined();
  });
});
