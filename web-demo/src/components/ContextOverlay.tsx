import { useEffect, type ReactNode } from "react";

export interface ContextOverlayProps {
  title: string;
  onClose: () => void;
  children: ReactNode;
}

/**
 * Nível 3 (redesign doc §19) — Popup/Drawer: conteúdo que excede o espaço natural do Inspector,
 * mas NÃO merece o Center Overlay inteiro (esse já existe, AD-021 — `CenterStage`'s
 * causal/timeline/life/feed/threads). Deliberadamente "não bloqueante" (doc): sem backdrop
 * escurecido, o resto da tela continua visível/usável por trás — só um click-catcher
 * transparente pra fechar ao clicar fora.
 */
function ContextOverlay({ variant, title, onClose, children }: ContextOverlayProps & { variant: "popup" | "drawer" }) {
  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") onClose();
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [onClose]);

  return (
    <div data-testid={`${variant}-backdrop`} onClick={onClose}>
      <div data-testid={`${variant}-panel`} onClick={(event) => event.stopPropagation()} role="dialog" aria-label={title}>
        <div data-testid={`${variant}-header`}>
          <h3>{title}</h3>
          <button type="button" data-testid={`${variant}-close`} aria-label="Close" onClick={onClose}>
            ×
          </button>
        </div>
        <div data-testid={`${variant}-body`}>{children}</div>
      </div>
    </div>
  );
}

/** Popup pequeno (doc §19) — 5-15 itens, filtros, ações rápidas. Ex.: relationships completas,
 * physical details, "explain decision". */
export function Popup(props: ContextOverlayProps) {
  return <ContextOverlay variant="popup" {...props} />;
}

/** Drawer médio (doc §19) — 420-520px, pra listas maiores (people list completa, org members,
 * building rooms, inventário). Ainda sem consumidor nesta rodada (só Agent Inspector foi
 * redesenhado) — pronto pra Household/Settlement/Organization na próxima. */
export function Drawer(props: ContextOverlayProps) {
  return <ContextOverlay variant="drawer" {...props} />;
}
