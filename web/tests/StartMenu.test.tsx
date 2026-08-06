import { describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { StartMenu } from "../src/components/StartMenu";

describe("StartMenu", () => {
  it("calls the matching handler for each button", () => {
    const onCreateWorld = vi.fn();
    const onContinue = vi.fn();
    const onSettings = vi.fn();

    render(
      <StartMenu onCreateWorld={onCreateWorld} onContinue={onContinue} onSettings={onSettings} />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Continuar" }));
    fireEvent.click(screen.getByRole("button", { name: "Criar mundo" }));
    fireEvent.click(screen.getByRole("button", { name: "Configurações" }));

    expect(onContinue).toHaveBeenCalledOnce();
    expect(onCreateWorld).toHaveBeenCalledOnce();
    expect(onSettings).toHaveBeenCalledOnce();
  });
});
