# Verificação 21c — rodada 2 (#72)

Verifier independente; commit 80007e1. Todas as mutações foram restauradas (`git checkout -- <arquivo>`).
Comando base: `bash scripts/test.sh --filter "FullyQualifiedName~Ontogeny"` (33 testes no baseline).

| Item | Resultado | Evidência |
|---|---|---|
| (a) FAIL 1: mutação `.Max() * 0` no piso | PASS | `Failed: 1, Passed: 32` — reprova `A_consolidated_milestone_is_a_floor_and_a_recent_one_is_lost` |
| (a2) piso `<=` -> `<` em `WindowLimitYears` | SOBREVIVE (fraco) | 33/33 passam; borda exata idade == limite não testada |
| (a3) removido `progress + 1e-9 >= m.Threshold` (`&& true`) | SOBREVIVE (fraco) | 33/33 passam |
| (a4) removido `WindowLimitYears <= currentAgeYears` (`true`) | SOBREVIVE (fraco) | 33/33 passam; piso sem checar janela fechada não é pego |
| (b) FAIL 2: duplicata no doc | PASS | `grep -c "Ontogeny.RegressionLoss" docs/domain/period-authoring-schema.md` = 1 |
| (c1) materialização real | PASS | Mutação: `SetDevelopment(Newborn)` após `AddNpc` em `MaterializeOne` reprova `Everyone_materialized_from_the_aggregate_pool_...` (o teste materializa 10 NPCs por seed via `.Value!`) |
| (c2) tolerância de ponto flutuante | PASS | `Full = 1.0` em `DevelopmentState` reprova `Full_development_tolerates_...` e `The_language_is_kept_after_development_completes_...`. A mutação em `AdvanceOneMonth` não foi testada |
| (d) check-docs / lint | PASS | `check-docs: OK`; `lint.sh` rc=0 sem saída |
| (e) `verify.sh --full` | PASS | log com `verify: OK`; Unit 2227 passed/3 skipped, Unit(2) 18/1 skipped, Integration 384, LongRunning 128/8 skipped, 0 falhas |

## Veredito: APROVADO COM RESSALVAS

Os dois FAILs da rodada 1 foram corrigidos e os pontos fracos (materialização e borda de conclusão) agora discriminam.
Ressalva (não bloqueante): o piso da regressão só é protegido contra a mutação `*0`. As mutações a2, a3 e a4 sobrevivem:
faltam testes (1) com marco não atingido e janela fechada (a3: sem a checagem, o piso passa a ficar acima do progresso e a doença não reduz nada),
(2) com marco atingido e janela ainda aberta (a4) e (3) com idade exatamente igual ao limite (a2).
