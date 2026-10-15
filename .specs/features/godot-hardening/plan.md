# Godot hardening das telas do backend [Estado: Verificado, PASS (Verifier independente, rodadas 1 a 2c, 2026-10-15)]

Fecha os buracos listados depois das #58/#59 (PRs #78 a #88). Sem rota nova.

## Problem

Buracos apontados na revisão: runner do cliente imprimindo `ok` com `SCRIPT ERROR`; cinco janelas de informação sobrepostas e
sem layout para tela curta; sandbox com meta de provisão diferente da do motor; teste de purga com folga fixa; telas só vistas com
fixture (morador do pool, interior, timeout de conversa, dados reais); FPS medido com a máquina ocupada.

## Flow

1. `tests/run.gd` (extended) - `Logger` do motor captura `SCRIPT ERROR` e o teste falha
2. `Scenes/app.gd` (extended) - `_exclusive` (uma janela de informação por vez) e `size_windows` (altura mínima)
3. `NpcDecisionSandbox.BuildContext` (new) - mesma meta de provisão (`ProvisionTargetFor`) do motor
4. `PooledNpcContractTests` (new .NET) - morador do pool passa no contrato congelado da ficha; GET não materializa; POST materializa
5. `ObservationScopeEndpointsTests` - purga por espera ativa (até 15 s) em vez de 2,5 s fixos
6. `dev_steps.gd` - `quit` retira a fonte; `demo:*` e `timesay` para capturas e medições

## Impact

| Front | What changes |
| --- | --- |
| domain | none |
| stored data | none |
| contract | nenhum |

## Landing

- Nada difícil de reverter.

## Criteria

1. WHEN um teste do cliente causa `SCRIPT ERROR` THEN o runner SHALL marcá-lo como FALHA (não `ok`)
2. WHEN uma janela de informação abre THEN as outras quatro SHALL fechar; conversa e HUD do jogador não fecham por isso
3. The system SHALL dar às janelas de informação altura mínima 220; a largura não precisa de layout por tela (o canvas `canvas_items` 1600 x 900 tem sempre 1600 unidades ou mais de largura, e tela física estreita só escala a interface)
4. The system SHALL montar o contexto do sandbox com a mesma meta de provisão do motor, para todo NPC vivo
5. WHEN `/npcs/{id}` é de um morador do pool THEN SHALL ter `lod` 2 e passar no contrato `npc-inspect`; GET não materializa; POST materializa (`lod` 0)
6. The system SHALL passar `stress:500` com cada tela aberta dentro de ±3 FPS da base (máquina ociosa, README § Desempenho)

**Independent test:** `bash scripts/client-test.sh --filter test_windows` e `dotnet test --filter "PooledNpcContractTests|DecisionSandboxEndpointTests"`.

## Rodada 2 (dívidas da #77 e buracos restantes)

7. WHEN o mundo é criado pelo menu THEN `POST /worlds/create` SHALL levar `periodId`; `GET /world/info` SHALL devolver `{name, periodId, technologyAxes}`; o período sobrevive a snapshot/reidratação sem mexer no hash canônico (ADR-0027)
8. WHILE o mundo está pausado por admin THEN `GET /simulation/status` SHALL trazer `pausedBy/pauseReason/pausedAtTick/pauseMaxTicks` e a faixa do topo SHALL nomear quem pausou e por quê para QUALQUER cliente; sem pausa de admin os campos são nulos
9. The system SHALL validar contratos com campo opcional tipado (chave `campo?`): ausente ou nulo passa, com valor tem de cumprir a forma; aplicado a ficha de NPC, status, admin, conversa, jogador, tecnologias, sandbox e diretório/deltas
10. The system SHALL rotular os eixos de tecnologia pelo cenário (`technologyAxes`), com eixo desconhecido capitalizado e vetor maior que os eixos numerado
11. IF o servidor ainda liga o `playerId` a um NPC (409 "já controla outro NPC") THEN o HUD SHALL oferecer só "Sair do personagem anterior"; duplo clique em Encarnar SHALL mandar um único POST
12. The system SHALL levar da ficha da cidade a um morador anônimo (`pendingResidentIds`) até "Ver em detalhe"; a ficha anônima SHALL esconder barras e botões sem sentido
13. The system SHALL refletir no `ROADMAP.md` as fases 10, 11, 12, 24, 25 e 26 com tela no cliente

## Provas com dados reais (2026-10-15)

- Conversa: servidor atrasado em 12 s (proxy TCP) → a UI desistiu em exatamente 8 s, avisou e descartou a resposta tardia.
- Interior: cliente dentro do prédio 3 publica escopo `Building`; rastro dos NPCs de dentro foi de 0 para 3-5 entradas.
- Tesouro com dados reais de 3 anos de simulação; barras com preenchimento colorido (verde entrou, vermelho saiu).
- Morador do pool real: mundo criado com cidade autorada com `AggregatePool` (30 moradores); `GET /npcs/0` deu `lod` 2, a ficha mostrou o aviso e "Ver em detalhe" e o POST materializou (`lod` 0). Teste .NET do critério 5 cobre o contrato.
- `GET /world/info` real devolveu o período do mundo criado pelo cliente.
- História/narrativas: mundo de 12 anos sem fatos nem relatos porque o `ScenarioLoaderV2` não liga `HistoryRules`; vira a issue #91 (fora do escopo do cliente).

## Testes removidos de propósito

- `test_economy::test_boot_hands_the_created_period_to_the_app` e `test_main_menu::test_continuing_the_current_world_forgets_the_period...`: o período deixou de passar pelo Boot (agora vem de `GET /world/info`), então o `selected_period` do menu não chega mais ao app; o teste novo `test_creating_a_world_sends_the_chosen_period_id` cobre o envio na criação. `test_resource_names_come_from_the_period_catalog_when_the_period_is_known` foi só renomeado para `..._from_the_world_info_period_and_the_catalog`.

## Out of scope

Warning de âncoras do `loading` que já existe na `develop`; história vazia (#91); conversa com LLM real (#90).
