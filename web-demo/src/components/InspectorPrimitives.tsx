import type { ReactNode } from "react";

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

/** Link discreto de "ver mais" — abre popup/drawer/view expandida, nunca troca o texto por um
 * botão gigante. */
export function SectionLink({ children, onClick, testId }: { children: ReactNode; onClick: () => void; testId?: string }) {
  return (
    <button type="button" className="section-link" onClick={onClick} data-testid={testId}>
      {children}
    </button>
  );
}
