// Porta exata de CityBoundsResolver.Resolve (src/LivingWorld.Domain/Cities/CityBoundsResolver.cs)
// — o World Creator (CreatorCityEditor) desenhava a cidade num canvas fixo 24x18 desconectado
// do footprint real que o jogo calcula (LIVE-POLISH: usuário via um tamanho no editor e outro,
// bem menor, ao entrar no mundo criado). Mesma fórmula dos dois lados evita o descompasso de
// novo caírem fora de sincronia.
const MIN_SIZE = 3;
const MAX_SIZE = 12;

export function citySide(population: number, mapWidth: number, mapHeight: number): number {
  const populationSide = Math.min(Math.max(Math.ceil(Math.sqrt(Math.max(population, 0)) / 2), MIN_SIZE), MAX_SIZE);
  const mapLimit = Math.max(1, Math.floor(Math.min(mapWidth, mapHeight) / 2));
  return Math.min(populationSide, mapLimit);
}
