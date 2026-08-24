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

// Câmera fit-to-screen: grid 10x10 em viewport 200x200 -> scale=20.
// Assentamento default ocupa (4,4)..(6,6), então o centro continua em (100,100).
const SETTLEMENT_SCREEN_POINT = { clientX: 105, clientY: 105 };

describe("WorldEditor", () => {
  it("changes map tool through the visual dock", () => {
    render(<WorldEditor initialForm={defaultScenarioForm()} viewport={VIEWPORT} />);

    fireEvent.click(screen.getByRole("button", { name: "Terreno" }));

    expect(screen.getByLabelText("tool-select")).toHaveValue("terrain");
    expect(screen.getByLabelText("tool-terrain")).toBeInTheDocument();
  });

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
    expect(screen.getByRole("navigation", { name: "Capítulos da configuração" }).querySelectorAll("button")).toHaveLength(8);
    expect(screen.getAllByTestId("active-config-chapter")).toHaveLength(1);
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
    render(
      <WorldEditor
        initialForm={defaultScenarioForm()}
        worldName="Vale de Aster"
        onCreated={onCreated}
        viewport={VIEWPORT}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Dar vida ao mundo" }));

    await waitFor(() => expect(onCreated).toHaveBeenCalledWith(20));
    const call = (fetch as unknown as ReturnType<typeof vi.fn>).mock.calls.find(([url]) => url === "/worlds/create")!;
    const body = JSON.parse((call[1] as RequestInit).body as string) as { scenarioJson: string; name: string };
    expect(body.scenarioJson).toBe(scenarioFormToJson(defaultScenarioForm()));
    expect(body.name).toBe("Vale de Aster");
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

    fireEvent.click(screen.getByRole("button", { name: /Ritmos/ }));
    fireEvent.click(screen.getByRole("button", { name: "Ajustar regras de Ritmos" }));
    fireEvent.click(screen.getByText(/Avançado \(limiares de seleção/));

    expect(await screen.findAllByRole("option", { name: "Ferreiro (#1)" })).not.toHaveLength(0);
  });

  // worldToScreen((2.5,2.5)) com center(5,5), scale 20, viewport 200x200 -> (50,50).
  const EMPTY_CELL_SCREEN_POINT = { clientX: 50, clientY: 50 };

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

    fireEvent.click(screen.getByRole("button", { name: "Dar vida ao mundo" }));
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

    fireEvent.click(screen.getByRole("button", { name: "Dar vida ao mundo" }));
    await waitFor(() =>
      expect((fetch as unknown as ReturnType<typeof vi.fn>).mock.calls.some(([url]) => url === "/worlds/create")).toBe(
        true,
      ),
    );

    const call = (fetch as unknown as ReturnType<typeof vi.fn>).mock.calls.find(([url]) => url === "/worlds/create")!;
    const scenario = JSON.parse(JSON.parse((call[1] as RequestInit).body as string).scenarioJson);
    expect(scenario.Cells).toBeUndefined();
  });

  it("paints multiple terrain cells in one pointer drag", async () => {
    vi.stubGlobal("fetch", vi.fn((url: string) => url === "/worlds/create"
      ? Promise.resolve(new Response(JSON.stringify({ npcCount: 0 }), { status: 200 }))
      : Promise.resolve(new Response("[]", { status: 200 }))));
    render(<WorldEditor initialForm={defaultScenarioForm()} viewport={VIEWPORT} />);
    const canvas = screen.getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);
    fireEvent.change(screen.getByLabelText("tool-select"), { target: { value: "terrain" } });

    fireEvent.mouseDown(canvas, { clientX: 50, clientY: 50 });
    fireEvent.mouseMove(canvas, { clientX: 90, clientY: 50 });
    fireEvent.mouseUp(canvas);
    fireEvent.click(screen.getByRole("button", { name: "Dar vida ao mundo" }));

    await waitFor(() => expect((fetch as unknown as ReturnType<typeof vi.fn>).mock.calls.some(([url]) => url === "/worlds/create")).toBe(true));
    const call = (fetch as unknown as ReturnType<typeof vi.fn>).mock.calls.find(([url]) => url === "/worlds/create")!;
    const scenario = JSON.parse(JSON.parse((call[1] as RequestInit).body as string).scenarioJson);
    expect(scenario.Cells.filter((cell: { X: number; Y: number; Terrain: number }) => cell.Y === 2 && cell.X >= 2 && cell.X <= 4 && cell.Terrain === 1)).toHaveLength(3);
  });

  it("renames and drags a selected settlement", () => {
    render(<WorldEditor initialForm={defaultScenarioForm()} viewport={VIEWPORT} />);
    const canvas = screen.getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    fireEvent.click(canvas, SETTLEMENT_SCREEN_POINT);
    fireEvent.change(screen.getByLabelText("settlement-name"), { target: { value: "Porto Âmbar" } });
    expect(screen.getByLabelText("settlement-name")).toHaveValue("Porto Âmbar");

    fireEvent.mouseDown(canvas, SETTLEMENT_SCREEN_POINT);
    fireEvent.mouseMove(canvas, { clientX: 145, clientY: 105 });
    fireEvent.mouseUp(canvas);
    expect(screen.getByTestId("settlement-position")).toHaveTextContent("7, 5");
  });

  it("deletes a selected settlement with the inspector action and supports undo/redo", () => {
    render(<WorldEditor initialForm={defaultScenarioForm()} viewport={VIEWPORT} />);
    const canvas = screen.getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);
    fireEvent.click(canvas, SETTLEMENT_SCREEN_POINT);
    fireEvent.click(screen.getByRole("button", { name: "Apagar assentamento" }));
    expect(screen.getByTestId("world-general-config")).toHaveTextContent("0");

    fireEvent.keyDown(window, { key: "z", ctrlKey: true });
    expect(screen.getByTestId("world-general-config")).toHaveTextContent("1");
    fireEvent.keyDown(window, { key: "y", ctrlKey: true });
    expect(screen.getByTestId("world-general-config")).toHaveTextContent("0");
  });

  it("deletes a selected settlement with Delete but not while editing its name", () => {
    render(<WorldEditor initialForm={defaultScenarioForm()} viewport={VIEWPORT} />);
    const canvas = screen.getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);
    fireEvent.click(canvas, SETTLEMENT_SCREEN_POINT);
    const name = screen.getByLabelText("settlement-name");
    fireEvent.keyDown(name, { key: "Backspace" });
    expect(screen.getByTestId("entity-inspector")).toBeInTheDocument();
    fireEvent.keyDown(window, { key: "Delete" });
    expect(screen.queryByTestId("entity-inspector")).not.toBeInTheDocument();
  });

  it("rotates a selected settlement by button or R, but not while editing its name", () => {
    render(<WorldEditor initialForm={defaultScenarioForm()} viewport={VIEWPORT} />);
    const canvas = screen.getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);
    fireEvent.click(canvas, SETTLEMENT_SCREEN_POINT);
    expect(screen.getByTestId("settlement-rotation")).toHaveTextContent("0°");

    fireEvent.keyDown(window, { key: "r" });
    expect(screen.getByTestId("settlement-rotation")).toHaveTextContent("90°");
    fireEvent.click(screen.getByRole("button", { name: "Rotacionar assentamento" }));
    expect(screen.getByTestId("settlement-rotation")).toHaveTextContent("180°");
    fireEvent.keyDown(screen.getByLabelText("settlement-name"), { key: "r" });
    expect(screen.getByTestId("settlement-rotation")).toHaveTextContent("180°");
  });

  it("uses the erase tool to remove a settlement under the pointer", () => {
    render(<WorldEditor initialForm={defaultScenarioForm()} viewport={VIEWPORT} />);
    const canvas = screen.getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);
    fireEvent.click(screen.getByRole("button", { name: "Apagar" }));
    fireEvent.click(canvas, SETTLEMENT_SCREEN_POINT);
    expect(screen.getByTestId("world-general-config")).toHaveTextContent("0");
  });

  it("explains an advanced chapter before revealing technical values", () => {
    render(<WorldEditor initialForm={defaultScenarioForm()} viewport={VIEWPORT} />);
    const chapter = screen.getByRole("button", { name: /Povos/ });
    expect(chapter).toHaveAttribute("title", expect.stringContaining("história"));
    fireEvent.click(chapter);
    expect(screen.getByTestId("chapter-guide")).toHaveTextContent("Por onde começar");
    expect(screen.getByRole("button", { name: "Ajustar regras de Povos" })).toBeInTheDocument();
    expect(screen.queryByLabelText("population-initial")).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Ajustar regras de Povos" }));
    const population = screen.getByLabelText("population-initial");
    expect(population).toBeInTheDocument();
    expect(population.closest("label")).toHaveAttribute("title", expect.stringContaining("Ajuste fino"));
  });

  it("authors extraordinary options as generic descriptors without named archetypes", () => {
    render(<WorldEditor initialForm={defaultScenarioForm()} viewport={VIEWPORT} />);

    fireEvent.click(screen.getByRole("button", { name: /Extraordinário/ }));
    fireEvent.click(screen.getByRole("button", { name: "Ajustar regras de Extraordinário" }));

    const toggle = screen.getByLabelText("Ativar extraordinário");
    expect(toggle).not.toBeChecked();
    fireEvent.click(toggle);
    fireEvent.click(screen.getByRole("button", { name: "+ Descritores extraordinários" }));

    expect(screen.getByLabelText("Descritores extraordinários-identificador-0")).toBeInTheDocument();
    expect(screen.getByLabelText("Descritores extraordinários-manifestações (csv)-0")).toBeInTheDocument();
    expect(screen.queryByText(/vampiro|lobisomem|lanterna/i)).not.toBeInTheDocument();
  });

  it("opens the selected settlement in a local city editor", () => {
    render(<WorldEditor initialForm={defaultScenarioForm()} viewport={VIEWPORT} />);
    const canvas = screen.getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);
    fireEvent.click(canvas, SETTLEMENT_SCREEN_POINT);

    fireEvent.click(screen.getByRole("button", { name: "Editar por dentro" }));

    expect(screen.getByTestId("creator-city-editor")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "vila" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Voltar ao mapa-múndi" })).toBeInTheDocument();
    // citySide(20, 10, 10) = 3 (footprint mínimo) — vila nesse tamanho nasce só com 1 construção
    // (T44b: 3 posições fixas saíam dos limites de uma vila 3x3).
    expect(screen.getByText(/1 construções/)).toBeInTheDocument();

    const cityCanvas = screen.getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(cityCanvas);
    fireEvent.click(screen.getByRole("button", { name: "Construção" }));
    fireEvent.click(cityCanvas, { clientX: 100, clientY: 100 });
    expect(screen.getByText(/2 construções/)).toBeInTheDocument();

    fireEvent.mouseDown(cityCanvas, { clientX: 105, clientY: 105 });
    fireEvent.mouseMove(cityCanvas, { clientX: 121, clientY: 105 });
    fireEvent.mouseUp(cityCanvas);
    // Canvas local agora usa o footprint real (citySide), bem menor que o antigo 24x18 fixo —
    // o mesmo arrasto de pixels aterrissa numa célula bem mais próxima do centro.
    expect(screen.getByTestId("creator-building-inspector")).toHaveTextContent("1, 1");

    fireEvent.keyDown(window, { key: "r" });
    expect(screen.getByTestId("building-rotation")).toHaveTextContent("90°");
    fireEvent.click(screen.getByRole("button", { name: "Rotacionar construção" }));
    expect(screen.getByTestId("building-rotation")).toHaveTextContent("180°");

    fireEvent.keyDown(window, { key: "Delete" });
    expect(screen.getByText(/1 construções/)).toBeInTheDocument();
    fireEvent.keyDown(window, { key: "z", ctrlKey: true });
    expect(screen.getByText(/2 construções/)).toBeInTheDocument();
  });
});
