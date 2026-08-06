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
| Arte pixel-art/pintada à mão (tiles, tokens, ícones ilustrados) | Sem pipeline de assets de arte no projeto; token/terreno são procedurais (cor determinística por id/hash), não arte curada (decisão 2026-08-06) |
| Movimento de personagem em escala mundo-múndi ("andar até a saída da cidade" pra trocar de escopo) | Domínio só valida movimento dentro do escopo de cidade hoje (`VisualInputEndpoints`); trocar de escopo por movimento real exige um sistema de movimento em escala de mapa-múndi que não existe — ver "Open Questions" abaixo |

## UX Pass 2 (2026-08-06) — grid real, tokens, painel lateral, mapa in-game
Escopo adicional pedido pelo usuário depois do T8 original entregar só listas/botões (não um grid de verdade). Cobre: renderização 2D real do mapa-múndi e da cidade, NPCs como token/dot por LOD de zoom, seleção por clique com painel lateral de informações/ações, editor de mapa por clique na tela "criar mundo" (em vez de só campos numéricos), e overlay de mapa (tecla M) em modo jogador.

### P1: Grid 2D real no mapa-múndi e na cidade
**User Story**: Como espectador/jogador, quero ver um grid 2D de verdade (não lista/botão) com terreno colorido por camada, cidades e NPCs plotados na posição real.
1. WHEN o mapa-múndi é aberto THEN sistema SHALL renderizar um canvas com uma célula por `(x,y)` do grid, colorida pela camada `Terrain` ativa (determinístico por id, sem arte).
2. WHEN uma cidade/NPC externo existe no snapshot THEN sistema SHALL plotar um marcador na coordenada real (`CellCoord`) sobre o grid, não numa lista separada.
3. WHEN o usuário clica numa célula vazia THEN sistema SHALL não fazer nada (só marcador/cidade são clicáveis).

### P1: Zoom com LOD dot↔token
**User Story**: Como espectador, quero que NPCs virem pontos brilhantes simples quando eu der zoom out (economia de recurso/legibilidade) e tokens maiores quando eu der zoom in.
1. WHEN o nível de zoom está abaixo de um limiar THEN sistema SHALL renderizar cada NPC como um dot (círculo pequeno, cor única).
2. WHEN o nível de zoom está no ou acima do limiar THEN sistema SHALL renderizar cada NPC como um token maior (círculo com anel/cor derivada do id do NPC — sem aparência ilustrada, ver Out of Scope).
3. WHEN o usuário aumenta/diminui o zoom THEN sistema SHALL re-renderizar sem nova requisição ao servidor (é decisão só de cliente sobre o mesmo snapshot).

### P1: Seleção por clique → painel lateral
**User Story**: Como espectador/jogador, quero clicar numa cidade ou NPC e ver as informações dela num painel lateral, sem trocar de tela.
1. WHEN o usuário clica numa cidade no mapa-múndi THEN sistema SHALL abrir um painel lateral direito com nome/id, população e um botão "Entrar" que faz o drill-down existente (troca de escopo).
2. WHEN o usuário clica num NPC (externo no mapa-múndi, ou morador na cidade) THEN sistema SHALL abrir o painel lateral com id/posição/ação atual.
3. WHEN o painel lateral está aberto e o usuário clica fora ou no X THEN sistema SHALL fechar o painel sem side-effect no mundo.

### P2: Editor de mapa por clique em "criar mundo"
**User Story**: Como usuário criando um mundo, quero pintar terreno/bioma e posicionar assentamentos clicando num grid, em vez de digitar arrays de números.
1. WHEN o usuário seleciona um id de terreno/bioma e clica numa célula do grid do formulário THEN sistema SHALL pintar aquela célula com o id escolhido.
2. WHEN o usuário ativa o modo "assentamento" e clica numa célula THEN sistema SHALL adicionar um assentamento naquela coordenada.
3. WHEN o formulário é submetido THEN sistema SHALL enviar o array `Cells` já preenchido com o que foi pintado (autoria explícita), não mais depender só de geração procedural por seed.

### P2: Overlay de mapa em modo jogador (tecla M)
**User Story**: Como jogador dentro de uma cidade/interior, quero apertar M e ver o mapa (só visualização, como em um RPG) sem perder meu estado atual.
1. WHEN o jogador aperta M dentro do escopo cidade/interior THEN sistema SHALL abrir um overlay com o mapa-múndi em modo somente-leitura (sem clique/drill-down).
2. WHEN o jogador aperta M novamente ou Esc THEN sistema SHALL fechar o overlay e manter o escopo/estado anterior intacto.

## Open Questions (UX Pass 2)
| Question | Status |
| --- | --- |
| Jogador deve "andar até a saída" pra sair da cidade (em vez de um botão de voltar) — isso exige movimento validado em escala mapa-múndi, que não existe hoje. Construir agora ou manter botão de drill-down/volta por enquanto? | Deferred — mantido botão de volta/painel por enquanto; movimento mundo-múndi fica registrado como melhoria futura, não bloqueia esta fase |
| Prédios não têm `CellCoord` no domínio (`Building` não guarda posição) — como plotá-los no grid da cidade? | Resolvido nesta passada: layout calculado no cliente (não é posição real do domínio), marcado visualmente como aproximado |

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
