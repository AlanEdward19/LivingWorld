// Fase 15.1, T24: casca do editor visual do World Creator — mesma instância de `MapView` do
// Observer (nenhuma segunda implementação de mapa), `EntityInspector` reusado tal como está.
// Como nenhum mundo existe ainda (isto roda ANTES de `POST /worlds/create`), o editor monta suas
// próprias instâncias de `SimulationStore`/`ViewStore`/`SelectionStore` — nunca as do App, que só
// existem depois de um mundo estar de pé — alimentadas por fontes "nulas" (nunca resolvem,
// porque nada aqui assina stream nenhum: `entitiesOf`/`currentPayload` sempre vazios, e todo o
// conteúdo visível vem de `staticEntities`/`cells`, como `WorldMapView` já faz pra cidade/prédio
// derivados). Célula sem pintura fica sem cor: o mapa procedural por Seed só existe no servidor.
import { useEffect, useMemo, useRef, useState, useSyncExternalStore, type ReactNode } from "react";
import { MapView } from "../MapView";
import { createWorld, fetchPeriodCatalog, type PeriodCatalog } from "../../api";
import {
  estimatedSettlementPopulation, parseCsvInts, scenarioFormToJson, type ScenarioFormState,
} from "../../scenarioDefaults";
import { citySide } from "../../map-engine/citySizing";
import { SimulationStore } from "../../state/simulationStore";
import { ViewStore } from "../../state/viewStore";
import { SelectionStore } from "../../state/selectionStore";
import { CATEGORY_COLOR } from "../../map-engine/categoryColors";
import { addSettlement, eraseCell, paintTerrainCell, paintWaterCell, type PaintTool } from "./tools/paint";
import type { CellSource } from "../../map-engine/renderer";
import type { AuthoritativeEntity, EntityRef, EntityRotation, SpaceId } from "../../map-engine/types";
import type { PortalSource, SnapshotSource, TickStreamSource } from "../../data/sources";
import { BehaviorPanel } from "./panels/BehaviorPanel";
import { CitiesPanel } from "./panels/CitiesPanel";
import { DynamicsPanel } from "./panels/DynamicsPanel";
import { EconomyPanel } from "./panels/EconomyPanel";
import { ExtraordinaryPanel } from "./panels/ExtraordinaryPanel";
import { MapPanel } from "./panels/MapPanel";
import { PopulationPanel } from "./panels/PopulationPanel";
import { creatorGroundAt, creatorPaintColor } from "./creatorWorldVisuals";
import { CreatorCityEditor, initialCreatorCityDraft, type CreatorCityDraft } from "./CreatorCityEditor";
import { useEditorHistory } from "./useEditorHistory";

export interface WorldEditorProps {
  initialForm: ScenarioFormState;
  worldName?: string;
  catalogPeriodId?: string;
  onCreated?: (npcCount: number) => void;
  onCancel?: () => void;
  viewport?: { width: number; height: number };
}

const WORLD: SpaceId = { kind: "World" };
const LOD_THRESHOLDS = { aggregate: 4, token: 10, detail: 18 };
const DEFAULT_VIEWPORT = { width: 900, height: 600 };
type ConfigChapter = "overview" | "map" | "population" | "behavior" | "economy" | "cities" | "dynamics" | "extraordinary";
const CONFIG_CHAPTERS: readonly { id: ConfigChapter; icon: string; label: string; description: string }[] = [
  { id: "overview", icon: "✦", label: "Visão", description: "O pulso inicial do mundo" },
  { id: "map", icon: "▦", label: "Território", description: "Escala, relevo e recursos" },
  { id: "population", icon: "●", label: "Povos", description: "Quem começa esta história" },
  { id: "behavior", icon: "◌", label: "Ritmos", description: "Necessidades e rotinas" },
  { id: "economy", icon: "◇", label: "Trocas", description: "Produção e mercados" },
  { id: "cities", icon: "⌂", label: "Assentamentos", description: "Fundação e migração" },
  { id: "dynamics", icon: "↟", label: "Evolução", description: "Mudanças através do tempo" },
  { id: "extraordinary", icon: "✧", label: "Extraordinário", description: "Capacidades opcionais por dados" },
];
const CHAPTER_GUIDES: Record<Exclude<ConfigChapter, "overview">, { question: string; effect: string; recommendation: string; tooltip: string }> = {
  map: { question: "Que território sustenta esta história?", effect: "Escala e deslocamento mudam encontros, rotas e acesso a recursos.", recommendation: "Comece pelo mapa e só ajuste os custos se quiser uma geografia mais hostil.", tooltip: "Custos maiores fazem personagens evitarem certos terrenos e altitudes." },
  population: { question: "Quem presencia o primeiro amanhecer?", effect: "População, longevidade e fertilidade definem o ritmo das gerações.", recommendation: "Mantenha o equilíbrio sugerido na primeira simulação; altere primeiro apenas a população.", tooltip: "Fertilidade e mortalidade afetam décadas de história, não apenas o começo." },
  behavior: { question: "Como as pessoas ocupam seus dias?", effect: "Necessidades e rotinas disputam o tempo de cada personagem.", recommendation: "Use os ritmos sugeridos até observar fome, sono e trabalho em ação.", tooltip: "Decaimento alto torna uma necessidade urgente mais rapidamente." },
  economy: { question: "O que mantém as comunidades vivas?", effect: "Produção, estoque, salários e preços formam as trocas do mundo.", recommendation: "Deixe a economia ativa e personalize receitas somente quando definir recursos próprios.", tooltip: "Sensibilidade determina o quanto escassez e abundância movimentam preços." },
  cities: { question: "Por que as pessoas ficam ou partem?", effect: "Escassez e oportunidade orientam migração, fundação e crescimento.", recommendation: "Posicione assentamentos no mapa; use limiares apenas para dirigir expansão futura.", tooltip: "Os limiares indicam quando falta de comida, moradia ou segurança vira pressão migratória." },
  dynamics: { question: "O mundo pode reinventar suas profissões?", effect: "Vieses e transformações mudam papéis disponíveis ao longo do tempo.", recommendation: "Esta etapa é opcional. Deixe vazia para uma primeira simulação previsível.", tooltip: "Regras de transformação emergem, unem, dividem ou removem profissões em ticks definidos." },
  extraordinary: { question: "O extraordinário faz parte deste mundo?", effect: "Descritores opcionais combinam fonte, efeitos, custos, riscos e manifestações sem arquétipos fixos.", recommendation: "Mantenha desligado para um mundo comum; ligue apenas ao autorar descritores completos.", tooltip: "Manifestações visuais são tags de dados como scale, tint, pallor e trail." },
};

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

export function WorldEditor({
  initialForm,
  worldName = "",
  catalogPeriodId,
  onCreated,
  onCancel,
  viewport = DEFAULT_VIEWPORT,
}: WorldEditorProps) {
  const { value: form, commit: setForm, undo, redo, canUndo, canRedo } = useEditorHistory(initialForm);
  const [catalog, setCatalog] = useState<PeriodCatalog | null>(null);
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
  const [activeChapter, setActiveChapter] = useState<ConfigChapter>("overview");
  const [technicalOpen, setTechnicalOpen] = useState(false);
  const technicalPanelRef = useRef<HTMLDivElement>(null);
  const [editingSettlement, setEditingSettlement] = useState<number | null>(null);
  const [cityDrafts, setCityDrafts] = useState<Record<number, CreatorCityDraft>>({});
  const [settlementRotations, setSettlementRotations] = useState<Record<number, EntityRotation>>({});

  useEffect(() => {
    if (!catalogPeriodId) {
      setCatalog(null);
      return;
    }
    let active = true;
    fetchPeriodCatalog(catalogPeriodId)
      .then((next) => active && setCatalog(next))
      .catch(() => active && setCatalog(null));
    return () => {
      active = false;
    };
  }, [catalogPeriodId]);

  function set<K extends keyof ScenarioFormState>(key: K, value: ScenarioFormState[K]) {
    setForm((current) => ({ ...current, [key]: value }));
  }

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
          return creatorGroundAt(form.seed, x, y).color;
        }
        return creatorPaintColor(form.seed, x, y, painted);
      },
    }),
    [form.width, form.height, form.cells, form.seed],
  );

  // Assentamento = a cidade que o motor funda no tick 0 (mesma lista de `Settlements` no
  // scenario JSON) — selecionável via o hit-test padrão do MapView quando a ferramenta ativa é
  // "selecionar" (T25 intercepta o clique só quando outra ferramenta está ativa).
  const settlementEntities: AuthoritativeEntity[] = useMemo(
    () =>
      form.settlements.map((s, i) => ({
        ref: { kind: "city" as const, id: `settlement:${i}`, space: WORLD },
        position: { x: s.x - 1.5, y: s.y - 1.5 },
        size: { w: 3, h: 3 },
        sizeIsDerived: false,
        color: CATEGORY_COLOR.city,
        label: s.name,
        showBoundary: false,
        rotation: settlementRotations[i] ?? 0,
      })),
    [form.settlements, settlementRotations],
  );

  function handlePaintClick(cell: { x: number; y: number }): boolean {
    if (tool === "select") {
      return false;
    }
    if (cell.x < 0 || cell.y < 0 || cell.x >= form.width || cell.y >= form.height) return true;
    setLastCell(cell);
    if (tool === "erase") {
      const settlementIndex = form.settlements.findIndex((settlement) => Math.abs(settlement.x - cell.x) <= 1 && Math.abs(settlement.y - cell.y) <= 1);
      if (settlementIndex >= 0) {
        deleteSettlement(settlementIndex);
        return true;
      }
    }
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

  function moveSettlement(ref: EntityRef, cell: { x: number; y: number }): boolean {
    if (ref.kind !== "city" || !ref.id.startsWith("settlement:")) return false;
    const index = Number(ref.id.split(":")[1]);
    setForm((current) => ({
      ...current,
      settlements: current.settlements.map((settlement, itemIndex) => itemIndex === index
        ? { ...settlement, x: Math.max(0, Math.min(current.width - 1, cell.x)), y: Math.max(0, Math.min(current.height - 1, cell.y)) }
        : settlement),
    }));
    return true;
  }

  function renameSettlement(index: number, name: string) {
    setForm((current) => ({
      ...current,
      settlements: current.settlements.map((settlement, itemIndex) => itemIndex === index ? { ...settlement, name } : settlement),
    }));
  }

  function deleteSettlement(index: number) {
    setForm((current) => ({ ...current, settlements: current.settlements.filter((_, itemIndex) => itemIndex !== index) }));
    setSettlementRotations((current) => Object.fromEntries(
      Object.entries(current).flatMap(([key, rotation]) => {
        const itemIndex = Number(key);
        if (itemIndex === index) return [];
        return [[itemIndex > index ? itemIndex - 1 : itemIndex, rotation]];
      }),
    ));
    stores.selectionStore.clear();
  }

  function rotateSettlement(index: number) {
    setSettlementRotations((current) => ({ ...current, [index]: nextRotation(current[index] ?? 0) }));
  }

  // Mesmo footprint que o mundo criado vai ter de verdade (CityBoundsResolver, via citySide) —
  // sem isso o editor desenhava um canvas fixo bem maior que a cidade real (LIVE-POLISH).
  function citySizeFor(index: number) {
    const side = citySide(estimatedSettlementPopulation(form, index), form.width, form.height);
    return { width: side, height: side };
  }

  function openSettlement(index: number) {
    setCityDrafts((current) => current[index] ? current : { ...current, [index]: initialCreatorCityDraft(form.seed, index, citySizeFor(index)) });
    stores.selectionStore.clear();
    setEditingSettlement(index);
  }

  function updateCityDraft(index: number, update: (draft: CreatorCityDraft) => CreatorCityDraft) {
    setCityDrafts((current) => ({ ...current, [index]: update(current[index] ?? initialCreatorCityDraft(form.seed, index, citySizeFor(index))) }));
  }

  async function handleCreate() {
    setStatus("submitting");
    setError(null);
    try {
      const response = await createWorld(scenarioFormToJson(form, cityDrafts), worldName);
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

  const selectedSettlementIndex = selection?.kind === "city" && selection.id.startsWith("settlement:")
    ? Number(selection.id.split(":")[1]) : null;
  const selectedSettlement = selectedSettlementIndex === null ? null : form.settlements[selectedSettlementIndex];
  const directorWidth = Math.min(viewport.width * 0.36, 460);
  const worldCamera = useMemo(() => {
    const scale = Math.max(8, Math.ceil(Math.max((viewport.width - directorWidth) / cells.width, viewport.height / cells.height)));
    return { center: { x: viewport.width / (2 * scale), y: cells.height / 2 }, scale };
  }, [cells.width, cells.height, directorWidth, viewport.width, viewport.height]);

  useEffect(() => {
    function handleEditorShortcut(event: KeyboardEvent) {
      if (editingSettlement !== null || isEditableTarget(event.target)) return;
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "z") {
        event.preventDefault();
        event.shiftKey ? redo() : undo();
        stores.selectionStore.clear();
        return;
      }
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "y") {
        event.preventDefault();
        redo();
        stores.selectionStore.clear();
        return;
      }
      if ((event.key === "Delete" || event.key === "Backspace") && selectedSettlementIndex !== null) {
        event.preventDefault();
        deleteSettlement(selectedSettlementIndex);
      } else if (event.key.toLowerCase() === "r" && selectedSettlementIndex !== null) {
        event.preventDefault();
        rotateSettlement(selectedSettlementIndex);
      }
    }
    window.addEventListener("keydown", handleEditorShortcut);
    return () => window.removeEventListener("keydown", handleEditorShortcut);
  }, [editingSettlement, redo, selectedSettlementIndex, undo]);

  useEffect(() => {
    if (!technicalOpen) return;
    for (const label of technicalPanelRef.current?.querySelectorAll("label") ?? []) {
      if (!label.title) label.title = fieldHelp(label.textContent ?? "");
    }
  }, [activeChapter, technicalOpen]);

  if (editingSettlement !== null) {
    const settlement = form.settlements[editingSettlement];
    const citySize = citySizeFor(editingSettlement);
    const draft = cityDrafts[editingSettlement] ?? initialCreatorCityDraft(form.seed, editingSettlement, citySize);
    return (
      <CreatorCityEditor
        cityId={`settlement:${editingSettlement}`} name={settlement.name} seed={form.seed} citySize={citySize}
        viewport={viewport} draft={draft} onDraftChange={(next) => updateCityDraft(editingSettlement, next)}
        onBack={() => setEditingSettlement(null)} simulationStore={stores.simulationStore}
        viewStore={stores.viewStore} selectionStore={stores.selectionStore}
      />
    );
  }

  function chapterContent() {
    let panel: ReactNode;
    switch (activeChapter) {
      case "map": panel = <MapPanel form={form} set={set} />; break;
      case "population": panel = <PopulationPanel form={form} set={set} />; break;
      case "behavior": panel = <BehaviorPanel form={form} set={set} professionNames={catalog?.professionNames} />; break;
      case "economy": panel = <EconomyPanel form={form} set={set} professionNames={catalog?.professionNames} />; break;
      case "cities": panel = <CitiesPanel form={form} set={set} />; break;
      case "dynamics": panel = <DynamicsPanel form={form} set={set} professionNames={catalog?.professionNames} skillNames={catalog?.skillNames} />; break;
      case "extraordinary": panel = <ExtraordinaryPanel form={form} set={set} />; break;
      default: return (
        <div className="world-overview-chapter">
          <span>A primeira página ainda está em branco.</span>
          <h4>Desenhe o lugar onde a história começa.</h4>
          <p>Use as ferramentas no mapa. As decisões profundas ficam nos capítulos ao lado e só aparecem quando você as escolher.</p>
          <dl><div><dt>Seed</dt><dd>{form.seed}</dd></div><div><dt>Área</dt><dd>{form.width * form.height} células</dd></div></dl>
        </div>
      );
    }
    const chapter = CONFIG_CHAPTERS.find((item) => item.id === activeChapter)!;
    const guide = CHAPTER_GUIDES[activeChapter];
    return <div className="director-chapter" data-testid="chapter-guide">
      <span className="director-chapter-eyebrow">Por onde começar</span>
      <h4>{guide.question}</h4>
      <p>{guide.effect}</p>
      <div className="director-recommendation"><span>Direção sugerida</span><strong>{guide.recommendation}</strong><button type="button" aria-label={`Ajuda sobre ${chapter.label}`} title={guide.tooltip}>?</button></div>
      <button type="button" className="director-tuning-toggle" aria-expanded={technicalOpen} onClick={() => setTechnicalOpen((open) => !open)}>
        {technicalOpen ? "Ocultar valores técnicos" : `Ajustar regras de ${chapter.label}`}
      </button>
      {technicalOpen && <div ref={technicalPanelRef} className="director-form-surface">{panel}</div>}
    </div>;
  }

  return (
    <div className="world-editor" data-testid="world-editor">
      <div className="world-editor-toolbar" data-testid="world-editor-toolbar">
        {onCancel && (
          <button type="button" className="ui-btn ui-btn--ghost world-editor-exit" aria-label="Cancelar criação de mundo" onClick={onCancel}>
            ✕ Sair
          </button>
        )}
        <div className="world-tool-dock" aria-label="Ferramentas do mapa">
          {([
            ["select", "◇", "Selecionar"], ["terrain", "▲", "Terreno"], ["water", "≋", "Água"],
            ["erase", "×", "Apagar"], ["settlement", "⌂", "Assentamento"],
          ] as const).map(([value, icon, label]) => (
            <button key={value} type="button" className={tool === value ? "active" : ""} aria-pressed={tool === value} onClick={() => setTool(value)}>
              <span aria-hidden="true">{icon}</span><small>{label}</small>
            </button>
          ))}
        </div>
        <label className="visually-hidden">Ferramenta
          <select aria-label="tool-select" value={tool} onChange={(e) => setTool(e.target.value as PaintTool)}>
            <option value="select">selecionar</option>
            <option value="terrain">pintar terreno</option>
            <option value="water">pintar água</option>
            <option value="erase">apagar célula</option>
            <option value="settlement">assentamento</option>
          </select>
        </label>
        {tool === "terrain" && (
          <div className="world-tool-options">
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
            </label>
          </div>
        )}
        <span className="world-tool-cell" aria-label="tool-last-cell">
          {lastCell ? `Célula: (${lastCell.x}, ${lastCell.y})` : "Célula: —"}
        </span>
        <div className="editor-history-controls" aria-label="Histórico de edição">
          <button type="button" aria-label="Desfazer" title="Desfazer (Ctrl+Z)" disabled={!canUndo} onClick={undo}>↶</button>
          <button type="button" aria-label="Refazer" title="Refazer (Ctrl+Y)" disabled={!canRedo} onClick={redo}>↷</button>
        </div>
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
          initialCamera={worldCamera}
          onPaintClick={handlePaintClick}
          onPaintDrag={["terrain", "water", "erase"].includes(tool) ? handlePaintClick : undefined}
          onEntityMove={tool === "select" ? moveSettlement : undefined}
        />

        {selectedSettlement ? (
          <aside className="side-panel settlement-editor-panel" data-testid="entity-inspector">
            <button type="button" className="side-panel-close" aria-label="fechar-painel" onClick={() => stores.selectionStore.clear()}>×</button>
            <span className="world-config-kicker">Assentamento selecionado</span>
            <label className="settlement-name-field">Nome
              <input aria-label="settlement-name" value={selectedSettlement.name} onChange={(event) => renameSettlement(selectedSettlementIndex!, event.target.value)} />
            </label>
            <div className="settlement-position" data-testid="settlement-position"><span>Posição no mundo</span><strong>{selectedSettlement.x}, {selectedSettlement.y}</strong></div>
            <div className="settlement-position" data-testid="settlement-rotation"><span>Orientação</span><strong>{settlementRotations[selectedSettlementIndex!] ?? 0}°</strong></div>
            <p>Arraste o assentamento diretamente pelo mapa para escolher outro lugar.</p>
            <button type="button" className="rotate-entity-button" aria-label="Rotacionar assentamento" onClick={() => rotateSettlement(selectedSettlementIndex!)}>↻ Rotacionar 90° <kbd>R</kbd></button>
            <button type="button" className="enter-settlement-button" aria-label="Editar por dentro" onClick={() => openSettlement(selectedSettlementIndex!)}>Editar por dentro →</button>
            <button type="button" className="delete-entity-button" aria-label="Apagar assentamento" onClick={() => deleteSettlement(selectedSettlementIndex!)}>Apagar assentamento</button>
          </aside>
        ) : (
          <aside className="side-panel world-director-panel" data-testid="world-general-config">
            <span className="world-config-kicker">Direção do mundo</span>
            <h3>Escolha o próximo capítulo</h3>
            <div className="world-config-stats">
              <div><span>▦</span><small>Mapa</small><strong>{form.width} × {form.height}</strong></div>
              <div><span>●</span><small>População</small><strong>{form.initialPopulation}</strong></div>
              <div><span>⌂</span><small>Assentamentos</small><strong>{form.settlements.length}</strong></div>
            </div>
            <nav className="world-chapter-nav" aria-label="Capítulos da configuração">
              {CONFIG_CHAPTERS.map((chapter) => (
                <button key={chapter.id} type="button" title={chapter.description} aria-pressed={activeChapter === chapter.id} onClick={() => { setActiveChapter(chapter.id); setTechnicalOpen(false); }}>
                  <span aria-hidden="true">{chapter.icon}</span><strong>{chapter.label}</strong><small>{chapter.description}</small>
                </button>
              ))}
            </nav>
            <div key={activeChapter} className="world-editor-panels" data-testid="active-config-chapter">
              {chapterContent()}
            </div>
            <button type="button" className="awaken-world-button" onClick={handleCreate} disabled={status === "submitting"}>
              {status === "submitting" ? "O mundo está despertando…" : "Dar vida ao mundo"}
            </button>
            {error && <p role="alert">{error}</p>}
          </aside>
        )}
      </div>
    </div>
  );
}

function isEditableTarget(target: EventTarget | null): boolean {
  return target instanceof HTMLElement && (target.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName));
}

function fieldHelp(label: string): string {
  const text = label.toLocaleLowerCase("pt-BR");
  if (text.includes("seed")) return "A mesma seed reproduz a mesma paisagem inicial.";
  if (text.includes("id") || text.includes("csv")) return "Identificadores vêm do catálogo do período; listas usam valores separados por vírgula.";
  if (text.includes("limiar")) return "Ponto a partir do qual esta regra passa a influenciar a simulação.";
  if (text.includes("peso")) return "Influência relativa desta condição quando o motor compara alternativas.";
  if (text.includes("tick")) return "Quantidade de ciclos da simulação antes desta mudança acontecer.";
  if (text.includes("chance") || text.includes("taxa") || text.includes("mortalidade")) return "Probabilidade ou intensidade aplicada durante o período indicado.";
  if (text.includes("máx") || text.includes("máxima")) return "Limite superior usado pelo motor para impedir crescimento ou duração indefinidos.";
  if (text.includes("decaimento")) return "Velocidade com que esta necessidade diminui e volta a exigir atenção.";
  return "Ajuste fino desta regra. Passe pelo capítulo recomendado antes de alterar o valor sugerido.";
}

function nextRotation(rotation: EntityRotation): EntityRotation {
  return ((rotation + 90) % 360) as EntityRotation;
}
