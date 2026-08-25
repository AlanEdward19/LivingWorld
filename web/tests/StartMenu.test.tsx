import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, act } from "@testing-library/react";
import { StartMenu } from "../src/components/StartMenu";

describe("StartMenu", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("calls onSettings immediately (no exit transition)", () => {
    const onSettings = vi.fn();
    render(<StartMenu onCreateWorld={vi.fn()} onContinue={vi.fn()} onSettings={onSettings} />);

    fireEvent.click(screen.getByRole("button", { name: "Configurações" }));

    expect(onSettings).toHaveBeenCalledOnce();
  });

  it("plays the warp exit before calling onContinue", () => {
    const onContinue = vi.fn();
    render(<StartMenu onCreateWorld={vi.fn()} onContinue={onContinue} onSettings={vi.fn()} />);

    fireEvent.click(screen.getByRole("button", { name: "Continuar" }));
    expect(onContinue).not.toHaveBeenCalled();

    act(() => {
      vi.advanceTimersByTime(1000);
    });
    expect(onContinue).toHaveBeenCalledOnce();
  });

  it("plays the planet-dive exit before calling onCreateWorld, and ignores further clicks mid-exit", () => {
    const onCreateWorld = vi.fn();
    const onContinue = vi.fn();
    render(<StartMenu onCreateWorld={onCreateWorld} onContinue={onContinue} onSettings={vi.fn()} />);

    fireEvent.click(screen.getByRole("button", { name: "Criar mundo" }));
    expect(onCreateWorld).not.toHaveBeenCalled();

    // botões desabilitados durante a transição — segundo clique não deve disparar outro caminho
    fireEvent.click(screen.getByRole("button", { name: "Continuar" }));

    act(() => {
      vi.advanceTimersByTime(1000);
    });
    expect(onCreateWorld).toHaveBeenCalledOnce();
    expect(onContinue).not.toHaveBeenCalled();
  });
});
