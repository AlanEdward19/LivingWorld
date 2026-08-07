import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { WorldEditor } from "../../src/components/creator/WorldEditor";
import { defaultScenarioForm } from "../../src/scenarioDefaults";

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
    expect(JSON.parse(body.scenarioJson).Width).toBe(10);
  });
});
