import { StarfieldBackground } from "../entry/StarfieldBackground";
import { BackButton } from "../components/InspectorPrimitives";
import { CognitionPanel } from "./CognitionPanel";
import { useSandboxEngine } from "./useSandboxEngine";

export interface SandboxViewProps {
  onNavigate: (path: string) => void;
}

/** Main-menu "Cognition Sandbox" — runs only the synthetic decision generator (`sandboxEngine`),
 * no world/NPCs involved, so the pipeline/neural view can be watched in isolation. */
export function SandboxView({ onNavigate }: SandboxViewProps) {
  const { entries, playing, togglePlaying, speedLabel, cycleSpeed } = useSandboxEngine();

  return (
    <div data-testid="sandbox-view" className="entry-cosmos-bg">
      <StarfieldBackground />
      <div className="sandbox-frame">
        <BackButton onClick={() => onNavigate("/")} />
        <h1>Cognition Sandbox</h1>
        <p>Watch a synthetic mind think — same decision-trace shape as a real NPC, no world attached.</p>

        <div className="sandbox-controls">
          <button type="button" data-testid="sandbox-toggle-play" aria-pressed={playing} onClick={togglePlaying}>
            {playing ? "Pause" : "Play"}
          </button>
          <button type="button" data-testid="sandbox-cycle-speed" onClick={cycleSpeed}>
            Speed: {speedLabel}
          </button>
        </div>

        <CognitionPanel entries={entries} live />
      </div>
    </div>
  );
}
