# Fase 15 (Mapa visual VTT 2D) Specification

## Problem Statement
Hoje o mundo existe só em backend e não há uma visualização viva para acompanhar cidade, NPCs e eventos. Precisamos de um cliente React + TypeScript em VTT 2D, em tempo real e com dois modos (espectador/admin e personagem), incluindo fog of war, navegação até interiores e camadas derivadas (terreno, rios, clima etc.), sem criar uma segunda fonte de verdade.

## Goals
- [ ] Visualizar mundo vivo em VTT 2D com sinais claros de atividade (movimento, trabalho, conflito, interação).
- [ ] Entregar dois modos: espectador global e personagem jogável com visão limitada por conhecimento.
- [ ] Aplicar resolução por foco de tela: mapa-múndi simplificado, cidade detalhada, interior máximo.
- [ ] Exibir camadas de visualização derivadas sobre o mesmo grid (geografia, infraestrutura, sociedade e clima).
- [ ] Garantir aparência inicial de NPC por token 2D composto dinamicamente e de forma determinística.
- [ ] Manter motor como autoridade: cliente recebe leitura/deltas e envia apenas intenções validadas.

## Out of Scope
| Feature | Reason |
| --- | --- |
| Cliente 3D, voz, lip sync, Unreal | Fase 14 (adiada) |
| Mecânicas novas de economia/sociedade/família | Pertencem às fases donas da mecânica |
| LLM escrevendo estado do mundo | Violação de fronteira do motor |

## Assumptions & Open Questions
| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| "Simular só o que está sendo enxergado" | Restringe detalhe de stream/render por foco, não pausa o tick global do mundo | Preserva imersão + performance sem quebrar determinismo | y |
| Canal realtime | WebSocket primário, SSE fallback para leitura espectador | Realtime bidirecional para personagem, fallback barato para monitoramento | n |
| Biblioteca visual de NPC | Asset pack 2D versionado + composição dinâmica por camadas | Entrega aparência rápida sem pipeline 3D | y |
| Open questions | none | Escopo da fase está definido para design | n/a |

## User Stories
### P1: Modo espectador/admin global
**User Story**: Como admin/espectador, quero ver o mundo inteiro vivo em mapa-múndi para monitorar cidades, NPCs externos e eventos.
1. WHEN o espectador abre o mapa-múndi THEN sistema SHALL enviar visão simplificada global com cidades e NPCs externos agregados por LOD.
2. WHEN eventos ativos ocorrem THEN sistema SHALL emitir marcadores/animadores visuais em tempo real no escopo correspondente.
3. WHEN o espectador faz drill-down THEN sistema SHALL trocar para o escopo detalhado (cidade/interior) sem perder continuidade temporal.

### P1: Camadas derivadas no mesmo grid
**User Story**: Como espectador/jogador, quero alternar camadas (terreno, bioma, rios, montanhas, recursos, estradas, fronteiras, reinos, cidades, aldeias, rotas, migrações, conflitos, clima) para entender o estado do mundo sem duplicar dados.
1. WHEN uma camada é selecionada THEN sistema SHALL renderizar a projeção derivada correspondente sobre o mesmo grid base.
2. WHEN uma camada é adicionada ao catálogo THEN sistema SHALL exigir endpoint/stream e renderer React registrados para essa camada.
3. WHEN dados canônicos mudam por tick THEN sistema SHALL refletir as camadas derivadas via deltas, sem escrita de volta no domínio.

### P1: Modo personagem com FOW
**User Story**: Como jogador, quero controlar meu personagem e ver apenas o que ele conhece.
1. WHEN personagem se move por click ou W/A/S/D THEN sistema SHALL validar intenção no servidor e publicar delta espacial do personagem.
2. WHEN área não foi descoberta THEN sistema SHALL aplicar fog of war; áreas visitadas/desbloqueadas SHALL permanecer visíveis conforme regra.
3. WHEN admin aplica override THEN sistema SHALL liberar visão total sem alterar estado social/econômico do mundo.

### P1: Resolução por foco (performance + imersão)
**User Story**: Como operador, quero que o detalhe visual seja proporcional ao foco atual da tela.
1. WHEN foco é mapa-múndi THEN sistema SHALL transmitir apenas dados simplificados globais.
2. WHEN foco entra em cidade THEN sistema SHALL transmitir entidades/atividades detalhadas da cidade focada.
3. WHEN foco entra em prédio/interior THEN sistema SHALL transmitir detalhamento máximo local.
4. WHEN foco sai de um escopo THEN sistema SHALL rebaixar o stream daquele escopo para resolução inferior.

### P1: Aparência inicial de NPC por token 2D
**User Story**: Como jogador, quero reconhecer NPCs por aparência visual consistente.
1. WHEN NPC é renderizado THEN sistema SHALL compor token circular 2D por camadas (ex.: pele, cabelo, roupa, acessório de profissão).
2. WHEN mesmo NPC é exibido com mesma seed/snapshot THEN sistema SHALL gerar token idêntico em qualquer cliente.
3. WHEN estado relevante do NPC muda THEN sistema SHALL alterar somente camadas previstas no catálogo visual.

## Edge Cases
- WHEN conexão realtime cai no meio do drill-down THEN sistema SHALL reidratar snapshot do escopo + replay de deltas sem escrita de mundo.
- WHEN jogador tenta movimento inválido (fora de navegação permitida) THEN sistema SHALL rejeitar com erro explícito e hash canônico inalterado.
- WHEN cliente assina escopo sem permissão (admin-only) THEN sistema SHALL negar subscribe e não vazar dados.

## Requirement Traceability
| Requirement ID | Story | Status |
| --- | --- | --- |
| VTT-01..03 | Modo espectador/admin global | Pending |
| VTT-04..06 | Camadas derivadas no mesmo grid | Pending |
| VTT-07..09 | Modo personagem com FOW | Pending |
| VTT-10..13 | Resolução por foco | Pending |
| VTT-14..16 | Aparência inicial de NPC | Pending |

## Success Criteria
- [ ] Espectador vê mundo vivo com LOD simplificado no mapa-múndi e drill-down contínuo.
- [ ] Camadas derivadas (incluindo rios e clima) são alternáveis e consistentes com o mesmo grid.
- [ ] Jogador controla personagem com visão limitada por descoberta/FOW.
- [ ] Detalhe de transmissão acompanha foco (global → cidade → interior) com custo controlado.
- [ ] Tokens de NPC são consistentes, determinísticos e compostos por asset pack versionado.
