# Godot hardening das telas do backend [Estado: Verificado, PASS (Verifier independente, 3 rodadas, 2026-10-15)]

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

## Provas com dados reais (2026-10-15)

- Conversa: servidor atrasado em 12 s (proxy TCP) → a UI desistiu em exatamente 8 s, avisou e descartou a resposta tardia.
- Interior: cliente dentro do prédio 3 publica escopo `Building`; rastro dos NPCs de dentro foi de 0 para 3-5 entradas.
- Tesouro com dados reais de 3 anos de simulação; barras com preenchimento colorido (verde entrou, vermelho saiu).
- Mundo da "grande metrópole": nenhum cenário padrão gera morador do pool, então a prova é o teste .NET do critério 5.

## Out of scope

Rotas novas (issue #77), warning de âncoras do `loading` que já existe na `develop`.
