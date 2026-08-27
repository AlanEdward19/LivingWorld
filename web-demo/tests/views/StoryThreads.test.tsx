import { describe, expect, it } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { StoryThreads } from "../../src/views/StoryThreads";
import { NavigationStore } from "../../src/nav/NavigationStore";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

const THREAD = WORLD_FIXTURE.storyThreads.find((t) => t.id === "oakbridge-food-crisis")!;

describe("StoryThreads", () => {
  it("shows the exact fixture numbers for The Oakbridge Food Crisis (doc#126)", () => {
    render(<StoryThreads fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} />);
    expect(screen.getByText("The Oakbridge Food Crisis")).toBeInTheDocument();
    expect(screen.getByTestId("story-thread-stats")).toHaveTextContent(
      `${THREAD.eventIds.length} events · ${THREAD.householdIds.length} households · ${THREAD.agentIds.length} Agents · ${THREAD.systemsTouched.length} systems`,
    );
    expect(THREAD.eventIds).toHaveLength(18);
    expect(THREAD.householdIds).toHaveLength(4);
    expect(THREAD.agentIds).toHaveLength(11);
    expect(THREAD.systemsTouched).toHaveLength(6);
  });

  it("clicking the card opens the Causal Explorer at the thread's root event", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<StoryThreads fixture={WORLD_FIXTURE} nav={nav} />);
    fireEvent.click(screen.getByTestId("story-thread-card"));
    expect(nav.current()).toEqual({ kind: "causal", eventId: "evt-harvest-below-normal" });
  });
});
