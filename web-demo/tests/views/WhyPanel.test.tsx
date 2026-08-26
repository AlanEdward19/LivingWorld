import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { WhyPanel } from "../../src/views/WhyPanel";

describe("WhyPanel", () => {
  const factors = [
    { text: "household food is low", linkedEventId: "evt-valen-purchase-failed" },
    { text: "grain prices rose", linkedEventId: "evt-grain-prices-rose" },
    { text: "she is hungry", linkedEventId: "evt-mira-very-hungry" },
  ];

  it("renders every factor's human-readable text", () => {
    render(<WhyPanel factors={factors} onFactorClick={() => {}} />);
    for (const factor of factors) {
      expect(screen.getByText(factor.text)).toBeInTheDocument();
    }
  });

  it("clicking a factor with a linkedEventId calls onFactorClick with that event id", () => {
    const onFactorClick = vi.fn();
    render(<WhyPanel factors={factors} onFactorClick={onFactorClick} />);
    fireEvent.click(screen.getByText("grain prices rose"));
    expect(onFactorClick).toHaveBeenCalledWith("evt-grain-prices-rose");
  });

  it("renders a factor with no linkedEventId as plain text, not clickable", () => {
    const onFactorClick = vi.fn();
    render(<WhyPanel factors={[{ text: "no known cause" }]} onFactorClick={onFactorClick} />);
    const item = screen.getByText("no known cause");
    expect(item.tagName).toBe("SPAN");
    fireEvent.click(item);
    expect(onFactorClick).not.toHaveBeenCalled();
  });
});
