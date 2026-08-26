import { describe, expect, it } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { TimelineBar } from "../../src/components/TimelineBar";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

describe("TimelineBar", () => {
  it("starts collapsed, showing the most recent tick", () => {
    render(<TimelineBar fixture={WORLD_FIXTURE} />);
    const lastEvent = WORLD_FIXTURE.events[WORLD_FIXTURE.events.length - 1];
    expect(screen.getByTestId("timeline-bar-toggle")).toHaveTextContent(lastEvent.tick);
    expect(screen.queryByTestId("timeline-bar-content")).not.toBeInTheDocument();
  });

  it("expands to show the full world Timeline on click", () => {
    render(<TimelineBar fixture={WORLD_FIXTURE} />);
    fireEvent.click(screen.getByTestId("timeline-bar-toggle"));
    expect(screen.getByTestId("timeline-bar-content")).toBeInTheDocument();
    expect(screen.getByTestId("timeline-view")).toBeInTheDocument();
  });

  it("collapses again on a second click", () => {
    render(<TimelineBar fixture={WORLD_FIXTURE} />);
    fireEvent.click(screen.getByTestId("timeline-bar-toggle"));
    fireEvent.click(screen.getByTestId("timeline-bar-toggle"));
    expect(screen.queryByTestId("timeline-bar-content")).not.toBeInTheDocument();
  });
});
