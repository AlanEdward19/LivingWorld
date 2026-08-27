# Diagnóstico — execução paralela (2026-08-25 19:45)

## Problemas encontrados

### 1. Lock de teste travado (CRÍTICO — corrigido)
`locks.json` tinha `testLock.holder = phase-16-2-worker-p1` desde 22:37 sem release.
**Efeito:** qualquer agent que respeita o protocolo ficava bloqueado esperando lock.
**Ação:** lock liberado nos 3 worktrees.

### 2. Todos os orchestrators usaram o workspace errado
Os prompts iniciais apontavam `LivingWorld` (primary) para **todas** as fases.
- Orchestrator 16.3 fez `git checkout feat/phase-16-3-world-realism` **no primary** → conflito de branch
- Commit `a131ecc` (16.3) caiu na branch `feat/phase-16-2-power-evolution`
- Workers P2–P7 da 16.3 rodaram no primary misturando Fauna/Flora/Combat com 16.2

### 3. Branches poluídas no primary (16.2)
```
48b8361 Revert trilha-c (no lugar errado)
a131ecc feat(phase-16-3) ← commit da 16.3 na branch 16.2
2037356 feat(api) trilha-c ← revertido depois
```

### 4. Worktrees sub-utilizados
| Worktree | Branch | Estado real |
| --- | --- | --- |
| `LivingWorld` | 16.2 | Mistura 16.2 + 16.3 + lixo de build |
| `LivingWorld-16-3` | 16.3 | Tem 62a2e13 + cópia parcial; falta work dos workers |
| `LivingWorld-trilha-c` | Trilha C | ce394cd (T1) OK; T2 uncommitted |

### 5. Progress files desatualizados
16.2 progress mostra tudo pending, mas há `PowerEvolutionStage` uncommitted.
16.3 progress não reflete workers P2–P7 rodando no primary.

## Estado por trilha (honesto)

| Trilha | Feito | Onde está | Próximo passo |
| --- | --- | --- | --- |
| **16.2** | T1 parcial (uncommitted) | primary | Commit T1–T2 só no primary |
| **16.3** | T1–T2 committed (2x: a131ecc + 62a2e13) | primary + 16-3 WT | Consolidar no 16-3 WT; P2+ uncommitted no primary |
| **Trilha C** | T1 committed (ce394cd) | trilha-c WT | Commit T2 no trilha-c WT |

## Ações recomendadas (manual ou com aprovação)

1. **Pausar/restartar orchestrators** com WORKTREE_PATH explícito (ver README)
2. **Não fazer stash** — specs agent continua no primary
3. **Consolidar 16.3:** copiar work uncommitted do primary → `LivingWorld-16-3`, commit lá
4. **Limpar primary:** reverter arquivos 16.3 no primary (checkout 4c0919b por path) — requer aprovação
5. **Trilha C:** commit T2 em `LivingWorld-trilha-c` e continuar de lá

## Regra nova para agents

```
NUNCA git checkout outra branch no LivingWorld primary.
SEMPRE cd para o WORKTREE_PATH da fase antes de editar código.
```
