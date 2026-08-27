import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { Drawer, Popup } from "../../src/components/ContextOverlay";

afterEach(() => {
  cleanup();
});

describe("Popup (redesign doc §19 — Nível 3, não bloqueante)", () => {
  it("renders the title and children", () => {
    render(
      <Popup title="Physical details" onClose={() => {}}>
        <p>Body content</p>
      </Popup>,
    );
    expect(screen.getByTestId("popup-panel")).toHaveTextContent("Physical details");
    expect(screen.getByTestId("popup-panel")).toHaveTextContent("Body content");
  });

  it("clicking the close button calls onClose", () => {
    const onClose = vi.fn();
    render(
      <Popup title="X" onClose={onClose}>
        content
      </Popup>,
    );
    fireEvent.click(screen.getByTestId("popup-close"));
    expect(onClose).toHaveBeenCalled();
  });

  it("clicking the backdrop calls onClose", () => {
    const onClose = vi.fn();
    render(
      <Popup title="X" onClose={onClose}>
        content
      </Popup>,
    );
    fireEvent.click(screen.getByTestId("popup-backdrop"));
    expect(onClose).toHaveBeenCalled();
  });

  it("clicking inside the panel does NOT call onClose (stopPropagation)", () => {
    const onClose = vi.fn();
    render(
      <Popup title="X" onClose={onClose}>
        content
      </Popup>,
    );
    fireEvent.click(screen.getByTestId("popup-panel"));
    expect(onClose).not.toHaveBeenCalled();
  });

  it("pressing Escape calls onClose", () => {
    const onClose = vi.fn();
    render(
      <Popup title="X" onClose={onClose}>
        content
      </Popup>,
    );
    fireEvent.keyDown(window, { key: "Escape" });
    expect(onClose).toHaveBeenCalled();
  });

  // Bug real reportado pelo usuário (2026-08-26): o popup abria fixo perto do topo da tela,
  // longe do link clicado. Precisa alinhar com o TOPO do anchor e ficar À ESQUERDA dele.
  it("anchors to the trigger's row and sits to its left when anchorRect is given", () => {
    const anchorRect = { top: 500, left: 900, right: 1000, bottom: 526, width: 100, height: 26 } as DOMRect;
    render(
      <Popup title="X" onClose={() => {}} anchorRect={anchorRect}>
        content
      </Popup>,
    );
    const panel = screen.getByTestId("popup-panel");
    expect(panel.style.top).toBe("500px");
    expect(panel.style.left).toBe("auto");
    // right = distância do anchor.left até a borda da viewport, mais uma folga — garante que o
    // painel termina ANTES do início do link (à esquerda dele), nunca por cima.
    expect(panel.style.right).toBe(`${window.innerWidth - 900 + 8}px`);
  });

  it("falls back to the CSS default position when no anchorRect is given", () => {
    render(
      <Popup title="X" onClose={() => {}}>
        content
      </Popup>,
    );
    const panel = screen.getByTestId("popup-panel");
    expect(panel.style.top).toBe("");
    expect(panel.style.right).toBe("");
  });

  it("is non-blocking — no dark backdrop element intercepts the whole screen visually (transparent by design, see tokens.css)", () => {
    // Sanity check estrutural: o backdrop existe (pra fechar ao clicar fora) mas não é um modal
    // bloqueante como o Center Overlay (AD-021) — não tem texto/whatever escondendo o resto.
    render(
      <Popup title="X" onClose={() => {}}>
        content
      </Popup>,
    );
    expect(screen.getByTestId("popup-backdrop")).toBeInTheDocument();
    expect(screen.queryByTestId("center-stage-overlay-backdrop")).not.toBeInTheDocument();
  });
});

describe("Drawer (redesign doc §19 — mesma mecânica do Popup, painel maior)", () => {
  it("renders with drawer-* testids, not popup-*", () => {
    render(
      <Drawer title="All households" onClose={() => {}}>
        <p>List</p>
      </Drawer>,
    );
    expect(screen.getByTestId("drawer-panel")).toHaveTextContent("All households");
    expect(screen.getByTestId("drawer-panel")).toHaveTextContent("List");
    expect(screen.queryByTestId("popup-panel")).not.toBeInTheDocument();
  });

  it("clicking the close button calls onClose", () => {
    const onClose = vi.fn();
    render(
      <Drawer title="X" onClose={onClose}>
        content
      </Drawer>,
    );
    fireEvent.click(screen.getByTestId("drawer-close"));
    expect(onClose).toHaveBeenCalled();
  });

  it("clicking the backdrop calls onClose, clicking the panel does not", () => {
    const onClose = vi.fn();
    render(
      <Drawer title="X" onClose={onClose}>
        content
      </Drawer>,
    );
    fireEvent.click(screen.getByTestId("drawer-panel"));
    expect(onClose).not.toHaveBeenCalled();
    fireEvent.click(screen.getByTestId("drawer-backdrop"));
    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
