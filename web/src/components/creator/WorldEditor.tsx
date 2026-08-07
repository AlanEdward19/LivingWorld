// Fase 15.1, T24: casca do editor visual do World Creator — mesma instância de `MapView` do
// Observer (nenhuma segunda implementação de mapa), `EntityInspector` reusado tal como está.
// Como nenhum mundo existe ainda (isto roda ANTES de `POST /worlds/create`), o editor monta suas
// próprias instâncias de `SimulationStore`/`ViewStore`/`SelectionStore` — nunca as do App, que só
// existem depois de um mundo estar de pé — alimentadas por fontes "nulas" (nunca resolvem,
// porque nada aqui assina stream nenhum: `entitiesOf`/`currentPayload` sempre vazios, e todo o
// conteúdo visível vem de `staticEntities`/`cells`, como `WorldMapView` já faz pra cidade/prédio
// derivados). Terreno pintado usa a MESMA função de cor de `MapGridEditor.tsx:122-126` — célula
// sem pintura fica sem cor (mapa é procedural por Seed só no servidor, nada é inventado aqui).
import { useMemo, useState, useSyncExternalStore } from "react";
import { MapView } from "../MapView";
import { EntityInspector } from "../inspector/EntityInspector";
import { createWorld } from "../../api";
import { colorById } from "../../colorById";
import { parseCsvInts, scenarioFormToJson, type ScenarioFormState } from "../../scenarioDefaults";
import { SimulationStore } from "../../state/simulationStore";
import { ViewStore } from "../../state/viewStore";
import { SelectionStore } from "../../state/selectionStore";
import { CATEGORY_COLOR } from "../../map-engine/categoryColors";
import { addSettlement, eraseCell, paintTerrainCell, paintWaterCell, type PaintTool } from "./tools/paint";
import type { CellSource } from "../../map-engine/renderer";
import type { AuthoritativeEntity, SpaceId } from "../../map-engine/types";
import type { PortalSource, SnapshotSource, TickStreamSource } from "../../data/sources";

export interface WorldEditorProps {
  initialForm: ScenarioFormState;
  onCreated?: (npcCount: number) => void;
  viewport?: { width: number; height: number };
}

const WORLD: SpaceId = { kind: "World" };
const LOD_THRESHOLDS = { aggregate: 4, token: 10, detail: 18 };
const DEFAULT_VIEWPORT = { width: 900, height: 600 };

// Nada aqui assina snapshot/stream/portal de verdade — o editor nunca chama `observeSpace`, e
// `PortalSource` só é consultada por navegação entre espaços (não existe, ainda, no editor).
function neverLoadingSnapshotSource(): SnapshotSource {
  return { load: () => new Promise(() => {}) };
}
function neverStreamingTickSource(): TickStreamSource {
  return { subscribe: () => () => {} };
}
function noPortalSource(): PortalSource {
  return { portalsOf: () => [] };
}

export function WorldEditor({ initialForm, onCreated, viewport = DEFAULT_VIEWPORT }: WorldEditorProps) {
  const [form, setForm] = useState(initialForm);
  const [status, setStatus] = useState<"idle" | "submitting" | "error">("idle");
  const [error, setError] = useState<string | null>(null);
  // T25: ferramenta ativa no mapa — "select" (default) deixa clique cair no hit-test/seleção
  // normal do MapView; qualquer outra pinta/adiciona assentamento em vez de selecionar.
  const [tool, setTool] = useState<PaintTool>("select");
  const terrainIds = useMemo(() => parseCsvInts(form.terrainIds), [form.terrainIds]);
  const biomeIds = useMemo(() => parseCsvInts(form.biomeIds), [form.biomeIds]);
  const [selectedTerrain, setSelectedTerrain] = useState(terrainIds[0] ?? 1);
  const [selectedBiome, setSelectedBiome] = useState(biomeIds[0] ?? 0);
  // Última célula tocada por uma ferramenta — leitura, nunca a forma primária de posicionar
  // (a ferramenta + clique é; o campo aqui só mostra onde o último clique caiu).
  const [lastCell, setLastCell] = useState<{ x: number; y: number } | null>(null);

  const stores = useMemo(
    () => ({
      simulationStore: new SimulationStore(neverLoadingSnapshotSource(), neverStreamingTickSource()),
      viewStore: new ViewStore(noPortalSource()),
      selectionStore: new SelectionStore(),
    }),
    // eslint-disable-next-line react-hooks/exhaustive-deps -- instâncias vivem por montagem do editor, não por form
    [],
  );

  const selection = useSyncExternalStore(
    (onStoreChange) => stores.selectionStore.subscribe(onStoreChange),
    () => stores.selectionStore.current(),
  );

  const cells: CellSource = useMemo(
    () => ({
      width: Math.max(1, form.width),
      height: Math.max(1, form.height),
      colorAt: (x, y) => {
        const painted = form.cells[`${x},${y}`];
        if (!painted) {
          return undefined;
        }
        return painted.water ? "#3a7bd5" : colorById(painted.terrain);
      },
    }),
    [form.width, form.height, form.cells],
  );

  // Assentamento = a cidade que o motor funda no tick 0 (mesma lista de `Settlements` no
  // scenario JSON) — selecionável via o hit-test padrão do MapView quando a ferramenta ativa é
  // "selecionar" (T25 intercepta o clique só quando outra ferramenta está ativa).
  const settlementEntities: AuthoritativeEntity[] = useMemo(
    () =>
      form.settlements.map((s, i) => ({
        ref: { kind: "city" as const, id: `settlement:${i}`, space: WORLD },
        position: { x: s.x, y: s.y },
        size: { w: 1, h: 1 },
        sizeIsDerived: false,
        color: CATEGORY_COLOR.city,
      })),
    [form.settlements],
  );

  function handlePaintClick(cell: { x: number; y: number }): boolean {
    if (tool === "select") {
      return false;
    }
    setLastCell(cell);
    setForm((f) => {
      if (tool === "settlement") {
        return { ...f, settlements: addSettlement(f.settlements, cell.x, cell.y) };
      }
      if (tool === "erase") {
        return { ...f, cells: eraseCell(f.cells, cell.x, cell.y) };
      }
      if (tool === "water") {
        return { ...f, cells: paintWaterCell(f.cells, cell.x, cell.y, selectedTerrain, selectedBiome) };
      }
      return { ...f, cells: paintTerrainCell(f.cells, cell.x, cell.y, selectedTerrain, selectedBiome) };
    });
    return true;
  }

  async function handleCreate() {
    setStatus("submitting");
    setError(null);
    try {
      const response = await createWorld(scenarioFormToJson(form));
      if (!response.ok) {
        setError(`criar mundo falhou: ${response.status} ${await response.text()}`);
        setStatus("error");
        return;
      }
      const body = (await response.json()) as { npcCount: number };
      setStatus("idle");
      onCreated?.(body.npcCount);
    } catch (err) {
      setError(String(err));
      setStatus("error");
    }
  }

  return (
    <div className="world-editor" data-testid="world-editor">
      <div className="world-editor-toolbar" data-testid="world-editor-toolbar">
        <label>
          Ferramenta:{" "}
          <select aria-label="tool-select" value={tool} onChange={(e) => setTool(e.target.value as PaintTool)}>
            <option value="select">selecionar</option>
            <option value="terrain">pintar terreno</option>
            <option value="water">pintar água</option>
            <option value="erase">apagar célula</option>
            <option value="settlement">assentamento</option>
          </select>
        </label>{" "}
        {tool === "terrain" && (
          <>
            <label>
              Terreno:{" "}
              <select
                aria-label="tool-terrain"
                value={selectedTerrain}
                onChange={(e) => setSelectedTerrain(Number(e.target.value))}
              >
                {terrainIds.map((id) => (
                  <option key={id} value={id}>
                    {id}
                  </option>
                ))}
              </select>
            </label>{" "}
            <label>
              Bioma:{" "}
              <select
                aria-label="tool-biome"
                value={selectedBiome}
                onChange={(e) => setSelectedBiome(Number(e.target.value))}
              >
                {biomeIds.map((id) => (
                  <option key={id} value={id}>
                    {id}
                  </option>
                ))}
              </select>
            </label>{" "}
          </>
        )}
        <span aria-label="tool-last-cell">
          {lastCell ? `Célula: (${lastCell.x}, ${lastCell.y})` : "Célula: —"}
        </span>
      </div>

      <div className="world-editor-body">
        <MapView
          space={WORLD}
          viewport={viewport}
          cells={cells}
          layers={[]}
          lodThresholds={LOD_THRESHOLDS}
          simulationStore={stores.simulationStore}
          viewStore={stores.viewStore}
          selectionStore={stores.selectionStore}
          staticEntities={settlementEntities}
          initialCamera={{ center: { x: cells.width / 2, y: cells.height / 2 }, scale: 10 }}
          onPaintClick={handlePaintClick}
        />

        {selection ? (
          <EntityInspector
            selectionStore={stores.selectionStore}
            simulationStore={stores.simulationStore}
            viewStore={stores.viewStore}
          />
        ) : (
          <aside className="side-panel" data-testid="world-general-config">
            <h3>Configuração geral</h3>
            <dl>
              <dt>Tamanho</dt>
              <dd>
                {form.width} × {form.height}
              </dd>
              <dt>Seed</dt>
              <dd>{form.seed}</dd>
              <dt>População inicial</dt>
              <dd>{form.initialPopulation}</dd>
              <dt>Assentamentos</dt>
              <dd>{form.settlements.length}</dd>
              <dt>Economia</dt>
              <dd>{form.economyEnabled ? "habilitada" : "desabilitada"}</dd>
              <dt>Cidades</dt>
              <dd>{form.citiesEnabled ? "habilitadas" : "desabilitadas"}</dd>
            </dl>
            <button type="button" onClick={handleCreate} disabled={status === "submitting"}>
              {status === "submitting" ? "Criando…" : "Criar mundo"}
            </button>
            {error && <p role="alert">{error}</p>}
          </aside>
        )}
      </div>
    </div>
  );
}

