import { describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { InteriorView } from "../src/components/InteriorView";
import type { InteriorSnapshot } from "../src/types";

describe("InteriorView", () => {
  it("shows the unmodeled-occupancy note when OccupancyModeled is false", () => {
    const snapshot: InteriorSnapshot = {
      id: { value: 8 },
      city: { value: "city-1" },
      buildingTypeId: 2,
      occupancyModeled: false,
    };

    render(<InteriorView snapshot={snapshot} onBack={() => {}} />);

    expect(screen.getByRole("note")).toHaveTextContent("ainda não é modelada");
  });

  it("calls onBack when the back button is clicked", () => {
    const onBack = vi.fn();
    const snapshot: InteriorSnapshot = {
      id: { value: 8 },
      city: { value: "city-1" },
      buildingTypeId: 2,
      occupancyModeled: false,
    };

    render(<InteriorView snapshot={snapshot} onBack={onBack} />);
    fireEvent.click(screen.getByText(/cidade/));

    expect(onBack).toHaveBeenCalled();
  });
});
