# Cliente Godot

## Problem
O web era um mapa 2D/VTT: mostrava dados, não um mundo. Quem observa a simulação não vê NPCs andando, dormindo ou
trabalhando, nem entra numa casa. ADR-0022 troca o web por Godot (4.7.2 em `/Applications/Godot_mono.app`). Sem
evidência numérica; o pedido é uma experiência viva, interativa e agradável.
Ao entregar, o usuário circula como espectador pelo mundo isométrico 3D, acompanha cada NPC, entra nas casas
(vários andares, exterior escurecido e desfocado) e consulta NPCs e cidades. **Pré-requisito:** `godot-api-readiness`.

## Flow
Reusa o contrato congelado em `tests/contracts/` e a assinatura `/visual/*`; nada de regra de simulação no cliente.
1. `LivingWorld.Api` (exists) - snapshot e deltas com cursor `(tick, sequence, scopeKey)`
2. `Contract/` (new, door 1) - HTTP + WS (SSE de reserva), valida `shape.*.json`, aplica deltas em ordem, `replay` em lacuna
3. `WorldStore` (new, no door - placement) - estado local por escopo, reconstruível por `subscribe` + `replay`
4. `Scenes/` (new, no door) - mundo, cidade, interior, NPC, câmera, painéis
5. out: pedidos de relógio `/simulation/*`; nada mais escreve no servidor

## Impact
| Front | What changes |
| --- | --- |
| domain | new term: `CameraRig` - câmera trocável (isométrica hoje; 1ª/3ª pessoa na fase do jogador) |
| stored data | nothing to migrate: cache do cliente é descartável |

## Relations
`None - no stored-data shape change`

## Surface
`None - nothing consumed outside`

## Landing
| One-way door | Literal shape | Alternative rejected |
| --- | --- | --- |
| 1 Cliente em GDScript com camada `client/godot/Contract/` | `Contract/` é o único código que fala HTTP/WS; cenas só leem `WorldStore` | C#: GodotSharp 4.7.2 só tem `net8.0`, motor é `net10.0`, e C# não foi exigido |
- Nothing else in this change is hard to reverse

## Criteria
### S1: Cliente de contrato (P1)
Camada única que entrega estado correto e em ordem.

**Acceptance Criteria**
1. WHEN o cliente assina um escopo THEN the contract client SHALL entregar o snapshot e depois os deltas em ordem de `sequence`.
2. IF chega delta com `sequence` diferente de `anterior + 1` THEN the contract client SHALL chamar `/visual/replay` com o último cursor e descartar o delta.
3. IF o canal cai THEN the contract client SHALL reconectar e reidratar por `/visual/replay` a partir do cursor.
4. IF o servidor responde `403` THEN the contract client SHALL expor acesso negado e SHALL NOT exibir payload.
5. IF um campo diverge de `tests/contracts/shape.*.json` THEN the contract client SHALL rejeitar a resposta.

### S2: Mundo isométrico 3D (P1)
1. WHEN o snapshot do mundo chega THEN the client SHALL renderizar terreno, cidades (`location`, `bounds`) e NPCs externos em 3D isométrico.
2. WHEN o usuário arrasta, gira ou dá zoom THEN the camera rig SHALL mover o ponto de vista sem enviar nada ao servidor.
3. WHEN o zoom chega à escala de cidade THEN the client SHALL trocar a assinatura para o escopo cidade.
4. WHEN um delta muda a célula de um NPC THEN the client SHALL mover só aquele NPC, interpolando entre as duas posições.

### S3: NPCs vivos (P1)
1. WHILE a ação do NPC é `Travel`, `Sleep`, `Work`, `Eat`, `Socialize` ou `Idle` the client SHALL tocar a animação correspondente.
2. IF a ação não tem animação mapeada THEN the client SHALL usar pose neutra e exibir o rótulo da ação.
3. The client SHALL exibir, para cada ação, o rótulo de `GET /visual/catalog/actions`, nunca texto próprio.

### S4: Interior multi-andar (P1)
1. WHEN o usuário entra numa casa THEN the client SHALL assinar o escopo interior e escurecer e desfocar o exterior.
2. WHILE dentro de um prédio the client SHALL ocultar teto e andares acima do andar visível.
3. WHEN o usuário troca de andar THEN the client SHALL exibir só os ocupantes daquele andar.
4. WHEN o usuário sai THEN the client SHALL restaurar o exterior e a assinatura anterior.

### S5: Consulta e relógio (P1)
1. WHEN o usuário abre a lista THEN the client SHALL paginar `GET /npcs` do escopo atual, com busca por nome.
2. WHEN o usuário seleciona um NPC THEN the client SHALL exibir `GET /npcs/{id}` e destacá-lo na cena.
3. WHEN o usuário aciona "seguir" THEN the camera rig SHALL acompanhar o NPC até o usuário soltar.
4. WHEN o usuário seleciona uma cidade THEN the client SHALL exibir nome, população e indicadores do `CitySnapshot`.
5. WHEN o usuário aciona pausa, retomar, velocidade ou passo THEN the client SHALL chamar `/simulation/*` e refletir `GET /simulation/status`.
6. IF o servidor está fora do ar THEN the client SHALL mostrar "sem conexão" e tentar de novo com espera crescente.

## Out of scope
| Excluded | Why |
| --- | --- |
| Modo jogador (1ª/3ª pessoa jogáveis, `/visual/player/{id}/move`) | Fase futura; só o `CameraRig` fica pronto |
| Arte final, som, criar mundo e crônicas | Primeira entrega com assets provisórios; telas de criação vêm depois |
| Regra de simulação, multiplayer, autenticação | ADR-0005, ADR-0014, ADR-0015 |

## Assumptions
| Assumption | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Canal de deltas | WebSocket; SSE de reserva | Servidor declara WS como primário | n |
| Arte inicial | Low-poly estilizado com placeholders | Arte final é troca de assets | n |
| CI do cliente | Job separado com Godot headless (`scripts/client-test.sh`), fora de `verify.sh` | ADR-0022 tirou toolchain extra do gate | n |
| Interior ao vivo | Relê o snapshot do interior a cada 1 s | O servidor só publica delta de mundo e cidade | n |
| `step` e `+1 ano` | O cliente reassina o escopo depois do comando | Esses comandos não publicam delta | n |
| Só-leitura | 1 h simulada conectado não altera o hash canônico (exceto relógio) | Prova que o cliente não escreve | n |

**Open questions:** none - all resolved or logged above.

## Observable
| Surface | Decision | Landing |
| --- | --- | --- |
| screen lista de NPCs | empty state | AC 5.1 mostra "nenhum NPC" |
| screen lista de NPCs | error state | AC 5.6 |
| screen interior | prédio sem layout servido | n/a - coberto por `godot-api-readiness`; sem layout o cliente não abre o interior |
| screen mundo | loading state | AC 1.1 - tela de carregamento até o snapshot |

## Sources
- ADR-0022 - Godot substitui o web; `.specs/features/godot-api-readiness/plan.md` - dados que o cliente consome
