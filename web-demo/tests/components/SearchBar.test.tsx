import { describe, expect, it } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { SearchBar } from "../../src/components/SearchBar";
import { NavigationStore } from "../../src/nav/NavigationStore";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

describe("SearchBar", () => {
  it("shows no results section before typing anything", () => {
    render(<SearchBar fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} />);
    expect(screen.queryByTestId("search-results")).not.toBeInTheDocument();
  });

  it("searching 'Mira' returns Mira grouped under People", () => {
    render(<SearchBar fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} />);
    fireEvent.change(screen.getByTestId("search-input"), { target: { value: "Mira" } });
    expect(within(screen.getByTestId("search-group-people")).getByText("Mira Valen")).toBeInTheDocument();
  });

  it("clicking a People result navigates to that agent", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<SearchBar fixture={WORLD_FIXTURE} nav={nav} />);
    fireEvent.change(screen.getByTestId("search-input"), { target: { value: "Mira" } });
    fireEvent.click(within(screen.getByTestId("search-group-people")).getByText("Mira Valen"));
    expect(nav.current()).toEqual({ kind: "agent", id: "mira-valen" });
  });

  it("a query with no matches shows an explicit empty state per category, not an undefined/broken list", () => {
    render(<SearchBar fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} />);
    fireEvent.change(screen.getByTestId("search-input"), { target: { value: "zzz-nothing-matches-zzz" } });
    expect(within(screen.getByTestId("search-group-people")).getByText("No people found.")).toBeInTheDocument();
    expect(within(screen.getByTestId("search-group-places")).getByText("No places found.")).toBeInTheDocument();
    expect(within(screen.getByTestId("search-group-households")).getByText("No households found.")).toBeInTheDocument();
    expect(within(screen.getByTestId("search-group-events")).getByText("No events found.")).toBeInTheDocument();
    expect(within(screen.getByTestId("search-group-threads")).getByText("No threads found.")).toBeInTheDocument();
  });
});
