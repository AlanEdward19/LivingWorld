import { apiBaseUrl } from "../api";

export interface SettingsViewProps {
  onBack: () => void;
}

/// Placeholder simples (decisão do usuário 2026-08-06, ver .specs/STATE.md): só o que já existe
/// de fato configurável no cliente hoje (URL da API, via VITE_API_BASE_URL) — sem inventar
/// opções que ainda não têm efeito nenhum no sistema.
export function SettingsView({ onBack }: SettingsViewProps) {
  return (
    <div data-testid="settings-view">
      <button type="button" onClick={onBack}>
        ← menu
      </button>
      <h2>Configurações</h2>
      <p>
        URL da API: <code>{apiBaseUrl() || "(mesma origem)"}</code>
      </p>
      <p>Mais opções em breve.</p>
    </div>
  );
}
