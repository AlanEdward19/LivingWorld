import { useEffect, useRef, useState } from "react";
import { Application, Container, Graphics, Sprite, type FederatedPointerEvent } from "pixi.js";
import type { WorldFixture } from "../fixture/types";
import { appearanceForNpc } from "../npc/appearance";
import { SETTLEMENT_PALETTE } from "../map/isoPalette";
import { tileNoise } from "./settlementLayout";
import { generateWorldRoads, settlementFootprintExtent } from "./worldLayout";
import { agentWorldPosition, LOCAL_UNITS_PER_WORLD_TILE, settlementWorldOrigin } from "../map/worldPosition";
import { fitZoom, initialCamera, panBy, zoomBy, type CameraState } from "./cameraState";
import { followStore } from "../state/followStore";
import { getNpcTexture } from "./npcTexture";
import { MapHoverCard, type MapHoverTarget } from "../components/MapHoverCard";

export interface WorldStageProps {
  fixture: WorldFixture;
  onSelectSettlement: (settlementId: string) => void;
  onSelectNpc: (agentId: string) => void;
  /** Clicou no terreno vazio — mesma paridade do `SettlementStage` (doc §42-43: clicar fora
   * mostra o "container" atual no Inspector). No mapa mundi o "container" é o mundo em si.
   * Sempre chamável, mesmo sem nada selecionado (no-op nesse caso). */
  onBackgroundClick: () => void;
}

/** Pixels por unidade de grid do mapa mundi — maior que `render/constants.ts`'s TILE (56, escala
 * local de settlement) porque cada unidade de mundo representa MUITO mais distância física. */
const WORLD_TILE_PX = 160;
/** Granularidade do terreno — não 1 tile por unidade de mundo inteira (ficaria em blocos grandes
 * demais, doc §12 "evitar grandes blocos de textura repetida"); nem tão fino quanto o `TILE`
 * local (a distância não pede esse detalhe). */
const WORLD_SUBTILE_PX = 64;
const WORLD_PADDING_UNITS = 3;

const GROUND_BASE = 0x3d4f2e; // levemente mais claro/quente que o verde do Settlement (0x3a4a2c) —
// mesma família de cor (doc §27 "mesmo renderer"), mas distinguível como escala diferente.
const GROUND_VARIANCE = 16;
const FOREST_THRESHOLD = 0.82; // fração de subtiles que viram "árvore" — bolsões, não grade cheia
const FOREST_COLOR = 0x2c4a2e;
const ROAD_COLOR = 0x6b5a44;
const RIVER_COLOR = 0x3f6f8a;

const CLICK_DRAG_THRESHOLD = 14;
const AGENT_DOT_RADIUS = 4;
const FOLLOW_RING_COLOR = 0xd5a85a;
/** Zoom a partir do qual o ponto vira o sprite de verdade do NPC (redesign doc §32/§50: "zoom
 * próximo → sprite"; pedido do usuário 2026-08-27: "se eu me aproximo bastante, deve aparecer o
 * sprite deles"). Textura 100x120 igual ao `SettlementStage`, escalada pra unidade de mundo. */
const SPRITE_REVEAL_ZOOM = 2.2;
// Bug real reportado pelo usuário (screenshot): NPCs gigantes, do tamanho do footprint do
// settlement inteiro. Causa: escala calculada em cima de WORLD_TILE_PX (160 — uma unidade de
// MUNDO, ~20 tiles locais de distância, ver LOCAL_UNITS_PER_WORLD_TILE), não do tamanho real que
// uma pessoa deveria ocupar na tela. O alvo certo é o tamanho em TELA no zoom em que o sprite
// aparece (`SPRITE_REVEAL_ZOOM`), não uma fração do tile de mundo — textura 100x120, escala
// pensada pra dar ~30px de largura na tela quando o sprite acabou de aparecer (30 / 2.2 / 100).
const WORLD_AGENT_SPRITE_SCALE = 0.14;

// Anel de "seguindo" — bug real reportado pelo usuário DUAS vezes (screenshots): primeiro um
// círculo do tamanho do corpo inteiro, depois uma elipse ainda mais larga que o próprio sprite e
// flutuando longe dos pés (gap visível). Calculado como FRAÇÃO do tamanho real do sprite (não
// mais números soltos) — assim não pode voltar a ficar desproporcional se a escala do sprite
// mudar de novo. Mesma ideia do `SettlementStage`, só que ali os números foram calibrados à mão
// pra aquela escala local; aqui é derivado pra nunca destoar da escala do sprite de novo.
// Bug real reportado pelo usuário (3ª rodada de screenshots): ainda "grosso e grande" mesmo
// depois de derivar da escala do sprite — as frações em si estavam grandes demais (0.4 de raio =
// quase a largura inteira do sprite em diâmetro) e a stroke (2px) era mais grossa que a própria
// elipse (raio_y de ~1.3px). Encolhido bem mais: diâmetro agora é uma FRAÇÃO PEQUENA da largura
// do sprite (não perto dela), e a stroke fina o bastante pra não dominar a forma.
const WORLD_AGENT_SPRITE_WIDTH = 100 * WORLD_AGENT_SPRITE_SCALE;
const WORLD_AGENT_SPRITE_HEIGHT = 120 * WORLD_AGENT_SPRITE_SCALE;
const FOLLOW_RING_RADIUS_X = WORLD_AGENT_SPRITE_WIDTH * 0.25;
const FOLLOW_RING_RADIUS_Y = WORLD_AGENT_SPRITE_HEIGHT * 0.05;
const FOLLOW_RING_OFFSET_Y = WORLD_AGENT_SPRITE_HEIGHT * 0.01;
const FOLLOW_RING_STROKE = 1;

/** Continuidade World → Settlement (redesign doc §25/§44-46, pedido do usuário 2026-08-26:
 * "mesma ideia de animação" da entrada num prédio) — a câmera do MAPA MUNDI dá zoom no settlement
 * clicado ANTES de trocar de rota, em vez de cortar direto pra outra tela. Dois renderers
 * continuam separados (WorldStage/SettlementStage) — decisão explícita do usuário, unificar os
 * dois num renderer/câmera só é trabalho maior que fica pra outra rodada — mas a animação de
 * ponte evita a sensação de "carreguei outro mapa" que o doc pede pra eliminar.
 *
 * NÃO se aplica a clicar num agent (bug real reportado pelo usuário 2026-08-27: clicar um NPC
 * estava "abrindo a cidade" dele em vez de só selecioná-lo) — clicar um agent é sempre seleção
 * instantânea (doc §42-43: "click seleciona a entidade... o mapa não muda de tela imediatamente"),
 * igual já funciona dentro de um settlement. `CenterStage.useSpatialScope` é quem garante que o
 * mapa mundi continua visível depois.
 */
const TRANSITION_ZOOM = 3.2;
const TRANSITION_LERP = 0.12;
const FOLLOW_CAMERA_LERP = 0.08; // por frame a 60fps — câmera "viaja" até o agent seguido
const TRANSITION_MIN_MS = 420;

interface TransitionState {
  targetX: number;
  targetY: number;
  onComplete: () => void;
  startedAt: number;
}

function lerp(a: number, b: number, t: number): number {
  return a + (b - a) * t;
}

function jitterColor(base: number, noise: number, maxDelta: number): number {
  const delta = Math.round((noise - 0.5) * 2 * maxDelta);
  const clamp = (channel: number) => Math.min(255, Math.max(0, channel + delta));
  const r = clamp((base >> 16) & 0xff);
  const g = clamp((base >> 8) & 0xff);
  const b = clamp(base & 0xff);
  return (r << 16) | (g << 8) | b;
}

function hexOf(cssHex: string): number {
  return Number.parseInt(cssHex.replace("#", "0x"), 16);
}

/** Tamanho mínimo em tela (px de mundo) pro footprint não desaparecer visualmente num settlement
 * minúsculo/sem prédios — só um piso, não afeta settlements com geometria real de verdade. */
const MIN_FOOTPRINT_PX = WORLD_TILE_PX * 0.18;

/**
 * World Map — renderer Canvas/WebGL dedicado (Pixi.js), mesma linguagem do `SettlementStage`
 * (terreno procedural, roads, câmera pan/zoom/drag, agents como entidades espaciais) na escala
 * do mundo inteiro (redesign doc "World Map Visual & Spatial Design"). Settlements viram
 * footprints reais (não círculos-pin) ligados por uma rede viária real (`worldLayout.ts`),
 * agents são pontos com a cor estável do seu fenótipo em zoom distante e o sprite de verdade em
 * zoom próximo (doc §29-32), se movendo pelo trajeto de patrulha convertido pra coordenada
 * absoluta de mundo (`map/worldPosition.ts` — a fronteira exata onde plugar X/Y absoluto real
 * quando existir).
 */
export function WorldStage({ fixture, onSelectSettlement, onSelectNpc, onBackgroundClick }: WorldStageProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const cameraRef = useRef<CameraState>(initialCamera(0, 0));
  const dragRef = useRef<{ startX: number; startY: number; startCamera: CameraState; moved: number; captured: boolean } | null>(null);
  const suppressClickRef = useRef(false);
  const agentDotsRef = useRef(new Map<string, Container>());
  const agentMarksRef = useRef(new Map<string, Graphics>());
  const agentSpritesRef = useRef(new Map<string, Sprite>());
  const followRingsRef = useRef(new Map<string, Graphics>());
  const transitionRef = useRef<TransitionState | null>(null);
  const [hover, setHover] = useState<MapHoverTarget | null>(null);

  useEffect(() => {
    const containerEl = containerRef.current;
    if (!containerEl || fixture.settlements.length === 0) return undefined;

    let destroyed = false;
    const app = new Application();
    const worldRoot = new Container();
    const terrainLayer = new Graphics();
    const riverLayer = new Graphics();
    const roadLayer = new Graphics();
    const settlementLayer = new Container();
    const agentLayer = new Container();

    const xs = fixture.settlements.map((s) => s.gridPosition.x);
    const ys = fixture.settlements.map((s) => s.gridPosition.y);
    const minX = Math.min(...xs) - WORLD_PADDING_UNITS;
    const maxX = Math.max(...xs) + WORLD_PADDING_UNITS;
    const minY = Math.min(...ys) - WORLD_PADDING_UNITS;
    const maxY = Math.max(...ys) + WORLD_PADDING_UNITS;
    const centerX = ((minX + maxX) / 2) * WORLD_TILE_PX;
    const centerY = ((minY + maxY) / 2) * WORLD_TILE_PX;
    const overviewZoom = fitZoom((maxX - minX) * WORLD_TILE_PX, (maxY - minY) * WORLD_TILE_PX, containerEl.clientWidth || 680, containerEl.clientHeight || 600);
    cameraRef.current = initialCamera(centerX, centerY, overviewZoom);

    async function setup() {
      await app.init({ resizeTo: containerEl!, backgroundColor: GROUND_BASE, antialias: true });
      if (destroyed) {
        app.destroy(true, { children: true });
        return;
      }
      containerEl!.appendChild(app.canvas);

      app.stage.addChild(worldRoot);
      worldRoot.addChild(terrainLayer, riverLayer, roadLayer, settlementLayer, agentLayer);

      // Terreno (doc §7-13: tile-based, procedural, variação sutil — nunca chapado nem grade
      // óbvia) + florestas em bolsões (doc §19), nunca cobrindo o mapa inteiro numa grade densa.
      const subMinX = Math.floor((minX * WORLD_TILE_PX) / WORLD_SUBTILE_PX);
      const subMaxX = Math.ceil((maxX * WORLD_TILE_PX) / WORLD_SUBTILE_PX);
      const subMinY = Math.floor((minY * WORLD_TILE_PX) / WORLD_SUBTILE_PX);
      const subMaxY = Math.ceil((maxY * WORLD_TILE_PX) / WORLD_SUBTILE_PX);
      for (let gx = subMinX; gx <= subMaxX; gx += 1) {
        for (let gy = subMinY; gy <= subMaxY; gy += 1) {
          const noise = tileNoise(gx, gy, "world-terrain");
          terrainLayer.rect(gx * WORLD_SUBTILE_PX, gy * WORLD_SUBTILE_PX, WORLD_SUBTILE_PX, WORLD_SUBTILE_PX).fill(jitterColor(GROUND_BASE, noise, GROUND_VARIANCE));
          if (noise > FOREST_THRESHOLD) {
            const cx = gx * WORLD_SUBTILE_PX + WORLD_SUBTILE_PX / 2;
            const cy = gy * WORLD_SUBTILE_PX + WORLD_SUBTILE_PX / 2;
            terrainLayer.circle(cx, cy, WORLD_SUBTILE_PX * 0.32).fill({ color: FOREST_COLOR, alpha: 0.85 });
          }
        }
      }
      terrainLayer.eventMode = "static";
      terrainLayer.on("pointertap", () => {
        if (suppressClickRef.current) return;
        // Pedido do usuário 2026-08-27: clicar em terreno vazio (nem cidade, nem NPC) mostra
        // informações do MUNDO no Inspector — mesma paridade de "clicar fora mostra o container
        // atual" que já existe no Settlement View (`onBackgroundClick` de lá).
        onBackgroundClick();
      });

      // Rio — caminho senoidal determinístico atravessando o mapa (doc §15-16: água é elemento
      // físico, tem largura/curva, não uma linha vetorial abstrata).
      const riverSeedNoise = tileNoise(0, 0, `${fixture.world.name}-river`);
      const riverAmplitude = (maxY - minY) * WORLD_TILE_PX * 0.15;
      const riverPhase = riverSeedNoise * Math.PI * 2;
      const riverCenterY = centerY;
      riverLayer.moveTo(minX * WORLD_TILE_PX, riverCenterY);
      const riverSteps = 48;
      for (let i = 1; i <= riverSteps; i += 1) {
        const t = i / riverSteps;
        const x = minX * WORLD_TILE_PX + t * (maxX - minX) * WORLD_TILE_PX;
        const y = riverCenterY + Math.sin(t * Math.PI * 2.4 + riverPhase) * riverAmplitude;
        riverLayer.lineTo(x, y);
      }
      riverLayer.stroke({ width: WORLD_TILE_PX * 0.14, color: RIVER_COLOR, alpha: 0.8, cap: "round", join: "round" });

      // Estradas ligando settlements (doc §17: "precisa conectar visualmente os lugares").
      for (const road of generateWorldRoads(fixture.settlements)) {
        roadLayer
          .moveTo(road.from.x * WORLD_TILE_PX, road.from.y * WORLD_TILE_PX)
          .lineTo(road.to.x * WORLD_TILE_PX, road.to.y * WORLD_TILE_PX)
          .stroke({ width: 8, color: ROAD_COLOR, alpha: 0.8 });
      }

      // Settlements — footprint com a área REAL que o settlement ocupa (pedido do usuário
      // 2026-08-27: "uma cidade com 4 casas que ocupam 4x4 deve ocupar o mesmo terreno no mapa
      // mundi"), não um pin nem um quadrado arbitrário. `settlementFootprintExtent` mede o
      // bounding box de verdade dos prédios (unidades locais) quando existe, com fallback por
      // população pra settlements sem geometria ainda (Millbrook/Stonehaven nesta demo) — nesse
      // caso o tamanho cresce/encolhe com a população (doc §91 "fallback... claramente
      // isolado"). Convertido pra unidades de mundo pela MESMA escala de `map/worldPosition.ts`
      // que agents/tudo mais já usa — nunca um fator de conversão próprio e desalinhado.
      for (const settlement of fixture.settlements) {
        const origin = settlementWorldOrigin(settlement);
        const worldX = origin.x * WORLD_TILE_PX;
        const worldY = origin.y * WORLD_TILE_PX;
        const extent = settlementFootprintExtent(settlement);
        const footprintWidth = Math.max(MIN_FOOTPRINT_PX, (extent.width / LOCAL_UNITS_PER_WORLD_TILE) * WORLD_TILE_PX);
        const footprintHeight = Math.max(MIN_FOOTPRINT_PX, (extent.height / LOCAL_UNITS_PER_WORLD_TILE) * WORLD_TILE_PX);

        const group = new Container();
        group.position.set(worldX, worldY);

        // Handlers no Graphics em si (não no Container que o envolve) — mesmo padrão comprovado
        // do `SettlementStage` (`roof`/`sprite`): um `Container` sem `hitArea` explícito não
        // hit-testa de verdade num mouse real do jeito que o teste sintético sugeriria.
        const footprint = new Graphics()
          .roundRect(-footprintWidth / 2, -footprintHeight / 2, footprintWidth, footprintHeight, Math.min(footprintWidth, footprintHeight) * 0.15)
          .fill(hexOf(SETTLEMENT_PALETTE.top))
          .stroke({ width: 3, color: hexOf(SETTLEMENT_PALETTE.right) });
        footprint.eventMode = "static";
        footprint.cursor = "pointer";
        footprint.on("pointertap", (event) => {
          event.stopPropagation();
          if (suppressClickRef.current) return;
          beginTransition(worldX, worldY, () => onSelectSettlement(settlement.id));
        });
        footprint.on("pointerover", (event: FederatedPointerEvent) => {
          setHover({ kind: "settlement", id: settlement.id, x: event.clientX, y: event.clientY });
        });
        footprint.on("pointerout", () => setHover((current) => (current?.id === settlement.id ? null : current)));
        group.addChild(footprint);

        // Blocos internos — textura de "área construída", determinístico por settlement (não
        // prédios individuais reais, só leitura visual de que ali tem estrutura, doc §26).
        const blockCount = 3 + Math.floor((tileNoise(0, 0, `${settlement.id}-blocks`) * 4) % 4);
        for (let i = 0; i < blockCount; i += 1) {
          const noiseX = tileNoise(i, 1, `${settlement.id}-block-x`);
          const noiseY = tileNoise(i, 2, `${settlement.id}-block-y`);
          const bx = (noiseX - 0.5) * footprintWidth * 0.8;
          const by = (noiseY - 0.5) * footprintHeight * 0.8;
          const size = Math.min(footprintWidth, footprintHeight) * 0.16;
          group.addChild(new Graphics().rect(bx - size / 2, by - size / 2, size, size).fill(hexOf(SETTLEMENT_PALETTE.left)));
        }

        settlementLayer.addChild(group);
      }

      // Agents — ponto com a cor estável do fenótipo em zoom distante (doc §29-30); o sprite
      // real (mesma textura do SettlementStage) some/aparece por zoom no ticker abaixo.
      const agentTextures = await Promise.all(fixture.agents.map((agent) => getNpcTexture(agent.id)));
      if (destroyed) {
        app.destroy(true, { children: true });
        return;
      }

      fixture.agents.forEach((agent, index) => {
        const dot = new Container();

        function onTap(event: FederatedPointerEvent) {
          event.stopPropagation();
          if (suppressClickRef.current) return;
          // Bug real reportado pelo usuário: clicar um agent estava disparando a MESMA
          // animação de "entrar na cidade" usada pro settlement. Clicar um agent é seleção
          // instantânea (doc §42-43) — `CenterStage.useSpatialScope` decide se o mapa mundi
          // continua visível ou não, aqui não navega/dá zoom nenhum.
          onSelectNpc(agent.id);
        }
        function onOver(event: FederatedPointerEvent) {
          setHover({ kind: "agent", id: agent.id, x: event.clientX, y: event.clientY });
        }
        function onOut() {
          setHover((current) => (current?.id === agent.id ? null : current));
        }

        const color = hexOf(appearanceForNpc(agent.id).skin);
        const mark = new Graphics().circle(0, 0, AGENT_DOT_RADIUS).fill(color).stroke({ width: 1, color: 0x0b0e12, alpha: 0.6 });
        mark.eventMode = "static";
        mark.cursor = "pointer";
        mark.on("pointertap", onTap);
        mark.on("pointerover", onOver);
        mark.on("pointerout", onOut);
        dot.addChild(mark);
        agentMarksRef.current.set(agent.id, mark);

        const sprite = new Sprite(agentTextures[index]);
        sprite.anchor.set(0.5, 1);
        sprite.scale.set(WORLD_AGENT_SPRITE_SCALE);
        sprite.visible = false;
        sprite.eventMode = "static";
        sprite.cursor = "pointer";
        sprite.on("pointertap", onTap);
        sprite.on("pointerover", onOver);
        sprite.on("pointerout", onOut);
        dot.addChild(sprite);
        agentSpritesRef.current.set(agent.id, sprite);

        const ring = new Graphics()
          .ellipse(0, FOLLOW_RING_OFFSET_Y, FOLLOW_RING_RADIUS_X, FOLLOW_RING_RADIUS_Y)
          .stroke({ width: FOLLOW_RING_STROKE, color: FOLLOW_RING_COLOR, alpha: 0.9 });
        ring.visible = false;
        dot.addChild(ring);
        followRingsRef.current.set(agent.id, ring);

        agentLayer.addChild(dot);
        agentDotsRef.current.set(agent.id, dot);
      });

      app.ticker.add(() => {
        const now = Date.now();
        // Redesign doc §32/§50 (LOD do agent): ponto discreto de longe, sprite de verdade
        // de perto — a MESMA transição, decidida uma vez por frame pro zoom atual da câmera.
        const showSprites = cameraRef.current.zoom >= SPRITE_REVEAL_ZOOM;

        for (const agent of fixture.agents) {
          const dot = agentDotsRef.current.get(agent.id);
          if (!dot) continue;
          const pos = agentWorldPosition(fixture, agent, now);
          dot.position.set(pos.x * WORLD_TILE_PX, pos.y * WORLD_TILE_PX);

          const mark = agentMarksRef.current.get(agent.id);
          const sprite = agentSpritesRef.current.get(agent.id);
          if (mark) mark.visible = !showSprites;
          if (sprite) sprite.visible = showSprites;
        }

        const activeId = followStore.activeFollowId();
        const followedAgent = activeId ? fixture.agents.find((a) => a.id === activeId) : undefined;
        for (const [agentId, ring] of followRingsRef.current) {
          ring.visible = agentId === activeId;
        }

        if (transitionRef.current) {
          const transition = transitionRef.current;
          cameraRef.current = {
            ...cameraRef.current,
            x: lerp(cameraRef.current.x, transition.targetX, TRANSITION_LERP),
            y: lerp(cameraRef.current.y, transition.targetY, TRANSITION_LERP),
            zoom: lerp(cameraRef.current.zoom, TRANSITION_ZOOM, TRANSITION_LERP),
          };
          if (now - transition.startedAt > TRANSITION_MIN_MS) {
            transitionRef.current = null;
            transition.onComplete();
          }
        } else if (followedAgent && !dragRef.current) {
          // Follow (redesign doc §46: "precisa funcionar em qualquer escala" — mesma mecânica do
          // Settlement, câmera trava só no último ativado, some ao arrastar sem des-seguir).
          // Câmera VIAJA até ele (pedido do usuário 2026-08-27: "animação de câmera... vai me
          // levar até ele", tanto ao começar a seguir quanto ao trocar de alvo via `activate` na
          // aba Followed) — `lerp` a cada frame, nunca um salto direto pra posição dele.
          const pos = agentWorldPosition(fixture, followedAgent, now);
          cameraRef.current = {
            ...cameraRef.current,
            x: lerp(cameraRef.current.x, pos.x * WORLD_TILE_PX, FOLLOW_CAMERA_LERP),
            y: lerp(cameraRef.current.y, pos.y * WORLD_TILE_PX, FOLLOW_CAMERA_LERP),
          };
        }

        const screen = app.screen;
        worldRoot.scale.set(cameraRef.current.zoom);
        worldRoot.position.set(screen.width / 2 - cameraRef.current.x * cameraRef.current.zoom, screen.height / 2 - cameraRef.current.y * cameraRef.current.zoom);
      });
    }

    function beginTransition(worldX: number, worldY: number, onComplete: () => void) {
      if (transitionRef.current) return;
      transitionRef.current = { targetX: worldX, targetY: worldY, onComplete, startedAt: Date.now() };
    }

    function onWheel(event: WheelEvent) {
      event.preventDefault();
      cameraRef.current = zoomBy(cameraRef.current, event.deltaY < 0 ? 1.12 : 1 / 1.12);
    }
    function onPointerDown(event: PointerEvent) {
      dragRef.current = { startX: event.clientX, startY: event.clientY, startCamera: cameraRef.current, moved: 0, captured: false };
    }
    function onPointerMove(event: PointerEvent) {
      const drag = dragRef.current;
      if (!drag) return;
      const dx = event.clientX - drag.startX;
      const dy = event.clientY - drag.startY;
      drag.moved = Math.max(drag.moved, Math.hypot(dx, dy));
      if (drag.moved > CLICK_DRAG_THRESHOLD) {
        suppressClickRef.current = true;
        if (!drag.captured) {
          drag.captured = true;
          containerRef.current?.setPointerCapture(event.pointerId);
          followStore.detachCamera();
        }
      }
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

    void setup();

    return () => {
      destroyed = true;
      containerEl.removeEventListener("wheel", onWheel);
      containerEl.removeEventListener("pointerdown", onPointerDown);
      containerEl.removeEventListener("pointermove", onPointerMove);
      containerEl.removeEventListener("pointerup", onPointerUp);
      containerEl.removeEventListener("pointerleave", onPointerUp);
      agentDotsRef.current.clear();
      agentMarksRef.current.clear();
      agentSpritesRef.current.clear();
      followRingsRef.current.clear();
      transitionRef.current = null;
      try {
        app.destroy(true, { children: true });
      } catch {
        // já destruída (ex.: StrictMode dupla-montagem em dev) — sem problema
      }
      containerEl.replaceChildren();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fixture]);

  return (
    <>
      <div data-testid="world-stage" ref={containerRef} />
      <MapHoverCard fixture={fixture} hover={hover} />
    </>
  );
}
