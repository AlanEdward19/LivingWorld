import type { InteriorSnapshot } from "../types";

export interface InteriorViewProps {
  snapshot: InteriorSnapshot;
  onBack: () => void;
}

/// Fase 15, T8 (VTT-03): foco de interior — identidade do prédio é real; ocupação não é
/// (InteriorProjector.OccupancyModeled, T5), então o cliente mostra isso explicitamente em vez
/// de fingir uma lista de moradores que o servidor nunca envia.
export function InteriorView({ snapshot, onBack }: InteriorViewProps) {
  return (
    <div data-testid="interior-view">
      <button type="button" onClick={onBack}>
        ← cidade
      </button>
      <h2>Prédio {snapshot.id.value}</h2>
      <p>Tipo: {snapshot.buildingTypeId}</p>
      {!snapshot.occupancyModeled && <p role="note">Ocupação por interior ainda não é modelada.</p>}
    </div>
  );
}
