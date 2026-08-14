// Fase 15.1, T31: implementação real de `TickStreamSource` — WebSocket `/visual/ws` tipado
// (RealtimeGateway/TickLoopService). A primeira mensagem do socket é o snapshot completo
// (mesmo shape de `VisualSnapshotEnvelope`) — descartada aqui porque `SimulationStore` já carrega
// o snapshot inicial via `SnapshotSource.load`; só mensagens de delta (`VisualDeltaEnvelope`,
// reconhecidas pelo campo `toCursor`, ausente no snapshot) chamam `onDelta` com o `ScopeTickDelta`
// desembrulhado. `onclose`/`onerror` disparam `onDrop`; o `unsubscribe` retornado neutraliza os
// handlers antes de fechar, então parar de observar por troca de espaço nunca conta como "queda".
import type { TickStreamSource } from "../sources";
import type { ScopeTickDelta } from "../contracts";
import type { SpaceId } from "../../map-engine/types";
import { ViewerMode } from "../../types";
import { buildWebSocketUrl } from "../../api";
import { spaceIdToFocusScope } from "./focusScope";

interface DeltaEnvelopeWire {
  fromCursor: { sequence: number };
  toCursor: { sequence: number };
  payload: ScopeTickDelta;
}

function isDeltaEnvelope(message: unknown): message is DeltaEnvelopeWire {
  return typeof message === "object" && message !== null && "toCursor" in message && "payload" in message;
}

export class RealTickStreamSource implements TickStreamSource {
  subscribe(space: SpaceId, onDelta: (delta: ScopeTickDelta) => void, onDrop?: () => void): () => void {
    const socket = new WebSocket(buildWebSocketUrl(spaceIdToFocusScope(space), ViewerMode.Spectator));

    socket.onmessage = (event) => {
      const message: unknown = JSON.parse(event.data as string);
      if (isDeltaEnvelope(message)) {
        // Metadados não enumeráveis preservam o payload legado para consumidores existentes,
        // mas permitem ao store rejeitar duplicatas e detectar lacunas.
        Object.defineProperties(message.payload, {
          fromSequence: { value: message.fromCursor.sequence, enumerable: false },
          sequence: { value: message.toCursor.sequence, enumerable: false },
        });
        onDelta(message.payload);
      }
    };
    socket.onclose = () => onDrop?.();
    socket.onerror = () => onDrop?.();

    return () => {
      socket.onclose = null;
      socket.onerror = null;
      socket.close();
    };
  }
}
