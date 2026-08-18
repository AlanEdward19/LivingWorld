import { useEffect, useMemo, useRef, useState, useSyncExternalStore } from "react";
import { MapView } from "../MapView";
import { generateBuildingFootprint, MATERIAL_COLOR, roofColorFor } from "../../map-engine/buildingFootprint";
import { CATEGORY_COLOR } from "../../map-engine/categoryColors";
import type { AuthoritativeEntity, EntityRef, EntityRotation, SpaceId } from "../../map-engine/types";
import type { SimulationStore } from "../../state/simulationStore";
import type { ViewStore } from "../../state/viewStore";
import type { SelectionStore } from "../../state/selectionStore";
import { creatorGroundAt } from "./creatorWorldVisuals";
import { useEditorHistory } from "./useEditorHistory";

export interface CreatorBuildingDraft { id: string; x: number; y: number; rotation?: EntityRotation }
export interface CreatorCityDraft { roads: Record<string, true>; buildings: CreatorBuildingDraft[] }

/** `size` deve vir da mesma fórmula que o jogo usa pro footprint real da cidade (`citySide`,
 * LIVE-POLISH) — um canvas maior que isso só criaria construções que nunca vão caber quando o
 * mundo for de fato criado. */
export function initialCreatorCityDraft(
  seed: number,
  settlementIndex: number,
  size: { width: number; height: number },
): CreatorCityDraft {
  const offset = (seed + settlementIndex) % 3;
  const maxX = Math.max(0, size.width - 2);
  const maxY = Math.max(0, size.height - 2);
  const clampX = (x: number) => Math.max(0, Math.min(Math.round(x), maxX));
  const clampY = (y: number) => Math.max(0, Math.min(Math.round(y), maxY));
  // Posições como frações do tamanho real da vila (não literais) — vilas pequenas (3x3) não
  // tinham espaço pros 3 prédios fixos em x=13/y=11 e eles ficavam fora dos limites.
  const spots = [
    { x: size.width * 0.25 + offset, y: size.height * 0.25 },
    { x: size.width * 0.75, y: size.height * 0.25 + offset },
    { x: size.width * 0.3, y: size.height * 0.75 },
  ];
  // Vilas minúsculas não têm espaço pra 3 construções sem sobrepor — nasce só com 1.
  const count = size.width < 6 || size.height < 6 ? 1 : spots.length;
  return {
    roads: {},
    buildings: spots.slice(0, count).map((spot, i) => (
      { id: `draft-${settlementIndex}-${i + 1}`, x: clampX(spot.x), y: clampY(spot.y), rotation: 0 }
    )),
  };
}

interface CreatorCityEditorProps {
  cityId: string;
  name: string;
  seed: number;
  /** Mesmo footprint (`citySide`) que o mundo criado vai ter de verdade — sem isso o editor
   * desenhava um canvas fixo 24x18 sempre maior que a cidade real (LIVE-POLISH). */
  citySize: { width: number; height: number };
  viewport: { width: number; height: number };
  draft: CreatorCityDraft;
  onDraftChange: (update: (draft: CreatorCityDraft) => CreatorCityDraft) => void;
  onBack: () => void;
  simulationStore: SimulationStore;
  viewStore: ViewStore;
  selectionStore: SelectionStore;
}

type CityTool = "select" | "road" | "building" | "erase";

export function CreatorCityEditor(props: CreatorCityEditorProps) {
  const { cityId, name, seed, citySize, viewport, draft: initialDraft, onDraftChange, onBack, simulationStore, viewStore, selectionStore } = props;
  const { value: draft, commit, undo, redo, canUndo, canRedo } = useEditorHistory(initialDraft);
  const onDraftChangeRef = useRef(onDraftChange);
  const [tool, setTool] = useState<CityTool>("select");
  const space: SpaceId = useMemo(() => ({ kind: "City", cityId }), [cityId]);
  const selection = useSyncExternalStore((listener) => selectionStore.subscribe(listener), () => selectionStore.current());

  useEffect(() => { onDraftChangeRef.current = onDraftChange; }, [onDraftChange]);
  useEffect(() => onDraftChangeRef.current(() => draft), [draft]);

  const buildings = useMemo<AuthoritativeEntity[]>(() => draft.buildings.map((building) => {
    const footprint = generateBuildingFootprint(building.id, 1);
    const width = Math.max(...footprint.map((cell) => cell.x)) + 1;
    const height = Math.max(...footprint.map((cell) => cell.y)) + 1;
    return {
      ref: { kind: "building", id: building.id, space },
      position: { x: building.x, y: building.y }, size: { w: width, h: height },
      sizeIsDerived: true, color: CATEGORY_COLOR.building,
      rotation: building.rotation ?? 0,
      footprintCells: footprint.map((cell) => ({
        x: cell.x, y: cell.y,
        color: cell.material === "door" ? MATERIAL_COLOR.door : roofColorFor(building.id),
        material: cell.material === "door" ? "door" as const : "roof" as const,
      })),
    };
  }), [draft.buildings, space]);

  function paintRoad(cell: { x: number; y: number }): boolean {
    if (tool !== "road") return false;
    if (cell.x < 0 || cell.y < 0 || cell.x >= citySize.width || cell.y >= citySize.height) return true;
    commit((current) => ({ ...current, roads: { ...current.roads, [`${cell.x},${cell.y}`]: true } }));
    return true;
  }

  function handleClick(cell: { x: number; y: number }): boolean {
    if (tool === "road") return paintRoad(cell);
    if (tool === "erase") {
      const target = buildings.find((building) => cell.x >= building.position.x && cell.y >= building.position.y && cell.x < building.position.x + building.size.w && cell.y < building.position.y + building.size.h);
      if (target) deleteBuilding(target.ref.id);
      return true;
    }
    if (tool !== "building") return false;
    const id = `draft-${cityId}-${draft.buildings.length + 1}`;
    commit((current) => ({ ...current, buildings: [...current.buildings, { id, x: cell.x, y: cell.y, rotation: 0 }] }));
    setTool("select");
    return true;
  }

  function moveBuilding(ref: EntityRef, cell: { x: number; y: number }): boolean {
    if (ref.kind !== "building") return false;
    commit((current) => ({ ...current, buildings: current.buildings.map((item) => item.id === ref.id ? { ...item, x: cell.x, y: cell.y } : item) }));
    return true;
  }

  const selectedBuilding = selection?.kind === "building" ? draft.buildings.find((item) => item.id === selection.id) : undefined;

  function deleteBuilding(id: string) {
    commit((current) => ({ ...current, buildings: current.buildings.filter((building) => building.id !== id) }));
    selectionStore.clear();
  }

  function rotateBuilding(id: string) {
    commit((current) => ({ ...current, buildings: current.buildings.map((building) => building.id === id
      ? { ...building, rotation: nextRotation(building.rotation ?? 0) }
      : building) }));
  }

  useEffect(() => {
    function handleShortcut(event: KeyboardEvent) {
      if (isEditableTarget(event.target)) return;
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "z") {
        event.preventDefault();
        event.shiftKey ? redo() : undo();
        selectionStore.clear();
      } else if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "y") {
        event.preventDefault();
        redo();
        selectionStore.clear();
      } else if ((event.key === "Delete" || event.key === "Backspace") && selectedBuilding) {
        event.preventDefault();
        deleteBuilding(selectedBuilding.id);
      } else if (event.key.toLowerCase() === "r" && selectedBuilding) {
        event.preventDefault();
        rotateBuilding(selectedBuilding.id);
      }
    }
    window.addEventListener("keydown", handleShortcut);
    return () => window.removeEventListener("keydown", handleShortcut);
  }, [redo, selectedBuilding, selectionStore, undo]);

  return (
    <div className="creator-city-editor" data-testid="creator-city-editor">
      <header className="creator-city-heading">
        <button type="button" aria-label="Voltar ao mapa-múndi" onClick={onBack}>← Voltar ao mapa-múndi</button>
        <div><span>Desenho do assentamento</span><h2>{name}</h2></div>
        <p>{draft.buildings.length} construções · rascunho local até a fase de integração.</p>
        <div className="editor-history-controls" aria-label="Histórico de edição da cidade">
          <button type="button" aria-label="Desfazer na cidade" title="Desfazer (Ctrl+Z)" disabled={!canUndo} onClick={undo}>↶</button>
          <button type="button" aria-label="Refazer na cidade" title="Refazer (Ctrl+Y)" disabled={!canRedo} onClick={redo}>↷</button>
        </div>
      </header>
      <MapView
        space={space} viewport={viewport}
        cells={{
          ...citySize, showGrid: true, backgroundColor: "#789eaa",
          colorAt: (x, y) => draft.roads[`${x},${y}`] ? "#9b8b6c" : creatorGroundAt(seed, x, y).color,
        }}
        layers={[]} lodThresholds={{ aggregate: 4, token: 10, detail: 18 }}
        simulationStore={simulationStore} viewStore={viewStore} selectionStore={selectionStore}
        staticEntities={buildings}
        onPaintClick={handleClick}
        onPaintDrag={tool === "road" ? paintRoad : undefined}
        onEntityMove={tool === "select" ? moveBuilding : undefined}
      />
      <div className="world-tool-dock creator-city-tools" aria-label="Ferramentas da cidade">
        {(["select", "road", "building", "erase"] as const).map((value) => (
          <button key={value} type="button" className={tool === value ? "active" : ""} aria-pressed={tool === value} onClick={() => setTool(value)}>
            <span aria-hidden="true">{value === "select" ? "◇" : value === "road" ? "═" : value === "building" ? "⌂" : "×"}</span>
            <small>{value === "select" ? "Mover" : value === "road" ? "Traçar rua" : value === "building" ? "Construção" : "Apagar"}</small>
          </button>
        ))}
      </div>
      {selectedBuilding && (
        <aside className="side-panel creator-building-inspector" data-testid="creator-building-inspector">
          <span className="world-config-kicker">Construção selecionada</span>
          <h3>Edifício {selectedBuilding.id.split("-").at(-1)}</h3>
          <p>Arraste no mapa para reposicionar.</p>
          <strong>{selectedBuilding.x}, {selectedBuilding.y}</strong>
          <div className="settlement-position" data-testid="building-rotation"><span>Orientação</span><strong>{selectedBuilding.rotation ?? 0}°</strong></div>
          <button type="button" className="rotate-entity-button" aria-label="Rotacionar construção" onClick={() => rotateBuilding(selectedBuilding.id)}>↻ Rotacionar 90° <kbd>R</kbd></button>
          <button type="button" className="delete-entity-button" aria-label="Apagar construção" onClick={() => deleteBuilding(selectedBuilding.id)}>Apagar construção</button>
        </aside>
      )}
    </div>
  );
}

function isEditableTarget(target: EventTarget | null): boolean {
  return target instanceof HTMLElement && (target.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName));
}

function nextRotation(rotation: EntityRotation): EntityRotation {
  return ((rotation + 90) % 360) as EntityRotation;
}
