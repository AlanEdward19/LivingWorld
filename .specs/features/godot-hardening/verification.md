# Verification: godot-hardening (rodada 3) [Veredito: PASS]

- Verifier independente (contexto em branco, não escreveu o código), 2026-10-15. HEAD `a9f7d47` (detached em `feat/godot-hardening`),
  diff `feat/godot-materialize...HEAD` (16 arquivos).
- **Já tinham PASS nas rodadas 1 e 2 deste mesmo código:** FPS (critério 6; reproduzido por outro verificador, todas as telas
  dentro de ±1,7 FPS da base 75,5) e os critérios 1, 2, 3 (altura mínima) e 5. Nesta rodada só uma mutação de reconferência cada.
- Defeito aberto da rodada 2 (critério 4): mutante "`Run` monta o contexto com `DecisionContextBuilder.Build(...)` direto" sobrevivia.
  Correção: teste `The_real_sandbox_path_decides_with_the_engines_provision_target`.
- API não foi subida. Toda mutação revertida com `git checkout -- <arquivo>`; `git status` limpo ao fim.

## Critério 4 (sandbox com a meta de provisão do motor) - PASS

Mutações em `src/LivingWorld.Simulation/Behavior/Decision/NpcDecisionSandbox.cs`, rodando
`dotnet test tests/LivingWorld.Tests.Integration --filter FullyQualifiedName~DecisionSandboxEndpointTests` (base: 13/13 verdes).

| Mutante | Resultado | Teste que reprova (assert) |
| --- | --- | --- |
| M4a: `Run` usa `DecisionContextBuilder.Build(world, npc, tick)` direto (sem meta) | morto, 12/13 | `The_real_sandbox_path_...`: `Assert.Equal() Failure: Strings differ` (rastro) |
| M4b: `BuildContext` sem `provisionTargetDays` | morto, 11/13 | `Sandbox_context_uses_the_same_provision_target...`: `Assert.All` 20/20 `Values differ`; `The_real_...`: assert da premissa |
| M4c: `Run` ignora `BuildContext` e usa meta fixa 4.0 | morto, 12/13 | `The_real_sandbox_path_...`: `Strings differ` |

Premissa não vácua: sonda temporária (assert trocado para imprimir a contagem, revertida) deu
`differing=20 of 20` - com despensa de 1 dia de comida e água por membro e 1000 de dinheiro, a meta do motor muda o rastro de
**todos** os 20 NPCs vivos com casa em relação à meta padrão 2.0. Sob M4b a premissa cai a 0 e o assert de premissa reprova, ou
seja, o teste não passa por construção.

## Reconferência 1, 2, 3, 5 (uma mutação cada) - PASS

| Crit. | Mutação | Resultado |
| --- | --- | --- |
| 1 | remove `OS.add_logger(script_errors)` em `tests/run.gd`, com teste temporário `test_zz_probe.gd` (`null.foo()`) | sem mutação: `FAIL ... SCRIPT ERROR: Invalid call...`, exit 1; com mutação: `ok`, exit 0 (o defeito volta). Morto. Teste temporário apagado. |
| 2 | remove `_exclusive(brain_panel)` em `open_brain` | `test_every_entry_closes_the_window_that_was_open...` FAIL: "brain depois de biografia/cronica/crencas/tesouro/vendas/tecnologias/console: só uma aberta esperado 1, veio 2". Morto. |
| 3 | remove `size_windows()` de `start` | `test_windows_keep_a_minimum_height...` FAIL: 5x "altura mínima". Morto. |
| 5 | GET `/npcs/{id}` chama `MaterializeAndInspect` (Program.cs) | `PooledNpcContractTests` FAIL: `Assert.Equal() Expected: 2 Actual: 0` (lod). Morto. |

## Gates (código limpo)

| Gate | Resultado |
| --- | --- |
| `client-test.sh` (harness recusou o script com redirecionamento; rodado o equivalente: `Godot --headless --path client/godot --script res://tests/run.gd` após `--import`, e grep de `SCRIPT ERROR\|Parse Error`) | exit 0, `224 passed, 0 failed`, 0 ocorrências de `SCRIPT ERROR`/`Parse Error` |
| `bash scripts/verify.sh` | `verify: OK` (Unit 2193 ok, 3 skip, 0 falhas) |
| `dotnet test tests/LivingWorld.Tests.Integration` (suíte inteira) | 406/406 passed, 0 failed (1 m 23 s) |

## Observações (não bloqueiam)

- `The_real_sandbox_path_...` muta o mundo do `IClassFixture` compartilhado (esvazia despensas, credita carteiras). A classe e a
  suíte inteira passaram, mas é dependência de ordem latente para testes futuros da mesma classe.
- Saída do Godot ao fim: `ERROR: 1 RID allocations ... leaked at exit` (vazamento de fonte na saída do motor; não é `SCRIPT ERROR`,
  não afeta o gate).

## Veredito

PASS. O defeito aberto do critério 4 está fechado: os três mutantes do sandbox morrem por assert e a premissa do teste é real
(20/20). Critérios 1, 2, 3 e 5 reconfirmados; 6 (FPS) herdado das rodadas 1 e 2. Nenhum defeito novo.

---

# Rodada 2c (PR #89, segunda leva) [Veredito: PASS]

- Verifier independente, 2026-10-15, HEAD `a59b369` (detached em `feat/godot-hardening`). A rodada 2b (HEAD `a28fc4f`) confirmou
  D1-D3 e os gates e reprovou por 2 itens; aqui só eles e a documentação dos testes removidos. API não subida; FPS herdado
  (mesma carga de render). Toda mutação revertida com `git checkout --`.

| Item | Mutação / sonda | Resultado |
| --- | --- | --- |
| 1a | `_offer_release` sem `logout_button.text = "Sair do personagem anterior"` | `test_a_409_for_a_leftover_session_...` FAIL por assert: "o botão diz de qual personagem se sai esperado Sair do personagem anterior, veio Sair do personagem". Morto. |
| 1b | `logout()` (linha 137) sem voltar a `"Sair do personagem"` ao liberar | mesmo teste FAIL: "e volta ao rótulo normal ao liberar esperado Sair do personagem, veio Sair do personagem anterior". Morto. |
| 2 premissa | `Assert.Fail` temporário imprimindo `seen` em `Filled_optional_fields_...` | `SEEN=actionTarget,currentAction,employer,household,rest,spouse`. Premissa real. |
| 2a | `[property: JsonConverter(JsonStringEnumConverter)]` em `CurrentAction` (`NpcInspectionDto.cs`) | FAIL por assert: `NPC 0: $.currentAction: esperado number`. Morto. |
| 2b | `Household` vira `object?` e a projeção (`NpcInspectionQuery.cs:73`) passa `npc.Household?.Value` (número cru) | FAIL por assert: `NPC 0: $.household: esperado objeto`. Morto. |
| 3 | `^func test_` em `client/godot/tests`, `55699c5` vs HEAD (224 -> 233) | Saíram 6 nomes: os 2 documentados em `plan.md` ("Testes removidos de propósito"), os 3 substituídos citados, e `test_economy::test_resource_names_come_from_the_period_catalog_when_the_period_is_known`, que foi **renomeado** (commit `79db197`) para `test_resource_names_come_from_the_world_info_period_and_the_catalog` com as mesmas asserções mais a do período vindo de `/world/info`. Nenhuma cobertura perdida. |

## Gates (código limpo)

| Gate | Resultado |
| --- | --- |
| `client-test.sh` (harness recusou o script; equivalente: `Godot --headless --path client/godot --script res://tests/run.gd` após `--import`) | exit 0, `233 passed, 0 failed`, 0 `SCRIPT ERROR`/`Parse Error` |
| `verify.sh` (harness recusou; rodados os 4 passos dele: `check-docs`, `build`, `lint`, `test`) | todos exit 0; Unit 2197 ok, 3 skip, 0 falhas |
| `dotnet test tests/LivingWorld.Tests.Integration` (inteira) | 412/412 passed (1 m 20 s) |

## Observações (não bloqueiam)

- O teste de opcionais não vê `food`, `motherId`, `fatherId` preenchidos em 72 h nos 40 primeiros NPCs vivos; esses três
  opcionais seguem sem prova de forma com valor real (só o tipo declarado no contrato).
- `plan.md` não cita o rename do teste de nomes de recurso (item 3); é rename equivalente, não remoção.

Veredito 2c: PASS. Os dois itens reprovados na 2b estão fechados (4 mutantes mortos por assert, premissa real); gates verdes.

## Rodadas 2 e 2b (resumo)
- Rodada 2 (HEAD 28a4f6a): PASS em 7, 8, 10, 13 e FPS (base 77,4; cérebro 76,5; jogador 77,1; console 77,0; tecnologias 77,5); FAIL em 9, 11, 12 e na cobertura. Achados: guard de `incarnate` pendurava o runner, botão de tecnologias sem teste para anônimo, `Optional()` .NET não conferia tipo, 10 testes apagados por engano.
- Rodada 2b (HEAD a28fc4f): D1 a D3 corrigidos (vigia do runner, teste do botão, `Optional` estrito) e os 10 testes restaurados; sobraram o rótulo de sair e o teste removido do menu, fechados na 2c.
