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
