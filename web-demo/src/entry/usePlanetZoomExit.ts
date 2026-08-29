import { useState } from "react";

/** Matches the old `web/` project's `CREATE_EXIT_MS` — CSS durations in tokens.css must match. */
const ZOOM_EXIT_MS = 950;

/**
 * "Continue" dives into the planet instead of a flat cut — same technique the old `web/`
 * project used for its start-menu exit: toggle a CSS class that scales+brightens the planet
 * from its own center, fade the surrounding content, then a blackout masks the swap to the
 * next screen. `prefers-reduced-motion` skips straight to navigating.
 */
export function usePlanetZoomExit(onNavigate: (path: string) => void) {
  const [exiting, setExiting] = useState(false);

  function zoomInto(path: string) {
    if (exiting) return;
    const reducedMotion = typeof window !== "undefined" && window.matchMedia?.("(prefers-reduced-motion: reduce)").matches;
    if (reducedMotion) {
      onNavigate(path);
      return;
    }
    setExiting(true);
    setTimeout(() => onNavigate(path), ZOOM_EXIT_MS);
  }

  return { exiting, zoomInto };
}
