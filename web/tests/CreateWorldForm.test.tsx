import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { CreateWorldForm } from "../src/components/CreateWorldForm";

describe("CreateWorldForm", () => {
  beforeEach(() => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response(JSON.stringify({ npcCount: 100 }), { status: 200 })),
    );
  });

  it("posts the default scenario as a full PascalCase JSON body on submit", async () => {
    render(<CreateWorldForm />);

    fireEvent.click(screen.getByRole("button", { name: "Criar mundo" }));

    await waitFor(() => expect(fetch).toHaveBeenCalled());

    const [url, init] = (fetch as unknown as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(url).toBe("/worlds/create");
    const body = JSON.parse((init as RequestInit).body as string) as { scenarioJson: string };
    const scenario = JSON.parse(body.scenarioJson);

    expect(scenario.Width).toBe(10);
    expect(scenario.InitialPopulation).toBe(100);
    expect(scenario.EconomyEnabled).toBe(true);
    expect(scenario.CitiesEnabled).toBe(true);
    expect(scenario.MaxDurationHours).toEqual({
      Eat: 2,
      Sleep: 8,
      Work: 8,
      Socialize: 3,
      Travel: 4,
      Idle: 2,
      Buy: 2,
    });
    expect(scenario.Dynamics).toEqual({
      ProfessionBiases: [],
      SkillBiases: [],
      TransformationRules: [],
    });
    expect(scenario.Cells).toBeUndefined();
  });

  it("emits an exhaustive Cells array once a cell is painted in the map editor", async () => {
    render(<CreateWorldForm />);

    const canvas = screen.getByTestId("grid-canvas") as HTMLCanvasElement;
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
    fireEvent.click(canvas, { clientX: 3.5 * 16, clientY: 4.5 * 16 }); // paints (3,4) with default terrain 1

    fireEvent.click(screen.getByRole("button", { name: "Criar mundo" }));
    await waitFor(() => expect(fetch).toHaveBeenCalled());

    const [, init] = (fetch as unknown as ReturnType<typeof vi.fn>).mock.calls[0];
    const scenario = JSON.parse(JSON.parse((init as RequestInit).body as string).scenarioJson);

    expect(scenario.Cells).toHaveLength(100); // default form is 10x10
    const painted = scenario.Cells.find((c: { X: number; Y: number }) => c.X === 3 && c.Y === 4);
    expect(painted).toMatchObject({ Terrain: 1, Biome: 1, Water: false, Altitude: 0 });
    const unpainted = scenario.Cells.find((c: { X: number; Y: number }) => c.X === 0 && c.Y === 0);
    expect(unpainted).toMatchObject({ Terrain: 1, Biome: 1 });
  });

  it("reflects an edited field (seed) in the submitted JSON", async () => {
    render(<CreateWorldForm />);

    fireEvent.change(screen.getByLabelText("map-seed"), { target: { value: "42" } });
    fireEvent.click(screen.getByRole("button", { name: "Criar mundo" }));

    await waitFor(() => expect(fetch).toHaveBeenCalled());

    const [, init] = (fetch as unknown as ReturnType<typeof vi.fn>).mock.calls[0];
    const body = JSON.parse((init as RequestInit).body as string) as { scenarioJson: string };
    expect(JSON.parse(body.scenarioJson).Seed).toBe(42);
  });

  it("calls onCreated with the npc count returned by the server", async () => {
    const onCreated = vi.fn();
    render(<CreateWorldForm onCreated={onCreated} />);

    fireEvent.click(screen.getByRole("button", { name: "Criar mundo" }));

    await waitFor(() => expect(onCreated).toHaveBeenCalledWith(100));
  });
});
