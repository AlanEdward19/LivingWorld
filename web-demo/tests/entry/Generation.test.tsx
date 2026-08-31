import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { GenerationView } from "../../src/entry/GenerationView";
import { EntryServicesProvider } from "../../src/entry/EntryContext";
import { MockWorldRepository } from "../../src/entry/repository/MockWorldRepository";
import { LocalStorageDraftRepository } from "../../src/entry/repository/LocalStorageDraftRepository";
import { MockWorldGenerationService } from "../../src/entry/repository/MockWorldGenerationService";
import { newDraft } from "../../src/entry/creator/draftState";

afterEach(() => cleanup());

function renderGeneration(onComplete = vi.fn(), onCancel = vi.fn()) {
  const draft = { ...newDraft("d1"), world: { ...newDraft("d1").world, name: "Eldoria" } };
  render(
    <EntryServicesProvider
      services={{ worlds: new MockWorldRepository(), drafts: new LocalStorageDraftRepository(), generation: new MockWorldGenerationService(5) }}
    >
      <GenerationView draft={draft} onComplete={onComplete} onCancel={onCancel} />
    </EntryServicesProvider>,
  );
}

describe("GenerationView", () => {
  it("doc §47-51 — runs mock stages to completion, then Enter World hands off the result", async () => {
    const onComplete = vi.fn();
    renderGeneration(onComplete);

    expect(screen.getByTestId("generation-view")).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId("generation-complete")).toBeInTheDocument(), { timeout: 3000 });

    fireEvent.click(screen.getByTestId("enter-world"));
    expect(onComplete).toHaveBeenCalledWith(expect.objectContaining({ worldId: "eldoria", worldName: "Eldoria" }));
  });

  it("Cancel stops generation and calls onCancel", async () => {
    const onCancel = vi.fn();
    renderGeneration(vi.fn(), onCancel);
    fireEvent.click(screen.getByTestId("generation-cancel"));
    expect(onCancel).toHaveBeenCalled();
    // let the aborted generate() promise settle before the test/module unmounts
    await new Promise((resolve) => setTimeout(resolve, 10));
  });
});
