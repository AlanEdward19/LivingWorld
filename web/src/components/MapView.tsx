// Fase 15.1, T13: casca React do map engine (design.md "Components" -> `MapView`; master
// prompt §30-32). Monta o canvas, liga wheel/drag/click/dblclick/Esc ao engine puro e roda o
// loop de `requestAnimationFrame` lendo os três stores diretamente — nenhum estado de tick vira
// `useState`: um delta do `SimulationStore` só atualiza refs e o próximo frame do canvas, nunca
// dispara um re-render do componente React (VTT2-32).
import { useEffect, useRef } from "react";
import { Camera, type Viewport } from "../map-engine/Camera";
import { InterpolationBuffer } from "../map-engine/interpolation";
import { hitTest } from "../map-engine/hitTest";
import { draw, type ActiveLayer, type CellSource } from "../map-engine/renderer";
import { npcVisualScale, pawnHitCoverageRadius, tokenRadiusPx } from "../map-engine/tokenSize";
import type { LodThresholds } from "../map-engine/lod";
import type { AuthoritativeEntity, CameraState, EntityRef, SpaceId } from "../map-engine/types";
import { toScopeKey } from "../map-engine/space";
import type { SimulationStore } from "../state/simulationStore";
import type { ViewStore } from "../state/viewStore";
import type { SelectionStore } from "../state/selectionStore";

/** T50 (bug "seguir NPC entre escopos"): mesmo `NpcScope` que o backend devolve em
 * `NpcInspection.currentScope` (kind 0 = World, 1 = City) — traduzido pro `SpaceId` do cliente. */
function spaceFromScope(scope: { kind: number; cityId: { value: string } | null }): SpaceId | null {
  if (scope.kind === 1 && scope.cityId) return { kind: "City", cityId: scope.cityId.value };
  if (scope.kind === 0) return { kind: "World" };
  return null;
}

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
  /**
   * Entidades que não vêm do `SimulationStore` (T14, gap real encontrado ao ligar cidades e
   * prédios ao mesmo pipeline de render/hit-test/seleção): cidades/prédios não têm delta de
   * tick — `SimulationStore.entitiesOf` só extrai NPC. A view (WorldMapView/CityView) computa
   * a posição delas (real ou aproximada) e entra aqui; MapView as trata como qualquer outra
   * entidade pro resto do pipeline (LOD, culling, hit-test, seleção, navegação).
   */
  staticEntities?: AuthoritativeEntity[];
  /**
   * Câmera de fit inicial para um espaço nunca visitado (T14, gap real: `Camera.initial`
   * assume grid começando em (0,0), o que serve WorldSpace mas não CitySpace — cidade usa
   * coordenadas absolutas de mundo centradas em `snapshot.location`, não em `cells.width/2`).
   * Se omitido, cai no `Camera.initial(cells.width, cells.height, viewport)` de sempre.
   */
  initialCamera?: CameraState;
  /**
   * Feedback do usuário (2026-08-07): trocar de andar dentro de um prédio não tinha efeito
   * visível na câmera — `viewStore.cameraFor` cacheia por `space` (buildingId+cityId), sem
   * andar (andar é estado só do componente, não do `SpaceId` — decisão documentada em
   * `InteriorView.tsx`), então a câmera gravada de um andar "vazava" pros outros. Quando este
   * valor muda de identidade entre renders, a câmera é forçada pro `initialCamera` (ou o fit
   * padrão) em vez do valor em cache — quem chama decide o que conta como "mudou de conteúdo".
   */
  resetCameraKey?: unknown;
  /**
   * T25: quando presente, todo clique passa por aqui ANTES do hit-test/seleção padrão, com a
   * célula de mundo sob o cursor. Retornar `true` consome o clique (ferramenta de pintura do
   * World Creator) — o `MapView` não roda hit-test nem toca `SelectionStore` nesse clique.
   * Retornar `false` (ex.: ferramenta "selecionar") deixa o clique cair no comportamento normal.
   */
  onPaintClick?: (cell: { x: number; y: number }) => boolean;
  /** Ferramenta contínua: chamada para cada célula cruzada enquanto o ponteiro está pressionado. */
  onPaintDrag?: (cell: { x: number; y: number }) => boolean;
  /** Autoria espacial: permite ao editor mover uma entidade existente por arraste. */
  onEntityMove?: (ref: EntityRef, cell: { x: number; y: number }) => boolean;
}

const DEFAULT_HIT_RADIUS_PX = 10;
const EMPTY_STATIC_ENTITIES: AuthoritativeEntity[] = [];

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
  staticEntities = EMPTY_STATIC_ENTITIES,
  initialCamera,
  resetCameraKey,
  onPaintClick,
  onPaintDrag,
  onEntityMove,
}: MapViewProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const cameraRef = useRef<Camera | null>(null);
  const interpolationRef = useRef(new InterpolationBuffer());
  const entitiesRef = useRef<AuthoritativeEntity[]>([]);
  const dragRef = useRef<{ x: number; y: number } | null>(null);
  const paintDragRef = useRef<{ x: number; y: number } | null>(null);
  const entityDragRef = useRef<{ ref: EntityRef; cell: { x: number; y: number } } | null>(null);
  const consumedPointerRef = useRef(false);
  // T50: evita 1 fetch de inspeção por frame (~60/s) enquanto o alvo seguido está fora do
  // espaço observado — só 1 em voo por vez, resetado quando o alvo volta a aparecer.
  const followResolvingRef = useRef(false);

  // Câmera do espaço: restaura a guardada no ViewStore, ou o fit inicial se nunca visitado.
  useEffect(() => {
    const fallback = initialCamera ?? Camera.initial(cells.width, cells.height, viewport);
    const initialState = resetCameraKey !== undefined ? fallback : viewStore.cameraFor(space, fallback);
    cameraRef.current = new Camera(initialState, viewport);
    if (resetCameraKey !== undefined) {
      viewStore.recordCamera(space, initialState);
    }
  }, [space, viewport.width, viewport.height, cells.width, cells.height, viewStore, initialCamera, resetCameraKey]);

  // Estado autoritativo: lê `entitiesOf` no mount e a cada notificação do SimulationStore —
  // nunca via `useState`, então uma notificação não re-renderiza este componente.
  useEffect(() => {
    function refreshEntities() {
      const latest = [...staticEntities, ...simulationStore.entitiesOf(space)];
      const now = performance.now();
      entitiesRef.current = latest;
      for (const entity of latest) {
        interpolationRef.current.observe(entity.ref.id, entity.position, now);
      }
      // T50 fix: enquanto o snapshot do NOVO espaço ainda não chegou (troca de escopo em
      // voo), `entitiesOf(space)` devolve vazio só por falta de dados — sincronizar a
      // seleção agora limparia (e derrubaria o follow) uma entidade que só ainda não foi
      // confirmada como ausente. Pula esta rodada; a próxima notificação (snapshot real
      // aplicado) chama `refreshEntities` de novo com a lista de verdade.
      if (simulationStore.isSpaceReady(space)) {
        selectionStore.syncWithSpace(space, latest.map((entity) => entity.ref));
      }
    }
    refreshEntities();
    return simulationStore.subscribe(refreshEntities);
  }, [space, simulationStore, selectionStore, staticEntities]);

  // Feedback do usuário (2026-08-21, 4ª rodada): o bug de clique voltava assim que o tempo
  // andava (acelerar tick, ou só deixar correr) e desaparecia parado em 1x sem tocar em nada.
  // Causa real: o loop de desenho (`frame`, abaixo) desenha a posição INTERPOLADA (visual,
  // suavizada) de cada NPC, mas os cliques comparavam contra `entitiesRef.current` — a posição
  // AUTORITATIVA crua, sem interpolação. Enquanto a interpolação está "em trânsito" entre a
  // posição antiga e a nova (qualquer tick recente), o pawn desenhado e o ponto que o hit-test
  // usa divergem — clicar onde o NPC está desenhado erra o alvo real. Parado (sem tick novo há
  // tempo), a interpolação já convergiu pro valor autoritativo e os dois batem, escondendo o bug.
  function visualEntitiesNow(): AuthoritativeEntity[] {
    const now = performance.now();
    return entitiesRef.current.map((entity) => ({
      ...entity,
      position: interpolationRef.current.visualPositionOf(entity.ref.id, now),
    }));
  }

  // T50: alvo seguido saiu do espaço observado (cruzou de cidade pro mundo ou vice-versa) — a
  // câmera parava de mover silenciosamente (comportamento de antes) porque não tinha pra onde
  // ir. Consulta a inspeção (mesma fonte que o inspector já usa) só pra ler o escopo atual real
  // do NPC, e troca de espaço via `viewStore.enter` — a mesma troca que a navegação manual já usa
  // (App.tsx reage a `currentSpace()` sozinho, nenhuma mudança necessária lá).
  async function resolveFollowedSpaceIfLost(followed: EntityRef) {
    if (followResolvingRef.current || followed.kind !== "npc") return;
    followResolvingRef.current = true;
    try {
      const inspection = await simulationStore.inspectNpc(Number(followed.id));
      if (viewStore.followedEntity()?.id !== followed.id) return; // usuário já seguiu outra coisa
      if (!inspection) {
        viewStore.stopFollow(); // morreu/não pôde ser inspecionado — não faz sentido continuar
        return;
      }
      const newSpace = spaceFromScope(inspection.currentScope);
      if (newSpace && toScopeKey(newSpace) !== toScopeKey(space)) {
        viewStore.enter(newSpace);
      }
    } finally {
      followResolvingRef.current = false;
    }
  }

  // Loop de desenho — lê os refs acima a cada frame, nunca espera por um re-render do React.
  useEffect(() => {
    let animationId: number;
    function frame() {
      const camera = cameraRef.current;
      if (camera) {
        // T19: com Follow ativo, a câmera acompanha a posição AUTORITATIVA (nunca a
        // interpolada) da entidade seguida, todo frame — sobrescreve qualquer pan manual até o
        // usuário arrastar de novo (o que cancela o follow em `handleMouseMove`).
        const followed = viewStore.followedEntity();
        if (followed) {
          const target = entitiesRef.current.find(
            (e) => e.ref.kind === followed.kind && e.ref.id === followed.id,
          );
          if (target) {
            camera.restore({ center: { ...target.position }, scale: camera.snapshot().scale });
            viewStore.recordCamera(space, camera.snapshot());
          } else {
            void resolveFollowedSpaceIfLost(followed);
          }
        }
        draw(canvasRef.current?.getContext("2d") ?? null, {
          camera: camera.snapshot(),
          cells,
          layers,
          entities: visualEntitiesNow(),
          events: simulationStore.livingStateOf(space).events,
          lodThresholds,
          highlightId: selectionStore.current()?.id,
        });
      }
      animationId = requestAnimationFrame(frame);
    }
    animationId = requestAnimationFrame(frame);
    return () => cancelAnimationFrame(animationId);
  }, [cells, layers, lodThresholds, selectionStore, viewStore, simulationStore, space]);

  useEffect(() => {
    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") {
        selectionStore.clear();
      }
    }
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [selectionStore]);

  // Escala clientX/Y para pixels de canvas: o box CSS do canvas pode divergir dos atributos
  // width/height (scrollbar, layout do container); sem ele o hit-test desalinha quando os dois
  // não coincidem 1:1.
  function screenPoint(e: { clientX: number; clientY: number }): { x: number; y: number } {
    const canvas = canvasRef.current!;
    const rect = canvas.getBoundingClientRect();
    return {
      x: ((e.clientX - rect.left) / rect.width) * canvas.width,
      y: ((e.clientY - rect.top) / rect.height) * canvas.height,
    };
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
    const camera = cameraRef.current;
    if (!camera) return;
    const point = screenPoint(e);
    const world = camera.screenToWorld(point);
    const cell = { x: Math.floor(world.x), y: Math.floor(world.y) };
    if (onPaintDrag?.(cell)) {
      consumedPointerRef.current = true;
      paintDragRef.current = cell;
      return;
    }
    if (onEntityMove) {
      const hit = hitTest(point, camera, visualEntitiesNow(), effectiveHitRadiusPx(camera));
      if (hit) {
        selectionStore.select(hit);
        entityDragRef.current = { ref: hit, cell };
        return;
      }
    }
    dragRef.current = { x: e.clientX, y: e.clientY };
  }

  function handleMouseMove(e: React.MouseEvent<HTMLCanvasElement>) {
    const camera = cameraRef.current;
    if (!camera) {
      return;
    }
    const world = camera.screenToWorld(screenPoint(e));
    const cell = { x: Math.floor(world.x), y: Math.floor(world.y) };
    if (paintDragRef.current && onPaintDrag) {
      for (const crossed of cellsOnLine(paintDragRef.current, cell).slice(1)) onPaintDrag(crossed);
      paintDragRef.current = cell;
      return;
    }
    if (entityDragRef.current && onEntityMove) {
      if (cell.x !== entityDragRef.current.cell.x || cell.y !== entityDragRef.current.cell.y) {
        onEntityMove(entityDragRef.current.ref, cell);
        entityDragRef.current = { ...entityDragRef.current, cell };
      }
      return;
    }
    const drag = dragRef.current;
    if (!drag) return;
    // T19: pan manual cancela o Follow — master prompt §19 ("mover a câmera pode
    // opcionalmente cancelar Follow"); sem isso o próximo frame do rAF puxaria a câmera de
    // volta pra entidade seguida, e o arrasto do usuário nunca teria efeito visível.
    if (viewStore.followedEntity()) {
      viewStore.stopFollow();
    }
    const dx = e.clientX - drag.x;
    const dy = e.clientY - drag.y;
    camera.panBy({ x: dx, y: dy });
    viewStore.recordCamera(space, camera.snapshot());
    dragRef.current = { x: e.clientX, y: e.clientY };
  }

  function handleMouseUp() {
    dragRef.current = null;
    paintDragRef.current = null;
    entityDragRef.current = null;
  }

  // Feedback do usuário (2026-08-07): clique em NPC só "pegava" bem quando zoomed-out. Causa
  // real: o raio de acerto precisa acompanhar o token. Reusa a MESMA fórmula do desenho do
  // token (`tokenRadiusPx`, 2026-08-21).
  //
  // Feedback do usuário (2026-08-21, 2ª rodada): um fator de folga fixo (1.3x) não bastava —
  // dentro de uma cidade/prédio o pawn desenha 1.65x/2.2x maior (`npcVisualScale`), então o
  // círculo de clique ficava bem menor que o token visível e o clique falhava quase sempre.
  //
  // Feedback do usuário (2026-08-21, 3ª rodada): igualar o raio ainda não bastava — o pawn é um
  // retângulo alto (não um círculo), então clicar na cabeça/torso visível (acima do centro)
  // continuava fora do raio pequeno, e às vezes "pegava" outro NPC próximo (menor distância
  // vencia). `pawnHitCoverageRadius` cobre o retângulo inteiro a partir do mesmo ponto de
  // ancoragem que `drawNpcPawn` desenha. Todo NPC observado por este `MapView` está no MESMO
  // espaço (`space.kind`), então o multiplicador exato (não uma folga aproximada) é conhecido aqui.
  function effectiveHitRadiusPx(camera: Camera): number {
    const baseRadius = tokenRadiusPx(camera.snapshot().scale) * npcVisualScale(space.kind);
    return Math.max(hitRadiusPx, pawnHitCoverageRadius(baseRadius));
  }

  function handleClick(e: React.MouseEvent<HTMLCanvasElement>) {
    const camera = cameraRef.current;
    if (!camera) {
      return;
    }
    if (consumedPointerRef.current) {
      consumedPointerRef.current = false;
      return;
    }
    if (onPaintClick) {
      const world = camera.screenToWorld(screenPoint(e));
      if (onPaintClick({ x: Math.floor(world.x), y: Math.floor(world.y) })) {
        return;
      }
    }
    const hit = hitTest(screenPoint(e), camera, visualEntitiesNow(), effectiveHitRadiusPx(camera));
    if (hit) {
      selectionStore.select(hit);
    } else {
      // feedback do usuário (2026-08-07): clicar em espaço vazio precisa desselecionar —
      // master prompt §14 ("clicar em espaço vazio pode remover seleção").
      selectionStore.clear();
    }
  }

  function handleDoubleClick(e: React.MouseEvent<HTMLCanvasElement>) {
    const camera = cameraRef.current;
    if (!camera) {
      return;
    }
    const hit = hitTest(screenPoint(e), camera, visualEntitiesNow(), effectiveHitRadiusPx(camera));
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
      onMouseLeave={handleMouseUp}
      onClick={handleClick}
      onDoubleClick={handleDoubleClick}
      style={{ cursor: "pointer" }}
    />
  );
}

function cellsOnLine(from: { x: number; y: number }, to: { x: number; y: number }): { x: number; y: number }[] {
  const cells: { x: number; y: number }[] = [];
  const dx = Math.abs(to.x - from.x);
  const dy = Math.abs(to.y - from.y);
  const sx = from.x < to.x ? 1 : -1;
  const sy = from.y < to.y ? 1 : -1;
  let x = from.x;
  let y = from.y;
  let error = dx - dy;
  while (true) {
    cells.push({ x, y });
    if (x === to.x && y === to.y) return cells;
    const twice = error * 2;
    if (twice > -dy) { error -= dy; x += sx; }
    if (twice < dx) { error += dx; y += sy; }
  }
}
