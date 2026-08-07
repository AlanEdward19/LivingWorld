// Fase 15.1, T14: orientação espacial "World / Cidade X / Prédio Y" (design.md; master prompt
// §8) — não é navegação de página administrativa, só a cadeia de ancestrais de `space.ts`
// tornada clicável. O último item (espaço atual) não é clicável.
import { ancestors, toScopeKey } from "../map-engine/space";
import type { SpaceId } from "../map-engine/types";

export interface BreadcrumbProps {
  space: SpaceId;
  onNavigate: (target: SpaceId) => void;
}

function labelFor(space: SpaceId): string {
  switch (space.kind) {
    case "World":
      return "Mundo";
    case "City":
      return `Cidade ${space.cityId.slice(0, 8)}`;
    case "Building":
      return `Prédio ${space.buildingId}`;
  }
}

export function Breadcrumb({ space, onNavigate }: BreadcrumbProps) {
  const chain = ancestors(space);

  return (
    <nav className="breadcrumb" aria-label="breadcrumb">
      {chain.map((ancestor, index) => {
        const isCurrent = index === chain.length - 1;
        return (
          <span key={toScopeKey(ancestor)}>
            {index > 0 && <span aria-hidden="true"> / </span>}
            <button type="button" disabled={isCurrent} onClick={() => onNavigate(ancestor)}>
              {labelFor(ancestor)}
            </button>
          </span>
        );
      })}
    </nav>
  );
}
