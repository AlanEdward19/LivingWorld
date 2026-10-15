# Verificação independente — issue #66 (ordem das fases, ADR-0025, plano lean Fase 21)

Verifier: contexto em branco; diff `feat/roadmap-state-sync...HEAD` (1d64fb2). Doc-only.

| # | Critério | Resultado | Evidência |
|---|---|---|---|
| 1 | Comentário na issue com proposta | PENDENTE-DO-AUTOR | Autor postará depois; decisão do usuário veio em chat. Não é FAIL. |
| 2 | STATE/ROADMAP refletem a decisão; check-docs verde | PASS | ROADMAP linhas 21/22/23 `ativa`/`próxima`, 18/27/17/19/20 `adiada` com motivo, regra "Ordem decidida (2026-10-14, #66)"; STATE "Decisão de escopo" + próxima unidade 21a→21b→21c. `bash scripts/check-docs.sh` = OK. ROADMAP.md 87 linhas, STATE.md 65. "O número é identidade, não ordem" intacto (contexto da tabela não alterado). |
| 3 | ADR da Fase 14 existe; "aceito" só com aprovação | PASS | `docs/adr/ADR-0025-...md` (27 linhas), Status aceito, decisor Alan/#66; usuário aprovou em chat (informado no briefing). Cobre o que o Godot cobre e o que não (voz, Unreal). Índice `docs/adr/README.md` atualizado. `phase-14-unreal.md` mantido, só banner de status. |
| 4 | plan.md lean passa no checklist de `rules/eval-criteria.md` | PASS | Ver abaixo. Issues #70, #71, #72 existem, OPEN, sem assignee (`gh issue view`), uma por fatia 21a/21b/21c; #70 aponta critérios 1,2,3,10 e traz regras da série. |
| 5 | Nenhum spec/design apagado | PASS | `git diff --name-status`: apenas A/M (plan.md, ADR-0025 novos; ROADMAP, STATE, adr/README, phase-14/15/21 modificados). Sem D/R. `spec.md/design.md/tasks.md/closeout.md` da fase 21 permanecem. |
| 6 | Fora de escopo: status alheios não mudaram | PASS | Diff do ROADMAP toca só linhas 14, 15, 17-23, 27 + regra nova; 16, 24-26, 28 intactas. Mudança 15 "fechada" e 14 "substituída" autorizadas pelo usuário. |
| 7 | `bash scripts/verify.sh` | PASS | "verify: OK"; testes unit 2193 passed, 0 failed, 3 skipped. |
| 8 | Mutação: check-docs reprova doc >100 linhas | PASS | +20 linhas em ROADMAP.md e +30 no ADR: `check-docs: ROADMAP.md tem 107 linhas (teto 100)`, exit 1. Restaurado com `git checkout`; check-docs volta a 0; worktree limpa. |

## Checklist do plan.md (fim de rules/eval-criteria.md)
- Cada item pode falhar: PASS. Todos têm condição observável (falha nomeando ActionType, ordem estrita, byte-idêntico, mutação nº1).
- Sem literal sem procedência: PASS. Limiares vêm do cenário `test-ontogeny`; só contagens 18/20 e 20/20 (permitidas por R4). Os 103%/102% são do sensor de escala exigido pela issue; "6 eixos" é estrutura do design, não limiar.
- Causal tem controle: PASS. Crit. 4 (cuidador ausente), 5 (prazo/tardia/nenhuma), 6 (gene vs exposição), 7 (órfão com cuidador de outro idioma) são braços pareados na mesma seed, ≥20 seeds.
- Nada humano: PASS. Reflexão, asserts por tick, hash.
- Horizonte ≤ 10 anos: PASS. Gate declara ≤ 10 anos; nenhum critério de 100 anos.
- O que o tipo garante ficou de fora: PASS. Crit. 1 usa reflexão para cobertura, sem testar tipo.
- Determinismo: crit. 10 + crit. 3 (desligar muda hash) seguem a regra; mutação de fronteira no crit. 1.

## Observações (não bloqueiam)
- Regra de ROADMAP "Status `spec` ... nenhuma começa antes da Fase 8" segue literal; coerente com a nova linha de ordem.
- O comentário da issue (critério 1) deve ser postado antes de fechar #66.

## Veredito
PASS (critério 1 pendente do autor). Sem FAIL.
