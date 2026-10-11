# Experiência do cliente Godot

## Problem
A primeira versão do cliente (`godot-client`) cumpre o contrato, mas parece uma ferramenta, não um mundo: terreno plano
sem vida, prédios como blocos, NPCs de cápsula, sem hora do dia, sem dicas ao passar o mouse, sem mapa nem avisos.
Pedido do usuário, literal: "ta muito simples o client". Quem observa a simulação não sente o tempo passar nem sabe
onde está. Sem número de referência; o critério é visual e de uso.
Ao entregar, o mundo tem dia e noite, cenário, prédios com telhado e janelas acesas, NPCs articulados, minimapa, dicas e avisos.

## Flow
Reusa `CursorStream`, `WorldStore` e `CameraRig` de `godot-client`; só acrescenta apresentação sobre o mesmo contrato.
1. `ClockPanel` (exists) - lê `/simulation/status` e emite `status_changed`
2. `DayCycle` (new, no door - placement) - interpola a hora entre leituras; dirige sol, céu e janelas
3. `WorldView` / `Scenery` / `NpcActor` (exist/new) - árvores, água, telhados, janelas, NPC articulado por pose
4. `App` (exists) - dica sob o cursor, nomes (`GET /npcs`), avisos dos `events` dos deltas, carregamento, minimapa

## Impact
| Front | What changes |
| --- | --- |
| domain | new term: `DayCycle` - hora do dia (0–24) derivada do tick do servidor; dia de 24 h é suposição do cliente |
| stored data | nothing to migrate: só apresentação |

## Relations
`None - no stored-data shape change`

## Surface
`None - nothing consumed outside`

## Landing
- None - toda a mudança é reversível por refatoração; nenhuma rota, dado persistido ou dependência nova

## Criteria
### E1: Tempo e luz (P1)
O mundo mostra a hora e a luz acompanha o relógio do servidor.
**Acceptance Criteria**
1. WHEN o servidor informa tick 12 THEN the client SHALL usar luz de meio-dia e janelas apagadas; no tick 23, luz de noite e janelas acesas.
2. WHILE o relógio do servidor está pausado the client SHALL manter a hora parada.
3. WHILE o relógio roda a N ticks/s the client SHALL avançar a hora N horas por segundo entre as leituras.
### E2: Orientação (P1)
1. WHEN o cursor está sobre NPC, prédio ou cidade THEN the client SHALL mostrar dica com nome e rótulo de ação do servidor, andares ou população.
2. WHEN o usuário clica no minimapa THEN the client SHALL levar a câmera até a célula clicada sem enviar nada ao servidor.
3. WHEN chega um delta com `events` THEN the client SHALL avisar com o texto `label` do servidor e guardar até 5 visíveis e todo o histórico.
4. WHILE não chegou o primeiro snapshot the client SHALL mostrar a tela de carregamento.
5. WHEN a lista `GET /npcs` chega THEN the client SHALL mostrar o nome sobre o NPC selecionado ou sob o cursor.
### E4: Vida ambiente (P2)
1. WHILE é noite plena (23 h) the client SHALL manter os postes das portas acesos, e apagados ao meio-dia, com brilho crescente no entardecer e decrescente no amanhecer.
2. WHILE o mundo está aberto the client SHALL mover as sombras de nuvens e reentrar a nuvem pelo lado oposto ao sair do mapa, sem desenhar a nuvem.
3. WHEN a luz do dia muda THEN the client SHALL trocar a camada de som: pássaros de dia, grilos à noite, e M SHALL silenciar.
### E5: Multidão (P2)
1. WHILE há mais de 150 NPCs e a câmera está longe (zoom > 16) the client SHALL desenhar cada NPC só com tronco e cabeça, sem sombra própria.
2. WHEN a câmera se aproxima OR há 150 NPCs ou menos THEN the client SHALL devolver o NPC ao detalhe completo.
### E3: Ficha do NPC (P2)
1. WHEN o usuário seleciona um NPC THEN the client SHALL mostrar barras de saúde, fome, sede, sono e social com os valores de `GET /npcs/{id}`.

## Out of scope
| Excluded | Why |
| --- | --- |
| Música, arte final, modelos importados | Assets provisórios procedurais; troca de arte é fase própria |
| Calendário do cenário (meses) | O servidor não serve a duração do dia; o cliente assume 24 h |
| Mobília e cômodos do interior | Apresentação futura; só cama (sono) e ferramenta (trabalho) seguem a ação |

## Assumptions
| Assumption | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Hora do dia | `tick % 24` | O cenário padrão usa `WorldCalendar(24,30,12)` | n |
| Ids de terreno | Paleta fixa por id; rios da camada `Rivers` | A API não serve nome de terreno | n |
| Brilho dos postes | Sobe e desce com a luz do dia (não liga e desliga) | Evita um salto às 19 h; pedido literal era noite = aceso | n |
| Desempenho | Medido com `--steps "stress:500"`: 77 FPS longe, 52 FPS perto (M1 Pro) | Sem baseline versionado ainda | n |

**Open questions:** none - all resolved or logged above.

## Observable
| Surface | Decision | Landing |
| --- | --- | --- |
| screen mundo | loading state | AC 2.4 |
| screen mundo | error state (servidor fora) | existing - banner "sem conexão" de `godot-client` |
| screen avisos | empty state | n/a - sem aviso, nada aparece |

## Sources
- Pedido do usuário nesta sessão; `.specs/features/godot-client/plan.md`
