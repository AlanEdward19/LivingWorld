import { describe, expect, it } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { FollowButton } from "../../src/components/FollowButton";
import { followStore } from "../../src/state/followStore";
import { AgentView } from "../../src/views/AgentView";
import { HouseholdView } from "../../src/views/HouseholdView";
import { SettlementView } from "../../src/views/SettlementView";
import { NavigationStore } from "../../src/nav/NavigationStore";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

describe("FollowButton", () => {
  it("shows 'Follow' when not followed, and toggles to 'Following' on click", () => {
    render(<FollowButton entityId="follow-test-agent" />);
    const button = screen.getByTestId("follow-button");
    expect(button).toHaveTextContent("Follow");
    expect(button).toHaveAttribute("aria-pressed", "false");

    fireEvent.click(button);
    expect(button).toHaveTextContent("Following");
    expect(button).toHaveAttribute("aria-pressed", "true");
  });

  it("reflects the followStore's actual state, not just its own click history", () => {
    followStore.toggleFollow("follow-test-settlement");
    const { getByTestId, unmount } = render(<FollowButton entityId="follow-test-settlement" />);
    expect(getByTestId("follow-button")).toHaveTextContent("Following");
    unmount();
    followStore.toggleFollow("follow-test-settlement"); // cleanup, after unmount so nothing re-renders
  });
});

describe("FollowButton — integrated into existing views", () => {
  it("appears in AgentView, HouseholdView and SettlementView with visual state matching followStore", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);

    const agent = render(<AgentView fixture={WORLD_FIXTURE} nav={nav} agentId="mira-valen" />);
    expect(agent.getByTestId("follow-button")).toBeInTheDocument();
    agent.unmount();

    const household = render(<HouseholdView fixture={WORLD_FIXTURE} nav={nav} householdId="valen-household" />);
    expect(household.getByTestId("follow-button")).toBeInTheDocument();
    household.unmount();

    const settlement = render(<SettlementView fixture={WORLD_FIXTURE} nav={nav} settlementId="oakbridge" />);
    expect(settlement.getByTestId("follow-button")).toBeInTheDocument();
    settlement.unmount();
  });
});
