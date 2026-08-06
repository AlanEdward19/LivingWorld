import { useState } from "react";
import { useRealtimeSnapshot } from "./hooks/useRealtimeSnapshot";
import { WorldMapView } from "./components/WorldMapView";
import { CityView } from "./components/CityView";
import { InteriorView } from "./components/InteriorView";
import { PlayerMoveControls } from "./components/PlayerMoveControls";
import { CreateWorldForm } from "./components/CreateWorldForm";
import { ViewerMode, focusScopeKey } from "./types";
import type { CitySnapshot, FocusScope, GlobalSnapshot, InteriorSnapshot } from "./types";

/// Fase 15, T8 (VTT-01..16): orquestra modo espectador/personagem e a pilha de drill-down
/// (mundo → cidade → interior) sobre o único hook de realtime — cada view só renderiza o
/// payload que já chegou do escopo atual, nenhuma lógica de FOW/validação mora no cliente.
export function App() {
  const [focus, setFocus] = useState<FocusScope>({ kind: "World" });
  const [mode, setMode] = useState<ViewerMode>(ViewerMode.Spectator);
  const [playerNpcId, setPlayerNpcId] = useState<number | undefined>(undefined);
  const [creatingWorld, setCreatingWorld] = useState(false);

  const { envelope, connected, error } = useRealtimeSnapshot<
    GlobalSnapshot | CitySnapshot | InteriorSnapshot
  >(focus, mode, mode === ViewerMode.Player ? playerNpcId : undefined);

  // Guarda contra a corrida entre trocar de escopo (setFocus) e o novo WebSocket ainda não ter
  // respondido: sem isso, o payload antigo (de outro escopo) rende no componente errado por um
  // ciclo — CityView recebendo um GlobalSnapshot, por exemplo.
  const payload = envelope?.scope?.scopeKey === focusScopeKey(focus) ? envelope.payload : null;

  return (
    <div>
      <header>
        <label>
          Modo:{" "}
          <select
            value={mode}
            onChange={(e) => setMode(Number(e.target.value) as ViewerMode)}
          >
            <option value={ViewerMode.Spectator}>Espectador</option>
            <option value={ViewerMode.Player}>Personagem</option>
          </select>
        </label>
        {mode === ViewerMode.Player && (
          <label>
            {" "}
            NPC:{" "}
            <input
              type="number"
              aria-label="player-npc-id"
              value={playerNpcId ?? ""}
              onChange={(e) =>
                setPlayerNpcId(e.target.value === "" ? undefined : Number(e.target.value))
              }
            />
          </label>
        )}{" "}
        <button type="button" onClick={() => setCreatingWorld((v) => !v)}>
          {creatingWorld ? "Cancelar" : "Criar mundo"}
        </button>
        {!connected && <span> reconectando…</span>}
        {error && <span role="alert"> {error}</span>}
      </header>

      {creatingWorld && (
        <CreateWorldForm
          onCreated={() => {
            setCreatingWorld(false);
            setFocus({ kind: "World" });
          }}
        />
      )}

      {!creatingWorld && focus.kind === "World" && payload && (
        <WorldMapView
          snapshot={payload as GlobalSnapshot}
          onSelectCity={(cityId) => setFocus({ kind: "City", cityId })}
        />
      )}

      {!creatingWorld && focus.kind === "City" && payload && (
        <>
          <CityView
            snapshot={payload as CitySnapshot}
            onSelectBuilding={(buildingId) =>
              setFocus({ kind: "Interior", buildingId, cityId: focus.cityId })
            }
            onBack={() => setFocus({ kind: "World" })}
          />
          {mode === ViewerMode.Player && playerNpcId !== undefined && (
            <PlayerMoveControls snapshot={payload as CitySnapshot} playerNpcId={playerNpcId} />
          )}
        </>
      )}

      {!creatingWorld && focus.kind === "Interior" && payload && (
        <InteriorView
          snapshot={payload as InteriorSnapshot}
          onBack={() => setFocus({ kind: "City", cityId: focus.cityId })}
        />
      )}
    </div>
  );
}
