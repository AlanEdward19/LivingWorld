import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { CriticalEventToast } from "../../src/components/CriticalEventToast";
import { NavigationStore } from "../../src/nav/NavigationStore";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

describe("CriticalEventToast", () => {
  it("shows the critical event's summary and tick (doc §172)", () => {
    render(<CriticalEventToast fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} onDismiss={() => {}} />);
    const critical = WORLD_FIXTURE.events.find((e) => e.severity === "critical")!;
    const toast = screen.getByTestId("critical-event-toast");
    expect(toast).toHaveTextContent(critical.summary);
    expect(toast).toHaveTextContent(critical.tick);
  });

  it("clicking 'View event' navigates to the Causal Explorer and dismisses", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    const onDismiss = vi.fn();
    render(<CriticalEventToast fixture={WORLD_FIXTURE} nav={nav} onDismiss={onDismiss} />);
    const critical = WORLD_FIXTURE.events.find((e) => e.severity === "critical")!;

    fireEvent.click(screen.getByText("View event"));

    expect(nav.current()).toEqual({ kind: "causal", eventId: critical.eventId });
    expect(onDismiss).toHaveBeenCalled();
  });

  it("clicking 'Dismiss' calls onDismiss without navigating", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    const onDismiss = vi.fn();
    render(<CriticalEventToast fixture={WORLD_FIXTURE} nav={nav} onDismiss={onDismiss} />);

    fireEvent.click(screen.getByText("Dismiss"));

    expect(onDismiss).toHaveBeenCalled();
    expect(nav.current()).toEqual({ kind: "world" });
  });

  it("renders nothing when the fixture has no critical event", () => {
    const fixtureWithoutCritical = { ...WORLD_FIXTURE, events: WORLD_FIXTURE.events.map((e) => ({ ...e, severity: "routine" as const })) };
    const { container } = render(
      <CriticalEventToast fixture={fixtureWithoutCritical} nav={new NavigationStore(WORLD_FIXTURE)} onDismiss={() => {}} />,
    );
    expect(container).toBeEmptyDOMElement();
  });
});
