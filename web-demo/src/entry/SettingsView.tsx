/** Doc §71-72 — placeholder shell, routes only; real settings aren't specced yet. */
export function SettingsView({ onNavigate }: { onNavigate: (path: string) => void }) {
  return (
    <div data-testid="settings-view">
      <header>
        <button type="button" onClick={() => onNavigate("/")}>
          ← Main Menu
        </button>
        <h1>Settings</h1>
      </header>
      <p>Nothing to configure yet.</p>
    </div>
  );
}
