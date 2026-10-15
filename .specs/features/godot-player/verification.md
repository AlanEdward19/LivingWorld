# Verificação independente: godot-player (#59 fatia D)

Verifier de contexto em branco (não escreveu o código). HEAD `81db374` (detached em `feat/godot-player`), diff
`feat/godot-beliefs...HEAD` (13 arquivos). Data: 2026-10-15. **Veredito: PASS** (7/7 critérios + padrões), 2 observações não bloqueantes.

## Gates

| Gate | Comando | Resultado |
| --- | --- | --- |
| Cliente | `godot --headless --path client/godot --script res://tests/run.gd` (conteúdo de `client-test.sh`; o harness recusou o script) | `client-test: 169 passed, 0 failed`, exit 0, 0 `SCRIPT ERROR`/`Parse Error` |
| check-docs | `find rules docs .specs/state AGENTS.md ROADMAP.md STATE.md -name '*.md' \| wc -l` | máx. 100 linhas (`phase-25-players.md`); `plan.md` 47 |
| build | `dotnet build LivingWorld.sln -c Release -warnaserror` | 0 Error(s) |
| lint | `dotnet format LivingWorld.sln --verify-no-changes` | exit 0 |
| test (verify) | `LW_BLOCK_EGRESS=1 dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "Category!=Scenario"` | Passed 2193, Failed 0, Skipped 3 |
| contrato | `dotnet test tests/LivingWorld.Tests.Integration --filter "PlayerContractTests\|ApiContractTests"` | Passed 12, Failed 0 |

`verify.sh` não rodou pelo próprio script (o harness recusou); rodei suas 4 etapas à mão, todas verdes.

## Critérios (plan.md) e mutações

As mutações foram aplicadas uma a uma em `test_player` e revertidas (o conteúdo original foi regravado; `git status` limpo).
Toda mutação reprovou **por assert** (`FAIL test_player::...`, exit 1). Em 3 casos houve também `SCRIPT ERROR`, mas sempre junto de um FAIL por assert.

| # | Critério | Evidência | Mutações → teste que reprova | Veredito |
| --- | --- | --- | --- | --- |
| 1 | Encarnar `POST /players/{npcId}/incarnate {playerId}`; 409/404 com mensagem própria; HUD não abre | `api_client.gd:110`; `player_hud.gd:79-93,162-169` | corpo `{player:..}`→`incarnate_posts...`; 409 sem msg / 2 msgs de 409 iguais / 404 sem msg / HUD abre (is_active+visible) / só `visible=true` em erro → `incarnate_errors_have_their_own_messages...` | PASS |
| 2 | Exatamente as 6 de `Choosable`, rótulos do catálogo, "motor decide" = `action: null` | `player_hud.gd:8` (ids = `ActionType.cs:9-18`; `PlayerCommands.cs:22-23`); `api_client.gd:116` | id trocado de **cada** Eat/Sleep/Work/Socialize/Idle/Buy; sem Buy; +Travel; nome Eat→Food; rótulo = nome cru → `action_bar_has_exactly...`; `null` vira `""` / auto chama Idle → `actions_post_the_name...` | PASS |
| 3 | 400 avisa sem fechar; 404 encerra | `player_hud.gd:107-116` | 400 chama `_lost` / 404 removido → `a_refused_action_warns...`; 5xx com msg de 400 → `a_server_error_on_an_action...` | PASS |
| 4 | Logout `POST /players/{playerId}/logout`; "desapareceu" + tick, "não congelou"; câmera solta | `api_client.gd:121`; `player_hud.gd:119-130`; `app.gd` `released→rig.release()`, `incarnate→rig.follow` | sem tick / sem "desapareceu" / sem "não ficou congelado" / rota não chamada → `logout_posts...`; `rig.follow` removido / `release` removido → `app_button_and_i_key...` | PASS |
| 5 | Pausado → aviso; só com personagem; vem de `/simulation/status` | `player_hud.gd:133-135`; `app.gd` `_on_clock_status` → `set_paused` | aviso sempre oculto → 2 testes; aviso sem `is_active` / texto sem "pausado" → `paused_world_note...`; `set_paused` não repassado → `app_forwards_the_admin_pause...` | PASS |
| 6 | Sem personagem não pede nada; só `/players/*`; resposta fora do contrato não encarna; sem requisição por frame; sem clique duplo | `player_hud.gd:97,120`; `api_client.gd:108-122`; nenhum `_process` em `player_hud.gd` | guard `is_active` fora de act / de logout → `inactive_hud_requests_nothing...`; escrita extra `/npcs/1/watchlist` → idem; guard `_busy` fora de act / de logout → `a_second_click_while_waiting...`; shape `""` em incarnate → `responses_are_checked...`; em action / logout → `action_and_logout_bodies_outside...`; `_process` fazendo POST → `an_active_hud_makes_no_requests...` | PASS |
| 7 | `stress:500` com HUD ±3 FPS | `plan.md` § Performance e `client/godot/README.md` | **não reproduzido** (API na 5214 não subida, por instrução). Evidência do autor: sem HUD 74,7/73,7/72,7; com 73,1/73,6/72,6 (média −0,6) | PASS (evidência do autor) |

## Padrões da issue

| Padrão | Evidência / mutação | Veredito |
| --- | --- | --- |
| HTTP só em `api_client.gd` | `grep transport.request` no diff: só `api_client.gd:110,116,121` | PASS |
| Shapes congelados + teste .NET | `tests/contracts/shape.player-{incarnate,action,logout}.json`; `PlayerContractTests.cs`. Mutações nos JSON (npcId→string, campo extra, action→string, sem playerId, tick renomeado) → `PlayerContractTests` Failed 1 cada | PASS |
| `playerId` em `user://player.cfg`, gerado uma vez | `player_profile.gd:8-17`; `save` removido / `return saved` removido → `profile_is_created_once...` | PASS |
| Botão Encarnar + tecla I documentada | `panels.gd` "Encarnar (I)"; `app.gd` KEY_I; README § Controles "Jogar". Botão desligado / I desligada / I sem guard de `is_active` → `app_button_and_i_key...` | PASS |
| Textos pt-BR, avisos por `toasts` | `app.gd` `player_hud.warning → toasts.push` | PASS |
| FPS no README § Desempenho | linha "HUD do jogador" em `client/godot/README.md` | PASS |
| Nenhuma rota de servidor nova | diff não toca `src/` | PASS |

## Observações (não bloqueiam; nenhum critério as exige)

- **O1 (UX, retomada):** o `playerId` persiste e o servidor guarda `ControllerId` (`Npc.cs:115,255`). Se o cliente reabrir
  com o jogador ainda encarnado no servidor, Encarnar devolve 409 "Você já controla outro NPC" e o HUD não abre; como
  `logout()` exige `is_active` (`player_hud.gd:120`), não há como sair pela UI. Sugestão: issue de retomada/logout forçado.
- **O2 (clique duplo no Encarnar):** `app.incarnate` não tem guard de requisição em andamento (I só checa `is_active`,
  que muda após a resposta). Dois cliques rápidos enviam 2 POSTs; o 2º volta 409 e mostra "já é controlado por outro
  jogador" para quem acabou de encarnar. O teste de "duplo I" só cobre o caso após a resposta.
- Desvio menor: o shape entrou em `PlayerContractTests` (factory própria) e não dentro de `ApiContractTests`; usa os
  mesmos helpers (`AssertMatchesContract`/`ShapeOf`). O tick do logout aparece cru; a issue pede "o tick".
