import { useEffect, useRef, useState } from "react";
import { Application, Container, Graphics, Sprite } from "pixi.js";
import type { BuildingFixture, FurnitureKind, WorldFixture } from "../fixture/types";
import { paletteForBuildingKind } from "../map/isoPalette";
import { patrolPositionAt } from "../map/patrolMath";
import { buildingFootprint, generateRoads, tileNoise, type Footprint } from "./settlementLayout";
import { focusOn, initialCamera, panBy, unfocus, zoomBy, type CameraState } from "./cameraState";
import { getNpcTexture } from "./npcTexture";
import { TILE } from "./constants";

export interface SettlementStageProps {
  fixture: WorldFixture;
  settlementId: string;
  /** Prédio focado (rota `{kind:"building"}`) — a câmera aproxima e o telhado dele revela o
   * interior NA MESMA cena, em vez de trocar pra uma view separada (AD-020). `null`/omitido =
   * vista de rua do settlement inteiro. */
  focusBuildingId?: string | null;
  onSelectAgent: (agentId: string) => void;
  /** `null` = sair do foco (volta pra vista de rua). */
  onFocusBuilding: (buildingId: string | null) => void;
}

const GROUND_BASE = 0x3a4a2c;
const GROUND_VARIANCE = 14; // +/- por canal RGB, não um int somado direto no hex (isso estourava canal)
const ROOF_ALPHA_FOCUSED = 0.14;
const FADE_SPEED = 0.12; // por frame a 60fps, ver ticker
const CLICK_DRAG_THRESHOLD = 6; // px de movimento do pointer antes de virar "arrastando"
const OUTDOOR_SPRITE_SCALE = (TILE * 0.6) / 100; // textura do NpcToken é 100x120

const FURNITURE_COLORS: Record<FurnitureKind, number> = {
  bed: 0x7a5c4a,
  table: 0x8a6f4e,
  chair: 0x6b5540,
  stove: 0x4a4a4a,
  oven: 0x3d3d3d,
  counter: 0x9c8b6d,
  shelf: 0x6e5c42,
  workbench: 0x5c4a36,
  desk: 0x7d6650,
};

function hexOf(cssHex: string): number {
  return Number.parseInt(cssHex.replace("#", "0x"), 16);
}

/** Varia um hex RGB por CANAL (clamped 0-255), não por soma direta no inteiro — somar direto
 * (bug corrigido) estourava de um canal pro outro e virava ruído de cor aleatório em vez de uma
 * variação sutil de terreno. */
function jitterColor(base: number, noise: number, maxDelta: number): number {
  const delta = Math.round((noise - 0.5) * 2 * maxDelta);
  const clamp = (channel: number) => Math.min(255, Math.max(0, channel + delta));
  const r = clamp((base >> 16) & 0xff);
  const g = clamp((base >> 8) & 0xff);
  const b = clamp(base & 0xff);
  return (r << 16) | (g << 8) | b;
}

function lerp(a: number, b: number, t: number): number {
  return a + (b - a) * t;
}

interface BuildingNode {
  root: Container;
  roof: Graphics;
  interior: Container;
  footprint: Footprint;
  worldX: number;
  worldY: number;
}

interface InteriorTransform {
  scale: number;
  offsetX: number;
  offsetY: number;
}

/**
 * Settlement View — renderer Canvas/WebGL dedicado (Pixi.js, AD-020), não mais SVG declarativo.
 * Terreno + roads são gerados por `settlementLayout.ts` (procedural/determinístico, camada de
 * apresentação — NÃO dado canônico). Prédios têm footprint real (área, não 1 tile) e revelam o
 * interior fisicamente (roof cutaway) ao focar, em vez de navegar pra outra tela.
 */
export function SettlementStage({ fixture, settlementId, focusBuildingId = null, onSelectAgent, onFocusBuilding }: SettlementStageProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const cameraRef = useRef<CameraState>(initialCamera(0, 0));
  const focusBuildingIdRef = useRef<string | null>(focusBuildingId);
  const dragRef = useRef<{ startX: number; startY: number; startCamera: CameraState; moved: number } | null>(null);
  const suppressClickRef = useRef(false);
  const buildingNodesRef = useRef(new Map<string, BuildingNode>());
  const interiorTransformsRef = useRef(new Map<string, InteriorTransform>());
  const agentSpritesRef = useRef(new Map<string, Sprite>());
  const agentLayerRef = useRef<Container | null>(null);
  const settlementCenterRef = useRef({ x: 0, y: 0 });
  const [activeFloorIndex, setActiveFloorIndex] = useState(0);

  /** Move a câmera pro prédio focado (ou de volta pro overview do settlement) — "aproximar a
   * câmera" em vez de só trocar o alpha do telhado (esse fica no ticker). Refs apenas, chamável
   * de qualquer efeito deste componente. */
  function applyCameraFocus(buildingId: string | null) {
    if (buildingId) {
      const node = buildingNodesRef.current.get(buildingId);
      if (node) {
        cameraRef.current = focusOn(
          cameraRef.current,
          buildingId,
          node.worldX + (node.footprint.width * TILE) / 2,
          node.worldY + (node.footprint.height * TILE) / 2,
        );
      }
    } else {
      cameraRef.current = unfocus(cameraRef.current, settlementCenterRef.current.x, settlementCenterRef.current.y);
    }
  }

  const settlement = fixture.settlements.find((s) => s.id === settlementId);
  const agents = settlement ? fixture.agents.filter((a) => a.settlementId === settlementId) : [];
  const focusedBuilding = settlement?.buildings.find((b) => b.id === focusBuildingId) ?? null;

  useEffect(() => {
    setActiveFloorIndex(0);
  }, [focusBuildingId]);

  useEffect(() => {
    focusBuildingIdRef.current = focusBuildingId;
    applyCameraFocus(focusBuildingId);
  }, [focusBuildingId]);

  function rebuildInterior(building: BuildingFixture, floorIndex: number, node: BuildingNode) {
    node.interior.removeChildren();
    const floor = building.floors[floorIndex];
    if (!floor) {
      interiorTransformsRef.current.delete(building.id);
      return;
    }
    const maxExtentX = Math.max(...floor.rooms.map((r) => r.bounds.x + r.bounds.width), 1);
    const maxExtentY = Math.max(...floor.rooms.map((r) => r.bounds.y + r.bounds.height), 1);
    const footprintPxW = node.footprint.width * TILE;
    const footprintPxH = node.footprint.height * TILE;
    const scale = Math.min((footprintPxW * 0.92) / maxExtentX, (footprintPxH * 0.92) / maxExtentY);
    const offsetX = (footprintPxW - maxExtentX * scale) / 2;
    const offsetY = (footprintPxH - maxExtentY * scale) / 2;
    interiorTransformsRef.current.set(building.id, { scale, offsetX, offsetY });

    for (const room of floor.rooms) {
      const rx = offsetX + room.bounds.x * scale;
      const ry = offsetY + room.bounds.y * scale;
      const rw = room.bounds.width * scale;
      const rh = room.bounds.height * scale;
      node.interior.addChild(new Graphics().rect(rx, ry, rw, rh).fill(0xcfc6ac).stroke({ width: 2, color: 0x2a2620 }));

      for (const item of room.furniture) {
        const fx = offsetX + item.gridPosition.x * scale;
        const fy = offsetY + item.gridPosition.y * scale;
        const size = Math.max(4, scale * 0.55);
        node.interior.addChild(new Graphics().rect(fx, fy, size, size).fill(FURNITURE_COLORS[item.kind]));
      }
    }
  }

  // (Re)monta a Application do Pixi quando o settlement muda — 3 settlements nesta demo,
  // custo de recriar tudo é desprezível (ponytail: não vale a pena um diff incremental aqui).
  useEffect(() => {
    const containerEl = containerRef.current;
    const settlementDef = settlement;
    if (!settlementDef || !containerEl) return undefined;

    let destroyed = false;
    const app = new Application();
    const worldRoot = new Container();
    const terrainLayer = new Graphics();
    const roadLayer = new Graphics();
    const buildingLayer = new Container();
    const agentLayer = new Container();
    agentLayerRef.current = agentLayer;

    const xs = settlementDef.buildings.map((b) => b.gridPosition.x);
    const ys = settlementDef.buildings.map((b) => b.gridPosition.y);
    const minX = Math.min(0, ...xs) - 3;
    const maxX = Math.max(4, ...xs) + 3;
    const minY = Math.min(0, ...ys) - 3;
    const maxY = Math.max(4, ...ys) + 3;
    const centerX = ((minX + maxX) / 2) * TILE;
    const centerY = ((minY + maxY) / 2) * TILE;
    cameraRef.current = initialCamera(centerX, centerY);
    settlementCenterRef.current = { x: centerX, y: centerY };

    // Parâmetros explícitos (não closure sobre `settlementDef`/`containerEl` do escopo externo)
    // porque o TS reseta narrowing de `const` ao cruzar uma fronteira de função aninhada —
    // isso dá aos dois um tipo não-nulo garantido dentro de `setup` sem precisar de `!`.
    async function setup(settlementDef: NonNullable<typeof settlement>, containerEl: HTMLDivElement) {
      await app.init({ resizeTo: containerEl, backgroundColor: GROUND_BASE, antialias: true });
      if (destroyed) {
        app.destroy(true, { children: true });
        return;
      }
      containerEl.appendChild(app.canvas);

      app.stage.addChild(worldRoot);
      worldRoot.addChild(terrainLayer, roadLayer, buildingLayer, agentLayer);

      for (let gx = minX; gx <= maxX; gx += 1) {
        for (let gy = minY; gy <= maxY; gy += 1) {
          const noise = tileNoise(gx, gy, settlementDef.id);
          terrainLayer.rect(gx * TILE, gy * TILE, TILE, TILE).fill(jitterColor(GROUND_BASE, noise, GROUND_VARIANCE));
        }
      }
      terrainLayer.eventMode = "static";
      terrainLayer.on("pointertap", () => {
        if (!suppressClickRef.current && focusBuildingIdRef.current) onFocusBuilding(null);
      });

      for (const road of generateRoads(settlementDef.buildings)) {
        roadLayer
          .moveTo(road.from.x * TILE, road.from.y * TILE)
          .lineTo(road.to.x * TILE, road.to.y * TILE)
          .stroke({ width: 10, color: 0x6b5a44, alpha: 0.75 });
      }

      for (const building of settlementDef.buildings) {
        const footprint = buildingFootprint(building);
        const palette = paletteForBuildingKind(building.kind);
        const worldX = building.gridPosition.x * TILE - (footprint.width * TILE) / 2;
        const worldY = building.gridPosition.y * TILE - (footprint.height * TILE) / 2;

        const root = new Container();
        root.position.set(worldX, worldY);

        const roof = new Graphics()
          .rect(0, 0, footprint.width * TILE, footprint.height * TILE)
          .fill(hexOf(palette.top))
          .stroke({ width: 3, color: hexOf(palette.right) });

        const interior = new Container();
        interior.alpha = 0;

        root.addChild(roof, interior);
        buildingLayer.addChild(root);

        const node: BuildingNode = { root, roof, interior, footprint, worldX, worldY };
        buildingNodesRef.current.set(building.id, node);

        if (building.floors.length > 0) {
          roof.eventMode = "static";
          roof.cursor = "pointer";
          roof.on("pointertap", (event) => {
            event.stopPropagation();
            if (suppressClickRef.current) return;
            onFocusBuilding(building.id);
          });
          // Pré-constrói o floor 0 já no mount (não só quando o efeito de foco roda depois) —
          // esse efeito depende de `buildingNodesRef` já populado, que só acontece aqui dentro
          // do `setup()` assíncrono; sem isso, um deep-link direto pra `/building/:id` (settlement
          // e prédio focados já no PRIMEIRO render) não desenharia o interior a tempo.
          rebuildInterior(building, 0, node);
        }
      }

      // Idem pro foco da câmera — se a rota já chegou com um prédio focado (deep-link direto
      // pra `/building/:id`), a câmera já nasce aproximada nele, não só o telhado transparente.
      applyCameraFocus(focusBuildingId);

      const agentTextures = await Promise.all(agents.map((agent) => getNpcTexture(agent.id)));
      if (destroyed) {
        app.destroy(true, { children: true });
        return;
      }

      agents.forEach((agent, index) => {
        const sprite = new Sprite(agentTextures[index]);
        sprite.anchor.set(0.5, 1);
        sprite.scale.set(OUTDOOR_SPRITE_SCALE);
        sprite.eventMode = "static";
        sprite.cursor = "pointer";
        sprite.on("pointertap", (event) => {
          event.stopPropagation();
          if (suppressClickRef.current) return;
          onSelectAgent(agent.id);
        });
        agentLayer.addChild(sprite);
        agentSpritesRef.current.set(agent.id, sprite);
      });

      app.ticker.add(() => {
        const now = Date.now();
        const focusedId = focusBuildingIdRef.current;

        for (const [buildingId, node] of buildingNodesRef.current) {
          const isFocused = buildingId === focusedId;
          node.roof.alpha = lerp(node.roof.alpha, isFocused ? ROOF_ALPHA_FOCUSED : 1, FADE_SPEED);
          node.interior.alpha = lerp(node.interior.alpha, isFocused ? 1 : 0, FADE_SPEED);
        }

        for (const agent of agents) {
          const sprite = agentSpritesRef.current.get(agent.id);
          if (!sprite) continue;
          const indoor = agent.indoorLocation;
          const showIndoors = Boolean(indoor && indoor.buildingId === focusedId);

          if (showIndoors && indoor) {
            const node = buildingNodesRef.current.get(indoor.buildingId);
            const transform = interiorTransformsRef.current.get(indoor.buildingId);
            if (node && transform && sprite.parent !== node.interior) node.interior.addChild(sprite);
            if (transform) {
              sprite.position.set(transform.offsetX + indoor.position.x * transform.scale, transform.offsetY + indoor.position.y * transform.scale);
              sprite.scale.set((transform.scale * 0.9) / 100);
            }
          } else {
            if (sprite.parent !== agentLayer) agentLayer.addChild(sprite);
            const pos = patrolPositionAt(agent.patrolPoints, now);
            sprite.position.set(pos.x * TILE, pos.y * TILE);
            sprite.scale.set(OUTDOOR_SPRITE_SCALE);
          }
        }

        const screen = app.screen;
        worldRoot.scale.set(cameraRef.current.zoom);
        worldRoot.position.set(screen.width / 2 - cameraRef.current.x * cameraRef.current.zoom, screen.height / 2 - cameraRef.current.y * cameraRef.current.zoom);
      });
    }

    function onWheel(event: WheelEvent) {
      event.preventDefault();
      cameraRef.current = zoomBy(cameraRef.current, event.deltaY < 0 ? 1.12 : 1 / 1.12);
    }
    function onPointerDown(event: PointerEvent) {
      dragRef.current = { startX: event.clientX, startY: event.clientY, startCamera: cameraRef.current, moved: 0 };
      containerRef.current?.setPointerCapture(event.pointerId);
    }
    function onPointerMove(event: PointerEvent) {
      const drag = dragRef.current;
      if (!drag) return;
      const dx = event.clientX - drag.startX;
      const dy = event.clientY - drag.startY;
      drag.moved = Math.max(drag.moved, Math.hypot(dx, dy));
      suppressClickRef.current = drag.moved > CLICK_DRAG_THRESHOLD;
      cameraRef.current = panBy(drag.startCamera, dx / drag.startCamera.zoom, dy / drag.startCamera.zoom);
    }
    function onPointerUp() {
      dragRef.current = null;
      suppressClickRef.current = false;
    }

    containerEl.addEventListener("wheel", onWheel, { passive: false });
    containerEl.addEventListener("pointerdown", onPointerDown);
    containerEl.addEventListener("pointermove", onPointerMove);
    containerEl.addEventListener("pointerup", onPointerUp);
    containerEl.addEventListener("pointerleave", onPointerUp);

    void setup(settlementDef, containerEl);

    return () => {
      destroyed = true;
      containerEl.removeEventListener("wheel", onWheel);
      containerEl.removeEventListener("pointerdown", onPointerDown);
      containerEl.removeEventListener("pointermove", onPointerMove);
      containerEl.removeEventListener("pointerup", onPointerUp);
      containerEl.removeEventListener("pointerleave", onPointerUp);
      buildingNodesRef.current.clear();
      interiorTransformsRef.current.clear();
      agentSpritesRef.current.clear();
      agentLayerRef.current = null;
      try {
        app.destroy(true, { children: true });
      } catch {
        // já destruída (ex.: StrictMode dupla-montagem em dev) — sem problema
      }
      containerEl.replaceChildren();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [settlementId]);

  // Reconstrói só o interior (rooms/furniture) quando o foco ou o andar ativo muda — não a cada
  // frame do ticker acima (que só faz fade de alpha + posição de agent).
  useEffect(() => {
    if (!focusedBuilding) return;
    const node = buildingNodesRef.current.get(focusedBuilding.id);
    if (node) rebuildInterior(focusedBuilding, activeFloorIndex, node);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [focusBuildingId, activeFloorIndex, settlementId]);

  if (!settlement) return null;

  return (
    <div data-testid="settlement-stage" ref={containerRef}>
      {focusedBuilding && (
        <div data-testid="settlement-stage-overlay">
          <button type="button" data-testid="street-view-button" onClick={() => onFocusBuilding(null)}>
            ← Street
          </button>
          <span data-testid="focused-building-name">{focusedBuilding.name}</span>
          {focusedBuilding.floors.length > 1 && (
            <div data-testid="floor-selector">
              {focusedBuilding.floors.map((floor, index) => (
                <button key={floor.id} type="button" aria-pressed={index === activeFloorIndex} onClick={() => setActiveFloorIndex(index)}>
                  {floor.label}
                </button>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
