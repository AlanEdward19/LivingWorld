# Execução paralela — Fases 16.2, 16.3, Trilha C

> Infraestrutura efêmera para orquestradores. Apague após merge das três fases.

## Worktrees (sem stash — cada agent usa SEU diretório)

| Fase | Diretório | Branch | Orchestrator |
| --- | --- | --- | --- |
| **16.2** | `C:\Users\Alan-\Desktop\Projetos\Pessoal\LivingWorld` | `feat/phase-16-2-power-evolution` | `orchestrator-phase-16-2` |
| **16.3** | `C:\Users\Alan-\Desktop\Projetos\Pessoal\LivingWorld-16-3` | `feat/phase-16-3-world-realism` | `orchestrator-phase-16-3` |
| **Trilha C** | `C:\Users\Alan-\Desktop\Projetos\Pessoal\LivingWorld-trilha-c` | `feat/trilha-c-dwarf-fortress-worldgen` | `orchestrator-trilha-c` |

**Regra:** implementação de código só no worktree da fase. Specs em edição no
`LivingWorld` principal — copie manualmente para o worktree correspondente quando
estabilizar (ou abra o Cursor no worktree certo).

Locks (`locks.json`) ficam sincronizados manualmente entre os três — adquira lock
no arquivo do worktree onde você está rodando o teste.

## Regras de teste (AD-009)

| Quem | Pode rodar | Proibido |
| --- | --- | --- |
| Workers/orchestrators | Gate **quick** com `--filter` do escopo da fase | `bash scripts/test.sh` sem filtro, `bash scripts/verify.sh` |
| Usuário | Gate completo + `verify.sh` | — |

**Lock**: antes de `dotnet test` ou `npm test`, adquira lock em `locks.json`. Libere ao terminar.

## Arquivos compartilhados (serializar edição)

| Arquivo | Donos | Regra |
| --- | --- | --- |
| `WorldEventKind.cs` | 16.2 (`PowerInherited`), Trilha C (+3 kinds) | Um de cada vez; merge no coordenador |
| `tests/.../Extraordinary/*` | 16.2 + 16.3 | Filtros distintos; commits atômicos por task |
| `WorldState.cs` | 16.3 (possível índice) | 16.2 não toca |

## Progresso

Cada orchestrator atualiza `{phase}-progress.md` no worktree da fase.
