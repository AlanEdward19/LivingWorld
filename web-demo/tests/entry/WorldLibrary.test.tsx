import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { WorldLibrary } from "../../src/entry/WorldLibrary";
import { EntryServicesProvider } from "../../src/entry/EntryContext";
import { MockWorldRepository, EmptyMockWorldRepository } from "../../src/entry/repository/MockWorldRepository";
import { LocalStorageDraftRepository } from "../../src/entry/repository/LocalStorageDraftRepository";
import { MockWorldGenerationService } from "../../src/entry/repository/MockWorldGenerationService";

afterEach(() => {
  cleanup();
  window.localStorage.clear();
});

function renderLibrary(worlds = new MockWorldRepository()) {
  const onNavigate = vi.fn();
  render(
    <EntryServicesProvider services={{ worlds, drafts: new LocalStorageDraftRepository(), generation: new MockWorldGenerationService() }}>
      <WorldLibrary onNavigate={onNavigate} />
    </EntryServicesProvider>,
  );
  return onNavigate;
}

describe("WorldLibrary", () => {
  it("doc §61 — empty state offers Create New World / Back to Main Menu", async () => {
    const onNavigate = renderLibrary(new EmptyMockWorldRepository());
    await waitFor(() => expect(screen.getByTestId("world-library-empty")).toBeInTheDocument());
    fireEvent.click(screen.getByText("Create New World"));
    expect(onNavigate).toHaveBeenCalledWith("/create");
  });

  it("doc §53-55 — lists worlds, search filters, Continue navigates to /worlds/:id", async () => {
    const onNavigate = renderLibrary();
    await waitFor(() => expect(screen.getAllByTestId("world-card")).toHaveLength(3));

    fireEvent.change(screen.getByTestId("world-search"), { target: { value: "mars" } });
    expect(screen.getAllByTestId("world-card")).toHaveLength(1);

    vi.useFakeTimers();
    try {
      fireEvent.click(screen.getAllByTestId("world-card-continue")[0]);
      // Continue plays the planet zoom-dive (usePlanetZoomExit) before navigating.
      await vi.advanceTimersByTimeAsync(1000);
    } finally {
      vi.useRealTimers();
    }
    expect(onNavigate).toHaveBeenCalledWith("/worlds/mars-2149");
  });

  it("shows the recent world's planet by default, swaps to the selected save on click", async () => {
    renderLibrary();
    await waitFor(() => expect(screen.getByTestId("planet-label")).toHaveTextContent("Eldoria"));

    fireEvent.click(screen.getAllByTestId("world-card-select")[1]);
    expect(screen.getByTestId("planet-label")).toHaveTextContent("Mars 2149");
  });

  it("← Main Menu navigates to /", async () => {
    const onNavigate = renderLibrary();
    await waitFor(() => screen.getByTestId("library-back"));
    fireEvent.click(screen.getByTestId("library-back"));
    expect(onNavigate).toHaveBeenCalledWith("/");
  });
});
