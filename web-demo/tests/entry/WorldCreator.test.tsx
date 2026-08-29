import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { WorldCreatorShell } from "../../src/entry/creator/WorldCreatorShell";
import { EntryServicesProvider } from "../../src/entry/EntryContext";
import { MockWorldRepository } from "../../src/entry/repository/MockWorldRepository";
import { LocalStorageDraftRepository } from "../../src/entry/repository/LocalStorageDraftRepository";
import { MockWorldGenerationService } from "../../src/entry/repository/MockWorldGenerationService";

beforeEach(() => {
  vi.useFakeTimers();
});

afterEach(() => {
  cleanup();
  window.localStorage.clear();
  vi.useRealTimers();
});

function renderShell(onNavigate = vi.fn()) {
  const drafts = new LocalStorageDraftRepository();
  render(
    <EntryServicesProvider services={{ worlds: new MockWorldRepository(), drafts, generation: new MockWorldGenerationService() }}>
      <WorldCreatorShell onNavigate={onNavigate} />
    </EntryServicesProvider>,
  );
  return { onNavigate, drafts };
}

describe("WorldCreatorShell", () => {
  it("doc §39 — edits autosave (debounced) and show Saving.../Saved", async () => {
    const { drafts } = renderShell();
    const name = screen.getByTestId("field-name").querySelector("input")!;
    fireEvent.change(name, { target: { value: "Eldoria" } });

    await vi.advanceTimersByTimeAsync(500);
    expect(screen.getByTestId("draft-status")).toHaveTextContent("Saved");

    const saved = await drafts.listDrafts();
    expect(saved[0].world.name).toBe("Eldoria");
  });

  it("doc §37 — undo/redo restores previous field values", async () => {
    renderShell();
    const name = screen.getByTestId("field-name").querySelector("input")! as HTMLInputElement;
    fireEvent.change(name, { target: { value: "Eldoria" } });
    expect(name.value).toBe("Eldoria");

    fireEvent.keyDown(window, { key: "z", ctrlKey: true });
    expect((screen.getByTestId("field-name").querySelector("input") as HTMLInputElement).value).toBe("");

    fireEvent.keyDown(window, { key: "z", ctrlKey: true, shiftKey: true });
    expect((screen.getByTestId("field-name").querySelector("input") as HTMLInputElement).value).toBe("Eldoria");
  });

  it("doc §36 — a locked field survives Randomize World", async () => {
    renderShell();
    const nameInput = screen.getByTestId("field-name").querySelector("input")! as HTMLInputElement;
    fireEvent.change(nameInput, { target: { value: "Eldoria" } });
    fireEvent.click(within(screen.getByTestId("field-name")).getByRole("button", { name: "🔓" }));

    fireEvent.click(screen.getByTestId("randomize-world"));
    expect((screen.getByTestId("field-name").querySelector("input") as HTMLInputElement).value).toBe("Eldoria");
  });

  it("doc §41 — leaving with unsaved changes shows the guard; Discard clears the draft and returns to Main Menu", async () => {
    const { onNavigate, drafts } = renderShell();
    const name = screen.getByTestId("field-name").querySelector("input")!;
    fireEvent.change(name, { target: { value: "Eldoria" } });

    fireEvent.click(screen.getByTestId("creator-back"));
    expect(screen.getByTestId("unsaved-draft-guard")).toBeInTheDocument();

    fireEvent.click(screen.getByTestId("guard-discard"));
    await vi.advanceTimersByTimeAsync(0);
    expect(onNavigate).toHaveBeenCalledWith("/");
    expect(await drafts.listDrafts()).toHaveLength(0);
  });

  it("doc §44 — Generate World is blocked until a World Name is set", async () => {
    renderShell();
    fireEvent.click(screen.getByTestId("creator-review"));
    expect(screen.getByTestId("generate-world")).toBeDisabled();

    // switch back to overview to edit, then return to review
    fireEvent.click(screen.getByText("Overview"));
    fireEvent.change(screen.getByTestId("field-name").querySelector("input")!, { target: { value: "Eldoria" } });
    fireEvent.click(screen.getByTestId("creator-review"));
    expect(screen.getByTestId("generate-world")).not.toBeDisabled();
  });
});
