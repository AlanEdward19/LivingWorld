import type { BuildingKind, WorldFixture } from "../fixture/types";
import type { NavigationStore } from "../nav/NavigationStore";
import { EntityRow, MetricRow, SectionHeader } from "../components/InspectorPrimitives";

export interface BuildingViewProps {
  fixture: WorldFixture;
  nav: NavigationStore;
  buildingId: string;
}

const BUILDING_ICON: Record<BuildingKind, string> = {
  residence: "\u{1F3E0}",
  agriculture: "\u{1F33E}",
  forge: "\u{1F528}",
  generic: "\u{1F3DB}\u{FE0F}",
};

const BUILDING_KIND_LABEL: Record<BuildingKind, string> = {
  residence: "Residence",
  agriculture: "Agriculture",
  forge: "Forge",
  generic: "Building",
};

/**
 * Building Inspector — segunda leva (2026-08-26, "sidebar de um prédio/casa tá feia"): a v1 já
 * tinha trocado `<dl>`/`<ul>` cru pelos primitives, mas ficou sem hierarquia visual de verdade —
 * sem ícone, sem subtítulo, "Key information" (só 1 métrica útil) competindo em destaque com
 * "Occupants" (o que o usuário realmente quer ver primeiro ao entrar numa casa). Header agora
 * segue o mesmo padrão do Settlement Inspector (ícone + nome, "Kind · Settlement" como subtítulo
 * em vez de MetricRow), e Occupants sobe pro topo.
 */
export function BuildingView({ fixture, nav, buildingId }: BuildingViewProps) {
  const settlement = fixture.settlements.find((s) => s.buildings.some((b) => b.id === buildingId));
  const building = settlement?.buildings.find((b) => b.id === buildingId);
  if (!settlement || !building) return null;

  const occupants = fixture.agents.filter((a) => a.indoorLocation?.buildingId === building.id);

  return (
    <div data-testid="building-inspector">
      <h1>
        {BUILDING_ICON[building.kind]} {building.name}
      </h1>
      <p data-testid="building-location">
        {BUILDING_KIND_LABEL[building.kind]} · {settlement.name}
      </p>

      <SectionHeader title="Occupants" trailing={occupants.length} />
      <ul data-testid="building-inspector-people">
        {occupants.map((agent) => (
          <li key={agent.id}>
            <EntityRow title={agent.name} meta={agent.profession} onClick={() => nav.replace({ kind: "agent", id: agent.id })} />
          </li>
        ))}
      </ul>
      {occupants.length === 0 && <p className="inspector-empty-note">No one is inside right now.</p>}

      <SectionHeader title="Key information" />
      <dl>
        <MetricRow label="Floors" value={building.floors.length} />
      </dl>

      <SectionHeader title="Related" />
      <EntityRow title={settlement.name} meta="Settlement" onClick={() => nav.replace({ kind: "settlement", id: settlement.id })} />
    </div>
  );
}
