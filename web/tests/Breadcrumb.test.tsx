import { describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { Breadcrumb } from "../src/components/Breadcrumb";
import type { SpaceId } from "../src/map-engine/types";

describe("Breadcrumb", () => {
  it("shows the full ancestor chain for a Building space", () => {
    const building: SpaceId = { kind: "Building", buildingId: "42", cityId: "city-1" };
    render(<Breadcrumb space={building} onNavigate={() => {}} />);

    expect(screen.getByRole("button", { name: "Mundo" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Cidade city-1" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Prédio 42" })).toBeInTheDocument();
  });

  it("navigates to the clicked ancestor", () => {
    const building: SpaceId = { kind: "Building", buildingId: "42", cityId: "city-1" };
    const onNavigate = vi.fn();
    render(<Breadcrumb space={building} onNavigate={onNavigate} />);

    fireEvent.click(screen.getByRole("button", { name: "Cidade city-1" }));

    expect(onNavigate).toHaveBeenCalledWith({ kind: "City", cityId: "city-1" });
  });

  it("disables the current space's own breadcrumb item", () => {
    const city: SpaceId = { kind: "City", cityId: "city-1" };
    render(<Breadcrumb space={city} onNavigate={() => {}} />);

    expect(screen.getByRole("button", { name: "Cidade city-1" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Mundo" })).toBeEnabled();
  });

  it("shows the authored city name instead of the raw id once it's known", () => {
    const city: SpaceId = { kind: "City", cityId: "city-1" };
    render(<Breadcrumb space={city} onNavigate={() => {}} cityName="Vale Dourado" />);

    expect(screen.getByRole("button", { name: "Cidade Vale Dourado" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Cidade city-1" })).not.toBeInTheDocument();
  });

  it("still falls back to the id slice while the city name hasn't loaded yet", () => {
    const building: SpaceId = { kind: "Building", buildingId: "42", cityId: "city-1" };
    render(<Breadcrumb space={building} onNavigate={() => {}} />);

    expect(screen.getByRole("button", { name: "Cidade city-1" })).toBeInTheDocument();
  });
});
