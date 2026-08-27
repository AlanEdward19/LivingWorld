import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { EntityRow, MetricRow, SectionHeader, SectionLink, StatusChips } from "../../src/components/InspectorPrimitives";

describe("SectionHeader (redesign doc §32)", () => {
  it("renders the title and an optional trailing count/link", () => {
    render(<SectionHeader title="Households" trailing={4} />);
    expect(screen.getByText("Households")).toBeInTheDocument();
    expect(screen.getByText("4")).toBeInTheDocument();
  });

  it("renders without trailing when omitted", () => {
    const { container } = render(<SectionHeader title="Currently" />);
    expect(container.querySelectorAll(".section-header span")).toHaveLength(1);
  });
});

describe("EntityRow (redesign doc §30)", () => {
  it("renders title, meta, and calls onClick", () => {
    const onClick = vi.fn();
    render(<EntityRow title="Valen Household" meta="4 members" onClick={onClick} />);
    expect(screen.getByText("Valen Household")).toBeInTheDocument();
    expect(screen.getByText("4 members", { exact: false })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button"));
    expect(onClick).toHaveBeenCalled();
  });

  it("renders without meta when omitted", () => {
    render(<EntityRow title="Oakbridge" onClick={() => {}} />);
    expect(screen.getByText("Oakbridge")).toBeInTheDocument();
  });
});

describe("StatusChips (redesign doc §50/§31)", () => {
  it("renders one chip per item", () => {
    render(<StatusChips items={["Hungry", "Tired", "Healthy"]} />);
    for (const item of ["Hungry", "Tired", "Healthy"]) {
      expect(screen.getByText(item)).toBeInTheDocument();
    }
    expect(screen.getAllByRole("listitem")).toHaveLength(3);
  });
});

describe("MetricRow (redesign doc §31)", () => {
  it("renders label and value on the same row", () => {
    render(<MetricRow label="Population" value={42} />);
    expect(screen.getByText("Population")).toBeInTheDocument();
    expect(screen.getByText("42")).toBeInTheDocument();
  });
});

describe("SectionLink", () => {
  it("renders its text and calls onClick", () => {
    const onClick = vi.fn();
    render(<SectionLink onClick={onClick}>View all →</SectionLink>);
    fireEvent.click(screen.getByText("View all →"));
    expect(onClick).toHaveBeenCalled();
  });
});
