## O que e por quê

<!-- O que muda e por quê? Ligue a issue: "Closes #123". -->

## Como testar

<!-- Passos que quem revisa pode seguir (cenário, seed, comando). -->

## Desempenho

<!-- Motor: `bash scripts/compare-simulation-cost.sh` antes/depois. Cliente: `bash scripts/client-stress.sh`. Ou "não se aplica". -->

## Checklist

- [ ] `bash scripts/verify.sh` sai 0 (e `bash scripts/client-test.sh`, se tocou `client/godot/`)
- [ ] Testes novos ou atualizados para o comportamento alterado
- [ ] Determinismo preservado (sem `Random`/`DateTime.Now`/GUID na simulação); goldens só regravados de propósito, com AD/ADR
- [ ] Sistema novo tem consumidor causal real (`rules/living-world-cohesion.md`)
- [ ] Docs atualizados, se necessário (`.md` ≤ 100 linhas)
- [ ] Sem segredos ou dados reais
