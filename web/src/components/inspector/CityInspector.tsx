// Fase 15.1, T15: inspector de cidade — os 6 indicadores de `CityPopulationQuery`
// (spec.md "Inspector de NPC e Cidade" AC1), lidos do payload já carregado, nunca recalculados
// no cliente.
//
// SPEC_DEVIATION: os 5 indicadores alem de população só existem no payload de `CitySnapshot`
// (T30 os adiciona lá, não em `GlobalCityMarker`) — então só aparecem quando o
// `SimulationStore` já está observando ESSA cidade (`currentPayload` bate o `cityId`). Ao
// selecionar uma cidade a partir do WorldSpace (o caminho real hoje — nenhuma view adiciona a
// própria cidade como entidade dentro de si mesma), só `population` está disponível, porque é
// o único campo que `GlobalCityMarker` carrega. É um gap real de arquitetura (contexto.md),
// não uma escolha de UI: mostrar os outros 5 inventados seria pior que omiti-los.
import { FollowButton } from "./FollowButton";
import type { SimulationStore } from "../../state/simulationStore";
import type { ViewStore } from "../../state/viewStore";
import type { FutureCitySnapshot, FutureGlobalSnapshot } from "../../data/contracts";

export interface CityInspectorProps {
  cityId: string;
  simulationStore: SimulationStore;
  viewStore: ViewStore;
}

const WORLD = { kind: "World" as const };

export function CityInspector({ cityId, simulationStore, viewStore }: CityInspectorProps) {
  const citySpace = { kind: "City" as const, cityId };
  const citySnapshot = simulationStore.currentPayload<FutureCitySnapshot>(citySpace);
  const hasFullIndicators = citySnapshot?.id.value === cityId;

  const worldSnapshot = simulationStore.currentPayload<FutureGlobalSnapshot>(WORLD);
  const worldMarker = worldSnapshot?.cities.find((c) => c.id.value === cityId);
  const name = (hasFullIndicators ? citySnapshot!.name : undefined) || worldMarker?.name;

  return (
    <div>
      <h3>Cidade {name || cityId.slice(0, 8)}</h3>

      <dl>
        <dt>População</dt>
        <dd>{hasFullIndicators ? citySnapshot!.indicators.population : worldMarker?.population ?? "—"}</dd>

        {hasFullIndicators ? (
          <>
            <dt>Riqueza</dt>
            <dd>{citySnapshot!.indicators.wealth}</dd>
            <dt>Saúde</dt>
            <dd>{citySnapshot!.indicators.health}</dd>
            <dt>Desigualdade</dt>
            <dd>{citySnapshot!.indicators.inequality}</dd>
            <dt>Economia</dt>
            <dd>{citySnapshot!.indicators.economy}</dd>
            <dt>Habitação</dt>
            <dd>{citySnapshot!.indicators.housing}</dd>
          </>
        ) : (
          <p role="note">Indicadores completos disponíveis ao abrir a cidade.</p>
        )}
      </dl>

      <div className="entity-inspector-actions">
        <FollowButton entityRef={{ kind: "city", id: cityId, space: WORLD }} viewStore={viewStore} />
        <button type="button" onClick={() => viewStore.enter({ kind: "City", cityId })}>
          Abrir
        </button>
      </div>
    </div>
  );
}
