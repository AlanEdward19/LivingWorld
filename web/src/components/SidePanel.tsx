export interface SidePanelProps {
  title: string;
  onClose: () => void;
  action?: { label: string; onClick: () => void };
  children: React.ReactNode;
}

/// T13 (fase 15, UX pass 2): painel deslizante à direita — info de cidade/NPC ao clicar um
/// marcador no grid. Genérico (não sabe o que está mostrando), views passam título+conteúdo.
export function SidePanel({ title, onClose, action, children }: SidePanelProps) {
  return (
    <aside className="side-panel" data-testid="side-panel">
      <button type="button" className="side-panel-close" aria-label="fechar-painel" onClick={onClose}>
        ×
      </button>
      <h3>{title}</h3>
      <div className="side-panel-content">{children}</div>
      {action && (
        <button type="button" onClick={action.onClick}>
          {action.label}
        </button>
      )}
    </aside>
  );
}
