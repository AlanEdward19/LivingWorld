import { describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { PresetStart } from "../../src/components/creator/PresetStart";

function stubFetch(templates: unknown[] = [], periodBody: unknown = {}) {
  vi.stubGlobal(
    "fetch",
    vi.fn((url: string) => {
      if (url === "/periods") {
        return Promise.resolve(new Response(JSON.stringify(templates), { status: 200 }));
      }
      return Promise.resolve(new Response(JSON.stringify({ periodDefinition: periodBody }), { status: 200 }));
    }),
  );
}

describe("PresetStart", () => {
  it("updates the visual preview when a size card is chosen", () => {
    stubFetch();
    render(<PresetStart onStart={() => {}} />);

    const preview = screen.getByTestId("preview-map-world");
    const before = preview.style.transform;
    fireEvent.click(screen.getByRole("button", { name: /Grande 40×40/ }));

    expect(screen.getByLabelText("preset-size")).toHaveValue("grande");
    expect(screen.getByRole("complementary", { name: "Prévia do mundo" })).toHaveTextContent("40 × 40");
    expect(screen.getByRole("complementary", { name: "Prévia do mundo" })).toHaveTextContent("150");
    expect(screen.getByTestId("preview-map-world").style.transform).not.toBe(before);
    expect(screen.getByTestId("preview-map-world").querySelectorAll("i")).toHaveLength(1600);
    expect(screen.getByTestId("preview-map-world")).toHaveStyle({ gridTemplateColumns: "repeat(40, 1fr)" });
  });

  it("exposes at most 4 fields and no advanced parameter", () => {
    stubFetch();
    render(<PresetStart onStart={() => {}} />);
    expect(screen.getAllByRole("textbox").length + screen.getAllByRole("spinbutton").length + screen.getAllByRole("combobox").length).toBe(4);
    expect(screen.queryByText(/avançado/i)).not.toBeInTheDocument();
  });

  it("starts a blank world from the chosen size preset, with the typed seed applied", async () => {
    stubFetch();
    const onStart = vi.fn();
    render(<PresetStart onStart={onStart} />);

    fireEvent.change(screen.getByLabelText("preset-name"), { target: { value: "Aldeia" } });
    fireEvent.change(screen.getByLabelText("preset-seed"), { target: { value: "7" } });
    fireEvent.change(screen.getByLabelText("preset-size"), { target: { value: "grande" } });
    fireEvent.click(screen.getByRole("button", { name: "Começar" }));

    await waitFor(() => expect(onStart).toHaveBeenCalled());
    const [form, name] = onStart.mock.calls[0];
    expect(form.width).toBe(40);
    expect(form.height).toBe(40);
    expect(form.initialPopulation).toBe(150);
    expect(form.seed).toBe(7);
    expect(name).toBe("Aldeia");
  });

  it("pre-populates the full ScenarioFormState when a template is chosen instead of blank", async () => {
    stubFetch(
      [{ periodId: "cidade-media", version: 1, source: "Cidade média", createdAtUtc: "" }],
      { Width: 20, Height: 20, Seed: 2, InitialPopulation: 150 },
    );
    const onStart = vi.fn();
    render(<PresetStart onStart={onStart} />);

    await screen.findByRole("option", { name: "Cidade média" });
    fireEvent.change(screen.getByLabelText("preset-starting-point"), { target: { value: "cidade-media" } });
    fireEvent.click(screen.getByRole("button", { name: "Começar" }));

    await waitFor(() => expect(onStart).toHaveBeenCalled());
    const [form, , periodId] = onStart.mock.calls[0];
    expect(form.width).toBe(20);
    expect(form.height).toBe(20);
    expect(form.initialPopulation).toBe(150);
    expect(periodId).toBe("cidade-media");
  });

  it("disables the size preset once a starting-point template is selected", async () => {
    stubFetch([{ periodId: "cidade-media", version: 1, source: "Cidade média", createdAtUtc: "" }]);
    render(<PresetStart onStart={() => {}} />);

    await screen.findByRole("option", { name: "Cidade média" });
    fireEvent.change(screen.getByLabelText("preset-starting-point"), { target: { value: "cidade-media" } });
    expect(screen.getByLabelText("preset-size")).toBeDisabled();
  });

  it("changes the visible landscape when seed changes", () => {
    stubFetch();
    render(<PresetStart onStart={() => {}} />);
    const colorsBefore = [...screen.getByTestId("preview-map-world").querySelectorAll("i")].map((cell) => cell.getAttribute("style"));

    fireEvent.change(screen.getByLabelText("preset-seed"), { target: { value: "91" } });

    const colorsAfter = [...screen.getByTestId("preview-map-world").querySelectorAll("i")].map((cell) => cell.getAttribute("style"));
    expect(colorsAfter).not.toEqual(colorsBefore);
  });
});
