import { useEffect, useRef, useState } from "react";
import { buildWebSocketUrl, fetchSnapshot } from "../api";
import { focusScopeKey } from "../types";
import type { FocusScope, ViewerMode, VisualSnapshotEnvelope } from "../types";

export interface RealtimeSnapshotState<TPayload> {
  envelope: VisualSnapshotEnvelope<TPayload> | null;
  connected: boolean;
  error: string | null;
}

/// Fase 15, T8 (VTT-02, VTT-03): conecta ao WebSocket de T3 — primeiro frame é o snapshot,
/// frames seguintes são delta. O payload de delta (T3/T7) não tem o mesmo formato do snapshot
/// completo para todo escopo ainda; em vez de mesclar formatos heterogêneos no cliente, um delta
/// só dispara reidratação via HTTP (mesmo espírito do edge case de spec.md "reidratar snapshot
/// + replay sem escrita de mundo" — aqui via re-subscribe, não replay por cursor).
export function useRealtimeSnapshot<TPayload>(
  scope: FocusScope,
  mode: ViewerMode,
  playerNpcId?: number,
  enabled = true,
): RealtimeSnapshotState<TPayload> {
  const [envelope, setEnvelope] = useState<VisualSnapshotEnvelope<TPayload> | null>(null);
  const [connected, setConnected] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const receivedFirstFrame = useRef(false);

  useEffect(() => {
    if (!enabled) return;
    setEnvelope(null);
    setError(null);
    receivedFirstFrame.current = false;

    const socket = new WebSocket(buildWebSocketUrl(scope, mode, playerNpcId));

    socket.onopen = () => setConnected(true);
    socket.onerror = () => setError("conexão realtime falhou");
    socket.onclose = () => setConnected(false);
    socket.onmessage = (event) => {
      if (!receivedFirstFrame.current) {
        receivedFirstFrame.current = true;
        setEnvelope(JSON.parse(event.data) as VisualSnapshotEnvelope<TPayload>);
        return;
      }
      fetchSnapshot<TPayload>(scope, mode, playerNpcId)
        .then(setEnvelope)
        .catch(() => setError("falha ao reidratar após delta"));
    };

    return () => socket.close();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [focusScopeKey(scope), mode, playerNpcId, enabled]);

  return { envelope, connected, error };
}
