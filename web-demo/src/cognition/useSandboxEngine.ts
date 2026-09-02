import { useEffect, useRef, useState } from "react";
import { tick } from "./sandboxEngine";
import type { CognitionTraceEntry } from "./types";

const MAX_HISTORY = 50; // same window as the backend's `NpcCognitionLog.DefaultWindowSize`
const SPEEDS = [2000, 1000, 500] as const; // ms per tick: slow/normal/fast

/** Only stateful/timer-owning piece of the sandbox — everything else (`sandboxEngine.tick`) stays
 * pure and testable without fake timers. */
export function useSandboxEngine() {
  const [entries, setEntries] = useState<CognitionTraceEntry[]>([]);
  const [playing, setPlaying] = useState(true);
  const [speedIndex, setSpeedIndex] = useState(1);
  const nextTickRef = useRef(0);

  useEffect(() => {
    if (!playing) return;
    const interval = setInterval(() => {
      setEntries((previous) => {
        const previousTrace = previous.length > 0 ? previous[previous.length - 1].trace : null;
        const entry = tick(nextTickRef.current, previousTrace);
        nextTickRef.current += 1;
        const next = [...previous, entry];
        return next.length > MAX_HISTORY ? next.slice(next.length - MAX_HISTORY) : next;
      });
    }, SPEEDS[speedIndex]);
    return () => clearInterval(interval);
  }, [playing, speedIndex]);

  return {
    entries,
    playing,
    togglePlaying: () => setPlaying((value) => !value),
    speedIndex,
    cycleSpeed: () => setSpeedIndex((index) => (index + 1) % SPEEDS.length),
    speedLabel: (["Slow", "Normal", "Fast"] as const)[speedIndex],
  };
}
