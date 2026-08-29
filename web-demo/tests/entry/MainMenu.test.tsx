import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { MainMenu } from "../../src/entry/MainMenu";
import { EntryServicesProvider } from "../../src/entry/EntryContext";
import { MockWorldRepository, EmptyMockWorldRepository } from "../../src/entry/repository/MockWorldRepository";
import { LocalStorageDraftRepository } from "../../src/entry/repository/LocalStorageDraftRepository";
import { MockWorldGenerationService } from "../../src/entry/repository/MockWorldGenerationService";
import type { DraftRepository } from "../../src/entry/repository/DraftRepository";

afterEach(() => {
  cleanup();
  window.localStorage.clear();
});

function renderMenu(worlds = new MockWorldRepository(), drafts: DraftRepository = new LocalStorageDraftRepository()) {
  const onNavigate = vi.fn();
  render(
    <EntryServicesProvider services={{ worlds, drafts, generation: new MockWorldGenerationService() }}>
      <MainMenu onNavigate={onNavigate} />
    </EntryServicesProvider>,
  );
  return onNavigate;
}

describe("MainMenu", () => {
  it("doc §21 — first launch: Create enabled, Continue disabled with 'No worlds yet'", async () => {
    renderMenu(new EmptyMockWorldRepository());
    await waitFor(() => expect(screen.getByTestId("action-continue")).toHaveTextContent("No worlds yet"));
    expect(screen.getByTestId("action-create")).not.toBeDisabled();
    expect(screen.getByTestId("action-continue")).toBeDisabled();
    expect(screen.queryByTestId("action-browse-worlds")).not.toBeInTheDocument();
  });

  it("doc §20 — with worlds: offers 'Continue {recent}' and Browse Worlds", async () => {
    const onNavigate = renderMenu();
    await waitFor(() => expect(screen.getByTestId("action-continue")).toHaveTextContent("Continue Eldoria"));
    screen.getByTestId("action-continue").click();
    expect(onNavigate).toHaveBeenCalledWith("/worlds/eldoria");
  });

  it("Browse Worlds navigates to /worlds, Create to /create, Settings to /settings", async () => {
    const onNavigate = renderMenu();
    await waitFor(() => screen.getByTestId("action-browse-worlds"));
    screen.getByTestId("action-browse-worlds").click();
    expect(onNavigate).toHaveBeenCalledWith("/worlds");
    screen.getByTestId("action-create").click();
    expect(onNavigate).toHaveBeenCalledWith("/create");
    screen.getByTestId("action-settings").click();
    expect(onNavigate).toHaveBeenCalledWith("/settings");
  });

  it("doc §79 — shows an error state with retry when loading worlds fails", async () => {
    const failing = { listWorlds: () => Promise.reject(new Error("down")), getWorld: () => Promise.resolve(null), addWorld: () => Promise.resolve() };
    render(
      <EntryServicesProvider services={{ worlds: failing as any, drafts: new LocalStorageDraftRepository(), generation: new MockWorldGenerationService() }}>
        <MainMenu onNavigate={vi.fn()} />
      </EntryServicesProvider>,
    );
    await waitFor(() => expect(screen.getByTestId("main-menu-error")).toBeInTheDocument());
  });
});
