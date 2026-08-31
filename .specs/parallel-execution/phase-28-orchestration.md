# Fase 28 — Orquestração (regras obrigatórias)

## Regras do orquestrador

1. **Parallel-safe só onde o `tasks.md` marca `[P]`** — senão **1 worker**.
2. **1 worker = 1 worktree = 1 branch** — nunca dois workers no mesmo diretório.
3. **Ownership estrito** — worker só edita arquivos listados na task dele. **Jamais** o teste/código de outro.
4. **Testes** — cada worker roda **só o filtro da sua task** (`--filter` do `tasks.md`).
5. **Merge** — orquestrador junta branches na `feat/phase-28-cognition` (`LivingWorld-28`) após gate da task.
6. **`WorldState.cs` / `Program.cs`** — só o worker de **integração** (sequencial), nunca em paralelo.

## Worktrees

| Papel | Diretório | Branch |
|---|---|---|
| **Integração / merge** | `LivingWorld-28` | `feat/phase-28-cognition` |
| Worker T{N} | `LivingWorld-28-t{N}` | `feat/phase-28-t{N}` |

Base de cada worker: último commit da integração **antes** de abrir o worktree.

## Estado (2026-08-30)

| Item | Status |
|---|---|
| Phase 1 T1,T2,T3,T15 | ✅ mergeado em `feat/phase-28-cognition` @ `e98f00e` |
| Integração WorldState | ⏳ pendente (bloqueia T6,T7,T8) |
| Phase 2 T5 | ⏳ próximo batch paralelo |
| Phase 4 T16,T17 | ⏳ próximo batch paralelo (com T5) |

## Próximo batch (parallel-safe `[P]`)

```
LivingWorld-28-t5   → T5  (CosmeticDetailSystem)
LivingWorld-28-t16  → T16 (SnapshotStringInterning)
LivingWorld-28-t17  → T17 (EventLogKindEncoding)
```

Depois merge → integração WorldState (1 worker) → T6 ∥ T7 em worktrees separadas.

## Fases sequenciais (1 worker cada)

- Phase 2 cadeia: T10 → T11 → T12
- Phase 3 inteira: T4 → T8 → T13 → T9 → T14
- Phase 4 cadeia: T18 → T19 → T20
- Phase 5: T21 → T22
- Phase 6: T23
