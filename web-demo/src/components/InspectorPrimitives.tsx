import type { MouseEvent, ReactNode } from "react";
import type { RelationshipKind, RelationshipStrength } from "../fixture/types";

const RELATIONSHIP_ICON: Record<RelationshipKind, string> = {
  family: "\u{1F46A}",
  romantic: "\u{1F49D}",
  friend: "\u{1F642}",
  professional: "\u{1F4BC}",
  rival: "\u{2694}\u{FE0F}",
};

const STRENGTH_TITLE: Record<RelationshipStrength, string> = {
  strong: "Strong bond",
  warm: "Warm",
  neutral: "Neutral",
  tense: "Tense",
};

/** Redesign doc §32 — header de seção compacto (11px uppercase muted), com contagem/link
 * opcional à direita ("HOUSEHOLDS   4" ou "HOUSEHOLDS   View all →"). */
export function SectionHeader({ title, trailing }: { title: string; trailing?: ReactNode }) {
  return (
    <div className="section-header">
      <span>{title}</span>
      {trailing !== undefined && <span>{trailing}</span>}
    </div>
  );
}

/** Redesign doc §30 — linha clicável de entidade relacionada (household/agent/place/etc.),
 * nome + metadado curto + chevron. Nunca a lista inteira espremida — quem renderiza decide
 * quantas linhas mostrar antes de oferecer "View all". */
export function EntityRow({
  title,
  meta,
  onClick,
  testId,
}: {
  title: string;
  meta?: string;
  onClick: () => void;
  testId?: string;
}) {
  return (
    <button type="button" className="entity-row" onClick={onClick} data-testid={testId}>
      <span className="entity-row-text">
        <span className="entity-row-title">{title}</span>
        {meta && <span className="entity-row-meta"> · {meta}</span>}
      </span>
      <span className="entity-row-chevron" aria-hidden="true">
        {">"}
      </span>
    </button>
  );
}

/** Redesign doc §50/§31 — chips discretos de status/condição, nunca barras de progresso. */
export function StatusChips({ items, testId }: { items: string[]; testId?: string }) {
  return (
    <ul className="status-chips" data-testid={testId}>
      {items.map((item) => (
        <li key={item} className="status-chip">
          {item}
        </li>
      ))}
    </ul>
  );
}

/** Redesign doc §31 — label e valor na MESMA linha, nunca um embaixo do outro. */
export function MetricRow({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="metric-row">
      <span className="metric-row-label">{label}</span>
      <span className="metric-row-value">{value}</span>
    </div>
  );
}

/** Linha de relacionamento estilo "aba social" (pedido do usuário 2026-08-26, "estilo aba
 * social do the sims") — ícone por categoria (`kind`) + nome + label + um indicador discreto de
 * força do vínculo, em vez de um `<li>Nome · label</li>` de texto corrido. */
export function RelationshipRow({
  name,
  label,
  kind,
  strength,
  onClick,
  testId,
}: {
  name: string;
  label: string;
  kind: RelationshipKind;
  strength: RelationshipStrength;
  onClick: () => void;
  testId?: string;
}) {
  return (
    <button type="button" className="relationship-row" onClick={onClick} data-testid={testId}>
      <span className="relationship-row-icon" aria-hidden="true">
        {RELATIONSHIP_ICON[kind]}
      </span>
      <span className="entity-row-text">
        <span className="entity-row-title">{name}</span>
        <span className="entity-row-meta"> · {label}</span>
      </span>
      <span
        className={`relationship-strength relationship-strength--${strength}`}
        title={STRENGTH_TITLE[strength]}
        aria-label={STRENGTH_TITLE[strength]}
      />
    </button>
  );
}

/** Botão Back no canto superior direito da view (saiu do Breadcrumb — usuário: back pertence à
 * view que o usuário está olhando, não à barra de navegação global). */
export function BackButton({ onClick }: { onClick: () => void }) {
  return (
    <button type="button" className="view-back-button" aria-label="Back" onClick={onClick}>
      ←
    </button>
  );
}

/** Link discreto de "ver mais" — abre popup/drawer/view expandida, nunca troca o texto por um
 * botão gigante. */
export function SectionLink({
  children,
  onClick,
  testId,
}: {
  children: ReactNode;
  /** Recebe o `MouseEvent` — quem abre um Popup usa `event.currentTarget` pra ancorar o painel
   * ao lado do próprio link clicado (pedido do usuário 2026-08-26: "abrir na mesma linha
   * horizontal do botão, e à esquerda", não flutuando fixo no topo da tela). */
  onClick: (event: MouseEvent<HTMLButtonElement>) => void;
  testId?: string;
}) {
  return (
    <button type="button" className="section-link" onClick={onClick} data-testid={testId}>
      {children}
    </button>
  );
}
