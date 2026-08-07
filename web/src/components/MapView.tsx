// Fase 15.1, T13: casca React do map engine (design.md "Components" -> `MapView`; master
// prompt §30-32). Monta o canvas, liga wheel/drag/click/dblclick/Esc ao engine puro e roda o
// loop de `requestAnimationFrame` lendo os três stores diretamente — nenhum estado de tick vira
// `useState`: um delta do `SimulationStore` só atualiza refs e o próximo frame do canvas, nunca
// dispara um re-render do componente React (VTT2-32).
//
// SPEC_DEVIATION: a task original previa remover `GridCanvas.tsx` neste commit. Ele continua em
// uso por `MapGridEditor.tsx` (editor do World Creator, migrado só em T25) — removê-lo agora
// quebraria a build. Segue o mesmo padrão já registrado em T8 ("extração e depois remoção"):
// `GridCanvas.tsx` só é apagado quando o último consumidor migrar (T14 tira WorldMapView/
// CityView, T17 tira MapOverlay, T25 tira MapGridEditor).
import { useEffect, useRef } from "react";
import { Camera, type Viewport } from "../map-engine/Camera";
import { InterpolationBuffer } from "../map-engine/interpolation";
import { hitTest } from "../map-engine/hitTest";
import { draw, type ActiveLayer, type CellSource } from "../map-engine/renderer";
import type { LodThresholds } from "../map-engine/lod";
import type { AuthoritativeEntity, EntityRef, SpaceId } from "../map-engine/types";
import type { SimulationStore } from "../state/simulationStore";
import type { ViewStore } from "../state/viewStore";
import type { SelectionStore } from "../state/selectionStore";

export interface MapViewProps {
  space: SpaceId;
  viewport: Viewport;
  cells: CellSource;
  layers: ActiveLayer[];
  lodThresholds: LodThresholds;
  simulationStore: SimulationStore;
  viewStore: ViewStore;
  selectionStore: SelectionStore;
  /** Resolve o espaço de destino de um double-click numa entidade, ou `null` se não navegável. */
  resolveNavigationTarget?: (ref: EntityRef) => SpaceId | null;
  hitRadiusPx?: number;
}

const DEFAULT_HIT_RADIUS_PX = 10;

export function MapView({
  space,
  viewport,
  cells,
  layers,
  lodThresholds,
  simulationStore,
  viewStore,
  selectionStore,
  resolveNavigationTarget,
  hitRadiusPx = DEFAULT_HIT_RADIUS_PX,
}: MapViewProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const cameraRef = useRef<Camera | null>(null);
  const interpolationRef = useRef(new InterpolationBuffer());
  const entitiesRef = useRef<AuthoritativeEntity[]>([]);
  const dragRef = useRef<{ x: number; y: number } | null>(null);

  // Câmera do espaço: restaura a guardada no ViewStore, ou o fit inicial se nunca visitado.
  useEffect(() => {
    const fallback = Camera.initial(cells.width, cells.height, viewport);
    const initialState = viewStore.cameraFor(space, fallback);
    cameraRef.current = new Camera(initialState, viewport);
  }, [space, viewport.width, viewport.height, cells.width, cells.height, viewStore]);

  // Estado autoritativo: lê `entitiesOf` no mount e a cada notificação do SimulationStore —
  // nunca via `useState`, então uma notificação não re-renderiza este componente.
  useEffect(() => {
    function refreshEntities() {
      const latest = simulationStore.entitiesOf(space);
      const now = performance.now();
      entitiesRef.current = latest;
      for (const entity of latest) {
        interpolationRef.current.observe(entity.ref.id, entity.position, now);
      }
      selectionStore.syncWithSpace(space, latest.map((entity) => entity.ref));
    }
    refreshEntities();
    return simulationStore.subscribe(refreshEntities);
  }, [space, simulationStore, selectionStore]);

  // Loop de desenho — lê os refs acima a cada frame, nunca espera por um re-render do React.
  useEffect(() => {
    let animationId: number;
    function frame() {
      const camera = cameraRef.current;
      if (camera) {
        const now = performance.now();
        const visualEntities = entitiesRef.current.map((entity) => ({
          ...entity,
          position: interpolationRef.current.visualPositionOf(entity.ref.id, now),
        }));
        draw(canvasRef.current?.getContext("2d") ?? null, {
          camera: camera.snapshot(),
          cells,
          layers,
          entities: visualEntities,
          lodThresholds,
          highlightId: selectionStore.current()?.id,
        });
      }
      animationId = requestAnimationFrame(frame);
    }
    animationId = requestAnimationFrame(frame);
    return () => cancelAnimationFrame(animationId);
  }, [cells, layers, lodThresholds, selectionStore]);

  useEffect(() => {
    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") {
        selectionStore.clear();
      }
    }
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [selectionStore]);

  function screenPoint(e: { clientX: number; clientY: number }): { x: number; y: number } {
    const rect = canvasRef.current!.getBoundingClientRect();
    return { x: e.clientX - rect.left, y: e.clientY - rect.top };
  }

  function handleWheel(e: React.WheelEvent<HTMLCanvasElement>) {
    const camera = cameraRef.current;
    if (!camera) {
      return;
    }
    const factor = e.deltaY < 0 ? 1.1 : 1 / 1.1;
    camera.zoomAt(screenPoint(e), factor);
    viewStore.recordCamera(space, camera.snapshot());
  }

  function handleMouseDown(e: React.MouseEvent<HTMLCanvasElement>) {
    dragRef.current = { x: e.clientX, y: e.clientY };
  }

  function handleMouseMove(e: React.MouseEvent<HTMLCanvasElement>) {
    const drag = dragRef.current;
    const camera = cameraRef.current;
    if (!drag || !camera) {
      return;
    }
    const dx = e.clientX - drag.x;
    const dy = e.clientY - drag.y;
    camera.panBy({ x: dx, y: dy });
    viewStore.recordCamera(space, camera.snapshot());
    dragRef.current = { x: e.clientX, y: e.clientY };
  }

  function handleMouseUp() {
    dragRef.current = null;
  }

  function handleClick(e: React.MouseEvent<HTMLCanvasElement>) {
    const camera = cameraRef.current;
    if (!camera) {
      return;
    }
    const hit = hitTest(screenPoint(e), camera, entitiesRef.current, hitRadiusPx);
    if (hit) {
      selectionStore.select(hit);
    }
  }

  function handleDoubleClick(e: React.MouseEvent<HTMLCanvasElement>) {
    const camera = cameraRef.current;
    if (!camera) {
      return;
    }
    const hit = hitTest(screenPoint(e), camera, entitiesRef.current, hitRadiusPx);
    const target = hit && resolveNavigationTarget?.(hit);
    if (target) {
      viewStore.enter(target);
    }
  }

  return (
    <canvas
      ref={canvasRef}
      data-testid="map-view-canvas"
      width={viewport.width}
      height={viewport.height}
      onWheel={handleWheel}
      onMouseDown={handleMouseDown}
      onMouseMove={handleMouseMove}
      onMouseUp={handleMouseUp}
      onClick={handleClick}
      onDoubleClick={handleDoubleClick}
      style={{ cursor: "pointer" }}
    />
  );
}
