import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { LifeView } from "../../src/views/LifeView";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

const MIRA = WORLD_FIXTURE.agents.find((a) => a.id === "mira-valen")!;

describe("LifeView", () => {
  it("shows all of Mira's life milestones from the fixture (doc#122)", () => {
    render(<LifeView fixture={WORLD_FIXTURE} agentId="mira-valen" />);
    const list = screen.getByTestId("life-milestones");
    expect(MIRA.lifeMilestones.length).toBe(8);
    for (const milestone of MIRA.lifeMilestones) {
      expect(list).toHaveTextContent(milestone.label);
    }
  });

  it("renders milestones in the fixture's chronological order", () => {
    render(<LifeView fixture={WORLD_FIXTURE} agentId="mira-valen" />);
    const items = screen.getByTestId("life-milestones").querySelectorAll("li");
    expect(items).toHaveLength(MIRA.lifeMilestones.length);
    items.forEach((item, index) => {
      expect(item.textContent).toContain(MIRA.lifeMilestones[index].label);
    });
  });
});
