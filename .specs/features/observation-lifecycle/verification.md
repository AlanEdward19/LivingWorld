# Verification — observation-lifecycle

Verifier independente (não escreveu o código), 2026-10-14. HEAD `b84557f` (detached em
`feat/observation-lifecycle`). Diff: `git diff feat/godot-npc-conversation...HEAD` (5 arquivos, +144).

**Veredito: PASS** — 4/4 critérios com evidência; 9/9 mutações obrigatórias derrubadas
(8 por assert, 1 só por SCRIPT ERROR com variante semântica derrubada por assert). 1 gap menor (M6b).

## Gates

| Comando | Resultado |
| --- | --- |
| `bash scripts/client-test.sh` | exit 0 — `client-test: 124 passed, 0 failed` |
| `bash scripts/client-test.sh --filter test_app` | exit 0 — 14 passed (3 testes novos `ok`) |
| `dotnet test tests/LivingWorld.Tests.Integration --filter ObservationScopeEndpointsTests` | `Passed! Failed: 0, Passed: 11` |
| `bash scripts/verify.sh` | exit 0 — `check-docs: OK`, Unit `Passed: 2193, Skipped: 3`, `verify: OK` |

`verify.sh` padrão não roda Integration (só `--full`); por isso o filtro de Integration foi rodado à parte.

## Critérios

1. **Purga sem novo POST (.NET)** — PASS. `ObservationScopeEndpoints.cs:25-30` cria `Timer` com período
   `max(250 ms, timeout/2)` que chama `PurgeStale`; descartado em `ApplicationStopping`.
   Teste: `ObservationScopeEndpointsTests.cs` `Silent_source_is_purged_without_any_new_post`.
2. **Fechar a janela retira a fonte e encerra a conversa** — PASS. `boot.gd:57-59` `_notification`
   (WM_CLOSE_REQUEST) → `quit_gracefully` → `_release_app` → `app.release_observation()`;
   `app.gd:424-428` encerra a conversa e limpa o observer. `boot.gd:24` `auto_accept_quit = false`.
   Testes: `test_app::test_closing_the_window_releases_observation_before_quitting`,
   `test_app::test_the_window_close_request_is_intercepted_so_release_can_finish`,
   `test_conversation::test_returning_to_the_menu_ends_the_open_conversation_once` (encerramento da
   conversa é coberto pela composição boot→`release_observation`, não ponta a ponta no close).
3. **Servidor mudo → sai em ≤ 2 s** — PASS. `boot.gd:13` `QUIT_GRACE := 2.0`; `boot.gd:70-72` timer de
   `quit_grace` libera a espera. Teste: `test_app::test_closing_the_window_does_not_hang_when_the_server_does_not_answer`.
4. **Sai uma única vez** — PASS. `boot.gd:64-66` guard `_quitting`. Teste: mesmo de (2), com dois
   `NOTIFICATION_WM_CLOSE_REQUEST` e `eq(quit_calls, [true])`.

## Mutações (aplicadas uma a uma, revertidas com `git checkout -- <arquivo>`)

| # | Mutação | Teste que reprova | Como |
| --- | --- | --- | --- |
| M1 | remove `Timer` de purga periódica (endpoint) | `Silent_source_is_purged_without_any_new_post` | assert (`Assert.False() Failure`) |
| M9a | remove `registry.ClearScope` em `PurgeStale` | `Heartbeat_timeout_removes_...` + `Silent_source_...` | assert |
| M9b | remove `tracker.PurgeStale` no POST | `Heartbeat_timeout_removes_a_stale_source_from_the_registry` | assert |
| M2 | `auto_accept_quit = true` | `test_the_window_close_request_is_intercepted_...` | assert (esperado false, veio true) |
| M3 | remove `_notification` | `test_closing_the_window_releases_...` | **só SCRIPT ERROR** (`Nonexistent function '_notification'`) |
| M3b | `_notification` vira no-op (`pass`) | `test_closing_the_window_releases_...` | assert (esperado [true], veio []) |
| M4 | não chama `release_observation` | `releases_..._before_quitting` ([false]) + `does_not_hang` ([true] cedo) | assert |
| M5 | remove `create_timer(quit_grace)` | `test_closing_the_window_does_not_hang_...` | assert (esperado [true], veio []) |
| M6 | `QUIT_GRACE := 60.0` | `test_closing_the_window_does_not_hang_...` | assert (`check` do teto ≤ 2 s) |
| M7 | remove guard `_quitting` | `test_closing_the_window_releases_...` | assert (esperado [true], veio [true, true]) |
| M8 | remove `end_conversation()` de `app.gd` `release_observation` | `test_conversation::test_returning_to_the_menu_ends_the_open_conversation_once` | assert (esperado 1, veio 0) |
| M6b (extra) | `var quit_grace := 60.0` (desacopla do `QUIT_GRACE`) | **nenhum** — sobrevive | — |

## Defeitos / gaps

- **G1 (menor, não bloqueia):** M6b sobrevive. O teste de prazo sobrescreve `boot.quit_grace = 0.05` e
  verifica só a constante `QUIT_GRACE`; nada assegura que o default de `quit_grace` é `QUIT_GRACE`.
  Sugestão: `eq(load("res://Scenes/boot.gd").new().quit_grace, boot.QUIT_GRACE, ...)`.
- **G2 (informativo):** remover `_notification` (M3) só é pego por SCRIPT ERROR (o teste chama
  `_notification` direto); o gate `client-test.sh` trata isso como falha (exit 1), e a variante
  semântica M3b reprova por assert.
- **G3 (informativo):** a purga periódica usa relógio de parede (`DateTimeOffset.UtcNow`, `Timer`) na
  camada API — fora de `LivingWorld.Simulation`, registro volátil fora do hash; não fere determinismo.
  O teste depende de `Task.Delay(2_500)` com heartbeat de 1 s (margem ~2x; possível flake sob carga).

## Estado final

`git status` limpo exceto este arquivo. Nada commitado. API não subida na 5214; FPS não se aplica.
