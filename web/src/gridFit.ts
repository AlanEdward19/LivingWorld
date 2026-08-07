// UX pass 3 (feedback do usuário: "não acho que o mapa deva ter esse limite de tamanho") — sem
// teto de produto algum aqui. O único limite é técnico: um <canvas> maior que ~12000px por lado
// arrisca estourar o limite de canvas do browser (Chrome corta em 32767px) ou travar a aba por
// memória — não é uma escolha de design, é o que o browser aguenta desenhar.
const MAX_CANVAS_PX = 12000;

export function maxSafeZoom(gridWidth: number, gridHeight: number, min = 4): number {
  return Math.max(min, Math.floor(MAX_CANVAS_PX / Math.max(gridWidth, gridHeight, 1)));
}

/// UX pass 3: zoom inicial que preenche a tela em vez de renderizar um quadrado minúsculo
/// (feedback do usuário — "o mapa deveria ser a tela toda"). Só o cálculo do primeiro zoom;
/// +/- continua controlado pelo estado normal do GridCanvas depois disso.
export function computeFitZoom(
  gridWidth: number,
  gridHeight: number,
  availableWidth: number,
  availableHeight: number,
  min = 4,
  max = maxSafeZoom(gridWidth, gridHeight, min),
): number {
  const fit = Math.floor(Math.min(availableWidth / gridWidth, availableHeight / gridHeight));
  return Math.max(min, Math.min(max, fit || min));
}
