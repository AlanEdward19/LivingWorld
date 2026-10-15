# Verification: godot-materialize (#59 fatia H)

Verifier independente (contexto em branco, não escreveu o código), 2026-10-15. HEAD `d77877e` (detached),
diff `feat/godot-technologies...HEAD`. **Resultado: PASS** (critérios 1-4 + comuns 1-3, 5); critério 5 (FPS) **não reproduzido**.

## Gates (rodados pelo Verifier)

O harness do agente recusou `bash scripts/*.sh`; rodei o conteúdo equivalente à mão:
- client-test: `Godot --headless --path client/godot --script res://tests/run.gd` → exit 0, `client-test: 204 passed, 0 failed`,
  0 ocorrências de `SCRIPT ERROR|Parse Error`; os 7 de `test_materialize` em `ok`.
- verify (equivalente): check-docs (AGENTS 99, ROADMAP 86, STATE 100, .specs/STATE 63; nada >100 em rules/docs/.specs/state)
  · `dotnet build -c Release -warnaserror` 0 erros · `dotnet format --verify-no-changes` exit 0
  · Unit `Category!=Scenario`: Passed 2193, Skipped 3, Failed 0.
- `dotnet test tests/LivingWorld.Tests.Integration --filter ApiContractTests`: Passed 11/11.
- Integração inteira: Passed 403/403.

## Critérios do plan.md

| # | Critério | Evidência | Veredito |
|---|---|---|---|
| 1 | lod 2 mostra aviso + "Ver em detalhe"; 0/1 não | `panels.gd` `_present`: `pooled_note.visible = lod == 2`, `materialize_button.visible = lod == 2`; testes 1-2 | PASS |
| 2 | GET não materializa | `show_npc` só chama `api.npc_detail`; teste 1 `transport.calls == ["GET /npcs/9"]` | PASS |
| 3 | botão → `POST /npcs/{id}/materialize` uma vez, troca ficha; detalhado não repete | `api_client.gd:185-187`; `materialize()` guarda `lod != 2`; teste 3 | PASS |
| 4 | 404/5xx/fora do contrato avisam e mantêm ficha; resposta tardia descartada | guarda `id != npc_id`; ramos 404/else emitem `warning`; `app.gd:350` → `toasts.push`; testes 4-7 | PASS |
| 5 | stress:500 ±3 FPS | sem janela nova nem `_process` (grep vazio em panels.gd); não medido aqui | não reproduzido |

## Mutações (todas revertidas com `git checkout --`)

| Mutação | Teste que reprova por assert |
|---|---|
| botão `visible = true` (lod 0 e 1) | t2 "detalhado/arquivado: sem botão esperado false" |
| botão `visible = lod >= 1` (só lod 1) | t2 "arquivado: sem botão" |
| botão `visible = false` com lod 2 | t1 "lod 2 oferece 'Ver em detalhe'", t4 |
| aviso `pooled_note.visible = false` | t1 `esperado true, veio false` |
| aviso sempre visível / texto sem "anônimo" | t3 (`pooled_note` false) / t1 check do texto |
| POST no `show_npc` (GET materializando) | t1 calls `["GET /npcs/9","POST ..."]`, t3 (t6 trava: runner morto em 90 s) |
| rota `/materialise` | t3, t4, t7 |
| método GET no lugar de POST | t3, t7 (t4 só SCRIPT ERROR, ver gap) |
| não chama `_present` no sucesso | t3 título "Ana de Verdade", t7 |
| guarda `lod != 2` removida | t3 "já detalhado: não repete esperado 1, veio 2" |
| 404 sem aviso | t4 (check "não existe mais") |
| 5xx sem aviso | t5 `esperado 1, veio 0`, t7 toast `[]` |
| mensagens 404/5xx trocadas | t4 (dois checks), t7 |
| shape `"npc-inspect"` → `""` | t5 "resposta fora do contrato vira aviso" + "não troca a ficha" |
| `_present` mesmo sem `ok` (ficha trocada no erro) | t5 |
| `_title.text = "?"` no ramo de erro | t4, t5 |
| guarda `id != npc_id` removida | t6 `esperado 5, veio 9` |
| `warning` não ligado ao toast no app | t7 "o aviso chega ao toast: []" |
| botão não ligado (`pressed` → pass) | t3, t7 |
| escrita extra (`POST /npcs/9/watchlist`) | t3 lista de chamadas |
| servidor devolve `{npc: ...}` no POST | `ApiContractTests.Npc_inspection_shape_...` Failed |
| POST do ApiContractTests em `/materialise` | mesmo teste: `Expected: OK, Actual: NotFound` |
| `shape.npc-inspect.json` `ageYears` → string | mesmo teste Failed |

## Padrões e critérios comuns da issue

- HTTP só em `Contract/api_client.gd` (`materialize_npc`, linha 185); cena não chama `transport`. PASS
- Shape: reusa `shape.npc-inspect.json` (inalterado no diff), validado no cliente via `_checked` e no .NET pelo POST. PASS
- Textos em português; aviso via `toasts.gd` (`app.gd:350`). Sem requisição por frame (sem `_process`). PASS
- Comum 5: grep em `api_client.gd` encontra todas as rotas da issue: `/conversations/start|send|end` (170/175/180),
  `/narratives/biographies|chronicles|reports` (91/95/99), `/history/beliefs` (104), `/players/.../incarnate|action|logout`
  (110/116/121), `/simulation/status` (38), `/admin/world/pause|resume|speed` (133/137/141), `/admin/search` (145),
  `/admin/audit` (149), `/cities/.../treasury` (155), `/workplaces/.../trades` (159), `/cultures/.../technologies` (164),
  `/npcs/.../materialize` (186), `/world/calendar` (50). PASS
- `.specs/features/godot-main-menu/plan.md`: cabeçalho e "Out of scope" atualizados; os 9 slugs citados existem em `.specs/features/`. PASS
- Nenhuma rota de servidor nova (diff não toca `src/`). PASS

## Gaps / observações (não bloqueiam)

1. t4 acessa `warnings[0]`/`warnings[1]` sem antes afirmar `warnings.size() == 2`: com a mutação "5xx sem aviso"
   (ou GET no lugar de POST) o t4 só dá SCRIPT ERROR e o runner imprime `ok`; a mutação é pega por assert em t5/t7
   e o `client-test.sh` reprova por SCRIPT ERROR, mas t4 sozinho não é discriminante.
2. A issue fala "na lista de NPCs"; o botão está na ficha (o diretório não informa `lod`), registrado em Out of scope do plano.
3. FPS `stress:500` e README § Desempenho não medidos/atualizados (sem janela nova): "não reproduzido".
4. `bash scripts/*.sh` recusado pelo harness do Verifier; gates rodados pelo comando equivalente.
