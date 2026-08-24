import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { NpcAuthoringControls } from "../../src/components/inspector/NpcAuthoringControls";
import type { AuthoringSource } from "../../src/data/sources";

function source(): AuthoringSource {
  return {
    powerCatalog: vi.fn().mockResolvedValue([
      { id: "energia", source: "artefato", effects: ["construct.create:2x1:40:24:green"], mode: "Active", reliability: "Guaranteed" },
    ]),
    grantPower: vi.fn().mockResolvedValue(undefined),
    revokePower: vi.fn().mockResolvedValue(undefined),
    invokePower: vi.fn().mockResolvedValue(undefined),
    rewritePersonality: vi.fn().mockResolvedValue(undefined),
    breakRelationships: vi.fn().mockResolvedValue(undefined),
    forceAction: vi.fn().mockResolvedValue(undefined),
  };
}

describe("NpcAuthoringControls", () => {
  it("grants a catalog power and places its effect at the authored cell through the source", async () => {
    const commands = source();
    const refresh = vi.fn().mockResolvedValue(undefined);
    const view = render(<NpcAuthoringControls npcId={7} source={commands} powerIds={[]} personality={{}} location={{ x: 2, y: 3 }} onRefresh={refresh} />);
    await screen.findByRole("option", { name: "energia" });

    fireEvent.click(screen.getByRole("button", { name: "Conceder poder" }));
    await waitFor(() => expect(commands.grantPower).toHaveBeenCalledWith(7, "energia"));
    expect(await screen.findByText("Concessão concluído.")).toBeInTheDocument();
    expect(refresh).toHaveBeenCalledTimes(1);
    view.rerender(<NpcAuthoringControls npcId={7} source={commands} powerIds={["energia"]} personality={{}} location={{ x: 2, y: 3 }} onRefresh={refresh} />);
    fireEvent.click(screen.getByRole("button", { name: "Revogar poder" }));
    await waitFor(() => expect(commands.revokePower).toHaveBeenCalledWith(7, "energia"));
    expect(await screen.findByText("Revogação concluído.")).toBeInTheDocument();
    expect(refresh).toHaveBeenCalledTimes(2);
    fireEvent.change(screen.getByLabelText("Célula X do constructo"), { target: { value: "9" } });
    fireEvent.change(screen.getByLabelText("Célula Y do constructo"), { target: { value: "4" } });
    fireEvent.click(screen.getByRole("button", { name: "Criar/usar na célula" }));

    await waitFor(() => expect(commands.invokePower).toHaveBeenCalledWith(7, "energia", 7, { x: 9, y: 4 }));
  });

  it("sends personality, relationship and immediate action as explicit commands", async () => {
    const commands = source();
    render(<NpcAuthoringControls npcId={7} source={commands} powerIds={[]} personality={{ extroversion: 20 }} location={{ x: 0, y: 0 }} onRefresh={vi.fn().mockResolvedValue(undefined)} />);
    await screen.findByRole("option", { name: "energia" });
    fireEvent.click(screen.getByText("Personalidade"));
    fireEvent.click(screen.getByRole("button", { name: "Raivoso" }));
    fireEvent.change(screen.getByLabelText("Extroversão"), { target: { value: "88" } });
    fireEvent.click(screen.getByRole("button", { name: "Salvar personalidade" }));
    await waitFor(() => expect(commands.rewritePersonality).toHaveBeenCalledWith(7, expect.objectContaining({ extroversion: 88, emotionalStability: 10, impulsivity: 90 })));

    fireEvent.click(screen.getByText("Relações e comportamento"));
    fireEvent.change(screen.getByLabelText("Outro NPC da relação"), { target: { value: "8" } });
    fireEvent.click(screen.getByRole("button", { name: "Romper relação entre os dois" }));
    await waitFor(() => expect(commands.breakRelationships).toHaveBeenCalledWith(7, 8));
    fireEvent.click(screen.getByRole("button", { name: "Dar ordem agora" }));
    await waitFor(() => expect(commands.forceAction).toHaveBeenCalledWith(7, 5));
  });

  it("leaves uncertain resolution to the authoritative engine", async () => {
    const commands = source();
    vi.mocked(commands.powerCatalog).mockResolvedValue([
      { id: "incerto", source: "ritual", effects: ["npc.health:5"], mode: "Active", reliability: "ResolutionCheck" },
    ]);
    render(<NpcAuthoringControls npcId={7} source={commands} powerIds={["incerto"]} personality={{}} location={{ x: 0, y: 0 }} onRefresh={vi.fn().mockResolvedValue(undefined)} />);
    await screen.findByRole("option", { name: "incerto" });
    fireEvent.click(screen.getByRole("button", { name: "Usar no NPC" }));

    expect(screen.queryByLabelText("Resultado da resolução")).not.toBeInTheDocument();
    await waitFor(() => expect(commands.invokePower).toHaveBeenCalledWith(7, "incerto", 7, undefined));
  });

  it("shows an authoritative rejection and does not refresh a command that failed", async () => {
    const commands = source();
    vi.mocked(commands.grantPower).mockRejectedValue(new Error("PowerId: descritor ausente"));
    const refresh = vi.fn().mockResolvedValue(undefined);
    render(<NpcAuthoringControls npcId={7} source={commands} powerIds={[]} personality={{}} location={{ x: 0, y: 0 }} onRefresh={refresh} />);
    await screen.findByRole("option", { name: "energia" });
    fireEvent.click(screen.getByRole("button", { name: "Conceder poder" }));

    expect(await screen.findByText("PowerId: descritor ausente")).toBeInTheDocument();
    expect(refresh).not.toHaveBeenCalled();
  });
});
