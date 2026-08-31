import { StarfieldBackground } from "./StarfieldBackground";

/** Doc §71-72 — placeholder shell, routes only; real settings aren't specced yet. */
export function SettingsView({ onNavigate }: { onNavigate: (path: string) => void }) {
  return (
    <div data-testid="settings-view" className="entry-cosmos-bg">
      <StarfieldBackground />
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
