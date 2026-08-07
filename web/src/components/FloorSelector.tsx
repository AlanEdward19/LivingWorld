// Feedback do usuário (2026-08-07): "a questão do Z (andares) não é só em prédios, é em tudo" —
// extraído do seletor local que só existia em `InteriorView.tsx`, agora reusado por
// `CityView`/`WorldMapView` também. Continua estado 100% client-side em cada view (nenhum dado
// de andar/Z existe no motor em nenhum dos 3 níveis — context.md gap 5) — este componente só
// renderiza o controle (botões + rótulo), cada view decide o TEXTO do rótulo (vocabulário
// diferente por nível: "andar"/"subsolo" faz sentido pra prédio, não pra mundo) e o que o andar
// afeta visualmente.
export interface FloorSelectorProps {
  floor: number;
  label: string;
  onChange: (floor: number) => void;
}

export function FloorSelector({ floor, label, onChange }: FloorSelectorProps) {
  return (
    <div className="floor-selector">
      <button type="button" aria-label="andar-abaixo" onClick={() => onChange(floor - 1)}>
        ▼
      </button>
      <span data-testid="floor-label">{label}</span>
      <button type="button" aria-label="andar-acima" onClick={() => onChange(floor + 1)}>
        ▲
      </button>
    </div>
  );
}
