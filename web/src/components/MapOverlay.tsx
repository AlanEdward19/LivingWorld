import { useEffect, useState } from "react";
import { fetchSnapshot } from "../api";
import { GridCanvas } from "./GridCanvas";
import { riverOverlayPoints, terrainColorLookup, worldMarkers } from "../worldMapData";
import { ViewerMode } from "../types";
import type { GlobalSnapshot } from "../types";

export interface MapOverlayProps {
  onClose: () => void;
}

/// T15 (fase 15, UX pass 2): "apertar M abre mapa, só visualização, igual em um RPG" — busca o
/// snapshot do mundo uma vez (não é realtime: é só uma consulta, não precisa acompanhar cada
/// tick) e renderiza em modo readOnly (sem clique/drill-down). M ou Esc fecha.
export function MapOverlay({ onClose }: MapOverlayProps) {
  const [snapshot, setSnapshot] = useState<GlobalSnapshot | null>(null);

  useEffect(() => {
    let cancelled = false;
    fetchSnapshot<GlobalSnapshot>({ kind: "World" }, ViewerMode.Spectator)
      .then((envelope) => {
        if (!cancelled) setSnapshot(envelope.payload);
      })
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape" || e.key.toLowerCase() === "m") onClose();
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [onClose]);

  return (
    <div className="map-overlay-backdrop" data-testid="map-overlay" onClick={onClose}>
      <div className="map-overlay-panel" onClick={(e) => e.stopPropagation()}>
        <h2>Mapa</h2>
        {snapshot ? (
          <GridCanvas
            width={snapshot.width}
            height={snapshot.height}
            cellColor={terrainColorLookup(snapshot)}
            overlayPoints={riverOverlayPoints(snapshot)}
            markers={worldMarkers(snapshot)}
            zoom={12}
            readOnly
          />
        ) : (
          <p>Carregando…</p>
        )}
        <button type="button" onClick={onClose}>
          Fechar (M/Esc)
        </button>
      </div>
    </div>
  );
}
