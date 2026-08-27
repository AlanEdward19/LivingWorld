import { useEffect, type CSSProperties, type ReactNode } from "react";

export interface ContextOverlayProps {
  title: string;
  onClose: () => void;
  children: ReactNode;
  /** Posição do elemento que abriu o popup (`event.currentTarget.getBoundingClientRect()`) —
   * bug real reportado pelo usuário: o popup abria fixo perto do topo da tela, longe do link
   * clicado. Agora abre alinhado com o TOPO do link, encostado à ESQUERDA dele — "na mesma
   * linha horizontal do botão... à esquerda". Sem anchor, cai no fallback fixo do CSS (Drawer
   * nunca usa isso — é um painel docado na borda, não flutuante). */
  anchorRect?: DOMRect | null;
}

/**
 * Nível 3 (redesign doc §19) — Popup/Drawer: conteúdo que excede o espaço natural do Inspector,
 * mas NÃO merece o Center Overlay inteiro (esse já existe, AD-021 — `CenterStage`'s
 * causal/timeline/life/feed/threads). Deliberadamente "não bloqueante" (doc): sem backdrop
 * escurecido, o resto da tela continua visível/usável por trás — só um click-catcher
 * transparente pra fechar ao clicar fora.
 */
/** Popup máximo (largura + margem) usado só pra manter o painel dentro da viewport quando o
 * anchor está perto de uma borda — mantido em sincronia manual com `width`/`max-height` do CSS
 * (`[data-testid="popup-panel"]`); não vale a pena medir o DOM real pra um clamp tão simples. */
const VIEWPORT_MARGIN = 8;

function ContextOverlay({ variant, title, onClose, children, anchorRect }: ContextOverlayProps & { variant: "popup" | "drawer" }) {
  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") onClose();
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [onClose]);

  // Bug real reportado pelo usuário: um clamp preventivo (assumindo altura máxima de popup)
  // empurrava o painel pra bem longe da linha do botão mesmo quando o conteúdo real era curto.
  // Alinha exatamente com o topo do link clicado — "mesma linha horizontal, à esquerda" — e usa
  // `max-height`/`overflow-y` do CSS (`popup-panel`) pra lidar com o caso raro de estourar o
  // fundo da viewport, em vez de sacrificar o alinhamento pro caso comum.
  const anchoredStyle: CSSProperties | undefined =
    variant === "popup" && anchorRect
      ? {
          top: Math.max(VIEWPORT_MARGIN, anchorRect.top),
          right: Math.max(VIEWPORT_MARGIN, window.innerWidth - anchorRect.left + VIEWPORT_MARGIN),
          left: "auto",
        }
      : undefined;

  return (
    <div data-testid={`${variant}-backdrop`} onClick={onClose}>
      <div data-testid={`${variant}-panel`} style={anchoredStyle} onClick={(event) => event.stopPropagation()} role="dialog" aria-label={title}>
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
