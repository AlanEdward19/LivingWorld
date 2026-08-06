/// T11 (fase 15, UX pass 2): cor determinística por id inteiro — o domínio só tem
/// TerrainId/BiomeId/etc. como ints sem nome, então não há "grama"/"deserto" pra mapear; um hash
/// simples em HSL garante que o mesmo id sempre vira a mesma cor, sem inventar semântica.
export function colorById(id: number, saturation = 55, lightness = 45): string {
  const hue = (id * 137.508) % 360; // ângulo áureo — distribui ids vizinhos em cores bem distintas
  return `hsl(${hue.toFixed(1)}, ${saturation}%, ${lightness}%)`;
}
