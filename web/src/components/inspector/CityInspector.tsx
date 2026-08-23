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
import { useEffect, useState, useSyncExternalStore } from "react";
import { FollowButton } from "./FollowButton";
import type { SimulationStore } from "../../state/simulationStore";
import type { ViewStore } from "../../state/viewStore";
import type { FutureCitySnapshot, FutureGlobalSnapshot } from "../../data/contracts";
import type { NarrativeSources } from "../../data/sources";
import { ConstructionProgressHud } from "../ConstructionProgressHud";

export interface CityInspectorProps {
  cityId: string;
  simulationStore: SimulationStore;
  viewStore: ViewStore;
  /** T7 (LWV-05): crônica da cidade. Opcional — ausente em contextos que ainda não têm essa fonte. */
  narrativeSources?: NarrativeSources;
}

const WORLD = { kind: "World" as const };

function CityChronicle({ cityId, currentTick, source }: {
  cityId: string; currentTick: number; source: NarrativeSources["chronicle"];
}) {
  const [prose, setProse] = useState<string | undefined>();

  useEffect(() => {
    setProse(undefined);
    let cancelled = false;
    void source.load(cityId, 0, currentTick).then((result) => {
      if (!cancelled) setProse(result.prose);
    });
    return () => { cancelled = true; };
  }, [cityId, currentTick, source]);

  return (
    <section aria-labelledby="city-chronicle-title">
      <h4 id="city-chronicle-title">Crônica</h4>
      {prose === undefined ? <p role="status">Carregando crônica…</p> : <p>{prose}</p>}
    </section>
  );
}

export function CityInspector({ cityId, simulationStore, viewStore, narrativeSources }: CityInspectorProps) {
  const citySpace = { kind: "City" as const, cityId };
  const citySnapshot = simulationStore.currentPayload<FutureCitySnapshot>(citySpace);
  const hasFullIndicators = citySnapshot?.id.value === cityId;

  const worldSnapshot = simulationStore.currentPayload<FutureGlobalSnapshot>(WORLD);
  const worldMarker = worldSnapshot?.cities.find((c) => c.id.value === cityId);
  const livingCity = useSyncExternalStore(
    (onStoreChange) => simulationStore.subscribe(onStoreChange),
    () => simulationStore.livingStateOf(WORLD).cities.get(cityId) ?? null,
  );
  const name = (hasFullIndicators ? citySnapshot!.name : undefined) || livingCity?.name || worldMarker?.name;
  const population = hasFullIndicators
    ? citySnapshot!.indicators.population
    : livingCity?.population ?? worldMarker?.population ?? "—";
  const currentTick = simulationStore.livingStateOf(WORLD).tick;

  return (
    <div>
      <h3>Cidade {name || cityId.slice(0, 8)}</h3>

      <dl>
        <dt>População</dt>
        <dd>{population}</dd>
        {livingCity?.foundedFromCityId && (
          <>
            <dt>Origem</dt>
            <dd>Assentamento fundado a partir de outra cidade; a população agregada mudou de sítio.</dd>
          </>
        )}

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

      <ConstructionProgressHud processes={simulationStore.livingStateOf(citySpace).processes.values()} />

      {narrativeSources && (
        <CityChronicle cityId={cityId} currentTick={currentTick} source={narrativeSources.chronicle} />
      )}

      <div className="entity-inspector-actions">
        <FollowButton entityRef={{ kind: "city", id: cityId, space: WORLD }} viewStore={viewStore} />
        <button type="button" onClick={() => viewStore.enter({ kind: "City", cityId })}>
          Abrir
        </button>
      </div>
    </div>
  );
}
