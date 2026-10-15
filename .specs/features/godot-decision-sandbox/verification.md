# Verificação independente — godot-decision-sandbox (#58 fatia 2)

**Veredito: PASS** (todos os critérios, 1–7 do `plan.md` e (a)–(d) da issue). Verifier sem contexto prévio, que não
escreveu o código. 2026-10-14. HEAD `b5e94cc` (detached `feat/decision-sandbox`), diff `feat/observation-lifecycle...HEAD`
(17 arquivos, +855/−4). Toda mutação revertida com `git checkout -- <arquivo>`; `git status` limpo ao fim.

## Gates (rodados pelo Verifier)

| Comando | Resultado |
|---|---|
| `bash scripts/verify.sh` | exit 0, `check-docs: OK`, Unit 2193 passed / 0 failed / 3 skip, `verify: OK` |
| `dotnet test tests/LivingWorld.Tests.Integration --filter "DecisionSandboxEndpointTests\|ApiContractTests"` | 22 passed / 0 failed |
| `bash scripts/test.sh --project integration` (extra) | 397 passed / 0 failed |
| `bash scripts/client-test.sh` (após `--import`) | exit 0, `132 passed, 0 failed` (antes e depois das mutações) |

## Critérios e mutações

Testes .NET: `tests/LivingWorld.Tests.Integration/Sandbox/DecisionSandboxEndpointTests.cs` (abaixo "DSET").
Testes cliente: `client/godot/tests/test_brain_sandbox.gd` (abaixo "TBS").

| # | Critério | Mutação (arquivo) | Teste que reprovou (assert) | |
|---|---|---|---|---|
| 1 / (a) / (d) | 100 chamadas, hash idêntico | `npc.RewritePersonality(...)` no `NpcDecisionSandbox.Run` | DSET `A_hundred_calls_leave_the_canonical_hash_untouched` — `Assert.Equal() Failure: Strings differ` | PASS |
| 1 / (d) | idem, escrita na rota | `SetHunger(h)` no handler de `DecisionSandboxEndpoints.cs` | mesmo teste, mesmo assert | PASS |
| 2 / (b) | só fome muda `topPressures` | ignorar `overrides.Hunger` | `Changing_only_hunger_changes_the_top_pressures`, `Each_need_override…(hunger)`, `ApiContractTests.Decision_sandbox_shape…` | PASS |
| 2 | sede | ignorar `overrides.Thirst` | `Each_need_override…(need: "thirst")` | PASS |
| 2 | sono | ignorar `overrides.Sleep` | `Each_need_override…(need: "sleep")` | PASS |
| 2 | social | ignorar `overrides.Social` | `Each_need_override…(need: "social")` | PASS |
| 2 | traços mudam o rastro | `Personality = npc.Personality` | `Personality_overrides_change_the_decision_trace` | PASS |
| 2 | ação de continuidade | `ContinuityAction: npc.CurrentAction` | `Continuity_action_override_changes_the_trace` | PASS |
| 3 / (c) | valor >100 → 400 | `value is < 0` (sem `> 100`) | `Unknown_npc_is_404_and_invalid_bodies_are_400` — Expected BadRequest, Actual OK | PASS |
| 3 / (c) | traço desconhecido → 400 | `if (false && !Traits.Contains(key))` | mesmo teste, linha 133 | PASS |
| 3 / (c) | ação inválida → 400 | `&& false &&` no `Enum.IsDefined` | mesmo teste, linha 134 | PASS |
| 3 / (c) | NPC morto → 404 | `if (npc is null)` (sem `!IsAlive`) | `Dead_npc_is_404` — Expected NotFound, Actual OK (linha 145) | PASS |
| 4 | enviar só o que difere | `if true or …` em `sandbox_body()` (envia todos) | TBS `test_simulate_posts_only_what_changed…`, `test_traits_are_sent…`, `test_untouched_sliders_follow…` | PASS |
| 4 | não tocados acompanham o NPC | `if not reset and not _touched.is_empty(): return` em `_apply_baseline` | TBS `test_untouched_sliders_follow…` — "o resto acompanha o NPC esperado 33.0, veio 70.0" | PASS |
| 4 | tocados ficam | `if not _touched.has(key)` → `if true` | mesmo teste — "o que o usuário mexeu fica esperado 5.0, veio 40.0" | PASS |
| 4 | desenhar no mesmo fluxo | remover `_draw_flow(trace)` em `simulate()` | TBS `test_simulate_posts…` — "mesmo componente de fluxo esperado [3, 2, 2], veio [2, 1, 2]" | PASS |
| 5 | aviso 400 | remover `warning.emit("Valores inválidos…")` | TBS `test_errors_warn_without_breaking_the_panel` — "esperado 2, veio 1" | PASS |
| 5 | aviso 5xx | remover `warning.emit("Não foi possível simular…")` | `test_errors_warn…` e `test_the_response_is_checked_against_the_frozen_shape` ("esperado 1, veio 0") | PASS |
| 5 | 404 | remover ramo `status == 404` | `test_errors_warn…` — "NPC sumiu esperado <EMPTY_GONE>, veio ''" | PASS |
| 5 | fora do contrato | shape `""` em `api_client.decision_sandbox` | `test_the_response_is_checked…` — "resposta fora do contrato vira aviso esperado 1, veio 0" | PASS |
| 6 | nova hora não troca a simulação | remover `return` de `refresh()` quando há `sandbox_trace` | TBS `test_a_new_hour_does_not_replace_a_simulation_on_screen` — "o fluxo simulado continua na tela" | PASS |
| 6 | painel fechado não simula | `if npc_id < 0` (sem `is_open`) | TBS `test_only_the_sandbox_route…` — "fechado não simula esperado 1, veio 2" | PASS |
| 6 | nenhuma escrita extra | `POST /npcs/%d/watch` antes do sandbox | mesmo teste — "simular é a única escrita … veio [watch, decision-sandbox]" | PASS |
| contrato | shape congelado alterado | `winningUtility: "string"` em `tests/contracts/shape.decision-sandbox.json` | `ApiContractTests.Decision_sandbox_shape_matches_the_committed_contract` | PASS |
| contrato | DTO do servidor muda | `DecisionSandboxResponse(ActionType Choice, …)` | mesmo teste | PASS |
| 7 | FPS ±3 com "E se…?" | — | **não reproduzido** (proibido subir API na 5214). Evidência do autor: sem painel 77,6/78,3/78,6; com painel 74,4/76,2/76,2 (média −2,6), em `plan.md` e `client/godot/README.md:54` | PASS (evidência do autor) |

Todas as falhas acima foram por assert (no 400/5xx um `SCRIPT ERROR` de índice veio *depois* do assert que já reprovara).

## Conferências estáticas

- Rota: `src/LivingWorld.Api/Sandbox/DecisionSandboxEndpoints.cs:23` mapeada em `Program.cs` (`MapDecisionSandboxEndpoints`).
- Leitura pura: `NpcDecisionSandbox.Run` só usa `DecisionContextBuilder.Build` + `context with {…}` + `DecisionSandbox.Decide`.
- ADR-0026 presente e indexado em `docs/adr/README.md`; `plan.md` 47 linhas, ADR 25 (≤100); `check-docs: OK`.
- Cliente valida a resposta contra `shape.decision-sandbox.json` (`api_client.gd:85`); `"null"`/`"[]"` = qualquer valor.

## Observações (não bloqueiam)

1. `Personality_overrides_change_the_decision_trace` muda os 10 traços juntos: ignorar **um** traço (mutação
   `Pick("ambition",…)` → `current.Ambition`) só reprovou pela validação de faixa (`ambition = 200`), não por comportamento.
2. ADR-0026 promete 404 também para NPC "arquivado ou ainda no pool"; só inexistente e morto têm teste.
3. Índice de ADR pula de 0024 para 0026 (0025 não existe nesta branch) — conferir se 0025 está reservado noutra branch.
