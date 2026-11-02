# Verificação — population-belief-gate

Verifier independente (não escreveu o código). Branch `fix/populacao-historia-catalogo`, diff `develop...HEAD`.

## Critérios

| # | Critério | Veredito | Evidência |
|---|---|---|---|
| 1 | Bônus +40 só com dinheiro e despensa abaixo da meta; sem dinheiro = 0; despensa cheia = 0 | PASS | `bash scripts/test.sh --filter Starvation_creates_oral_history` -> Passed 1/1. Asserções em `RuntimeHistoryTests.cs` (withMoney=40, broke=0, stocked=0). Gate em `BehaviorDecisionSystem.Utility.cs` (`CanActOnScarcityBelief`). |
| 2 | Crença ainda decide a ação de quem pode comprar | PASS | `bash scripts/test.sh --project integration --filter Scarcity_belief_diverges` -> Passed 1/1. |
| 3 | População ao vivo ano 5 >= 90% do baseline, sem regravar | PASS | `bash scripts/test.sh --project longrunning --filter Live_world_population` -> Passed 1/1 (30 s). `git diff develop...HEAD --stat -- tests/baselines` vazio: baseline intocado. |
| 3b | Em develop limpo o teste falha | PASS | Worktree em /private/tmp (develop 6799bf3): mesmo comando -> `Failed: 1` em `EconomyDepthConservationTests.Live_world_population_stays_within_90_percent_of_the_baseline`. Worktree removido. |
| 4 | Mutação: sem o gate, 1 e 3 reprovam | PASS | Removido `CanActOnScarcityBelief(...) &&`: teste 1 -> Failed (Expected 0, Actual 40); teste 3 -> Failed. Restaurado via `git checkout -- <arquivo>`; `git status` limpo. |

## Os testes da #91 foram enfraquecidos?

Não além do necessário. A asserção de divergência foi preservada:
- `DecisionContextIntegrationTests`: `Assert.Equal(Eat, plain)`, `Assert.Equal(Buy, withBelief)`, `Assert.NotEqual` e o `Assert.Equal(Buy, scored.Action)` seguem intactos. Mudou só o cenário (carteira 100, estoque 5 -> 1, fome 20 -> 14) para o NPC ser alguém que pode agir.
- `RuntimeHistoryTests`: a asserção original de +40 foi mantida, agora com carteira e despensa vazia; foram acrescentados os casos 0 (sem dinheiro, despensa cheia).

## Gates

- `bash scripts/verify.sh`: iniciado; no fechamento deste relatório ainda em execução (build ok, 0 warnings/0 errors). Resultado final NÃO observado pelo Verifier. PENDENTE.
- `bash scripts/verify.sh --full`: não executado (tempo).

## Veredito

PASS nos critérios 1-4. Gate `verify.sh` pendente de confirmação pelo chamador.
