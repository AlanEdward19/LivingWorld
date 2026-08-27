import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { FamilyTree } from "../../src/views/FamilyTree";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

describe("FamilyTree", () => {
  it("shows Mira's parents row empty, spouse and children for the Valen family", () => {
    render(<FamilyTree fixture={WORLD_FIXTURE} agentId="mira-valen" onSelectAgent={() => {}} />);
    const tree = screen.getByTestId("family-tree");
    expect(tree).toHaveTextContent("Tomas Valen");
    expect(tree).toHaveTextContent("Eli Valen");
    expect(tree).toHaveTextContent("Nora Valen");
    expect(tree).toHaveTextContent("Mira Valen");
  });

  it("shows Eli's parents and sibling", () => {
    render(<FamilyTree fixture={WORLD_FIXTURE} agentId="eli-valen" onSelectAgent={() => {}} />);
    const tree = screen.getByTestId("family-tree");
    expect(tree).toHaveTextContent("Mira Valen");
    expect(tree).toHaveTextContent("Tomas Valen");
    expect(tree).toHaveTextContent("Nora Valen");
  });

  it("shows an empty state for someone with no recorded family (Rowan)", () => {
    render(<FamilyTree fixture={WORLD_FIXTURE} agentId="rowan" onSelectAgent={() => {}} />);
    expect(screen.getByText("No recorded family for Rowan.")).toBeInTheDocument();
  });

  it("clicking a relative navigates via onSelectAgent, but the focus node itself is disabled", () => {
    const onSelectAgent = vi.fn();
    render(<FamilyTree fixture={WORLD_FIXTURE} agentId="mira-valen" onSelectAgent={onSelectAgent} />);
    fireEvent.click(screen.getByText("Tomas Valen"));
    expect(onSelectAgent).toHaveBeenCalledWith("tomas-valen");

    onSelectAgent.mockClear();
    fireEvent.click(screen.getByText("Mira Valen"));
    expect(onSelectAgent).not.toHaveBeenCalled();
  });
});
