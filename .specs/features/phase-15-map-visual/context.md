# Fase 15 (Mapa visual VTT 2D) Context

## Deferred Ideas
- **T4**: as camadas `Roads`, `Borders`, `Kingdoms`, `Climate` e `Mountains` (VisualLayerId) não têm dado canônico no motor hoje — nenhuma classe `Kingdom`/`Border`/`Road`/`Climate` existe em `LivingWorld.Domain`, e `MapCell.Altitude` não tem limiar documentado para "montanha". `GlobalLayerBuilder.Build` retorna `LayerBuildResult.NotYetModeled` pra essas 5 (ver `src/LivingWorld.Api/Visual/Layers/GlobalLayerBuilder.cs`). Precisa de uma fase/task futura que modele esses conceitos no domínio antes dessas camadas terem conteúdo real.
- **T4**: `GlobalSnapshot.ActiveEvents` sempre vazio — o motor não tem noção de "evento em andamento" (guerra/festival/desastre), só histórico ponto-a-ponto (`Facts`/event log). Precisa de um conceito de evento-em-curso no domínio antes de resumir isso pro mapa-múndi (VTT-02).
