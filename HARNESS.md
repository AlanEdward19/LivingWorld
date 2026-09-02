# HARNESS.md — Manifesto de Harness (Living World)

> Índice do harness **já existente** neste repo — não é greenfield. Atualize sempre que
> adicionar/alterar um controle ou aplicar uma decisão de steering. Não duplica `rules/*.md`
> nem `docs/*`: referencia por caminho.

## 1. Harnessability
- **Tipagem**: C#/.NET 10, `Nullable` + `TreatWarningsAsErrors` (`Directory.Build.props`) —
  type-check é sensor grátis embutido no `build.sh`.
- **Fronteiras de módulo**: claras (`Domain` puro → `Simulation` → `AI`/`Infrastructure` →
  `Api`/`Workers`), reforçadas por `tests/LivingWorld.Tests.Unit/ArchitectureTests.cs`.
- **Convenções fortes**: xUnit, EF Core (migração versionada), entrada única por
  `scripts/*.sh` — baixa superfície para o agente inventar comando novo.
- **Situação**: legado relativo (Fases 0–4 fechadas, ~37 ADs, 15 ADRs). Fase 5 (Economia) é
  a próxima unidade — este documento existe para habilitar spec-driven + loop autônomo nela.

## 2. Os 4 componentes neste repo

### Guias (feedforward)
- `AGENTS.md` — índice/roteador, boundaries, progressive loading.
- `rules/*.md` sob demanda: `implementation.md`, `tests.md`, `simulation-determinism.md`,
  `llm-boundary.md`, `database-entities.md`, `eval-criteria.md`.
- `ROADMAP.md` + `docs/roadmap/phase-NN-*.md` — objetivo, tasks e critérios por fase.
- `docs/adr/*.md` (arquitetura) e `docs/decisions-log.md` (AD-NNN, processo/escopo).
- `.specs/features/<feature>/{spec,design}.md` — guia funcional do `tlc-spec-driven` quando a
  feature passa por spec-driven formal (ex.: `phase-04-needs`).

### Sensores (feedback)
- `scripts/verify.sh` — gate único: `check-docs` + `build` + `lint` + `test`.
- `scripts/verify-mutation.sh` — prova que o gate reprova de verdade (3 mutantes: `Random`
  em `Domain`, assert invertido, `.md` de 200 linhas); caro, roda manual.
- `Directory.Build.props` + `BannedSymbols.txt` +
  `tests/LivingWorld.Tests.Unit/BannedApiAnalyzerTests.cs` — `Random`/`DateTime.Now`/`Guid.NewGuid()`
  em `Domain`/`Simulation` é **erro de compilação**, não convenção.
- `tests/LivingWorld.Tests.LongRunning/GoldenHashesTests.cs` +
  `tests/LivingWorld.Tests.Integration/Behavior/UtilityAiHashScenarioTests.cs` — hash de mundo
  versionado; desligar um sistema tem de mudar o hash.
- `WorldSnapshotTests.cs` (Unit) — reflexão sobre os campos de `WorldState`: campo não
  classificado canônico/volátil reprova.
- `tests/LivingWorld.Tests.LongRunning/Serialization/ReferentialIntegritySweepTests.cs` +
  `src/LivingWorld.Simulation/ReferentialIntegritySweep.cs` — sweep genérico por reflexão sobre
  todos os tipos de ID.
- `ArchitectureTests.cs`, `Geography/GeographyNamingArchitectureTests.cs` (em
  `tests/LivingWorld.Tests.Unit/`), `tests/LivingWorld.Tests.LongRunning/Population/PopulationArchitectureTests.cs`
  — fronteiras de camada, sem literal de apresentação no motor.
- `.specs/lessons.json`/`LESSONS.md` (auto-mantido por `scripts/lessons.py`) — lição
  confirmada vira guia; candidata fica em quarentena até corroborar em 2 features.

### Memória
- `STATE.md` — handoff único entre sessões: última unidade fechada, próxima, eval gates
  disponíveis, riscos com/sem mecanismo.
- `docs/decisions-log.md` (AD-NNN) e `docs/adr/ADR-NNNN-*.md` — por que cada decisão foi
  tomada; nunca duplicado em `STATE.md`.
- `ROADMAP.md` — status por fase (fechada/pendente/spec); fonte única, não replicada.
- Git: commit `feat(phase-NN): <resumo>` só com `verify.sh` em 0 (política em `AGENTS.md`);
  fases sob `tlc-spec-driven` formal commitam 1x por task (AD-032), granularidade de bisect.
- `.specs/features/<feature>/validation.md` — evidência da verificação independente
  (author != verifier) quando a feature passou por spec-driven.

### Bootstrap
Ordem de leitura numa sessão nova, do mais barato ao mais específico — nunca leia `docs/`
ou `src/` inteiros de largada:
`AGENTS.md` (boundaries + quick reference) → `STATE.md` (feito/próximo/riscos) →
`ROADMAP.md` só para achar `docs/roadmap/phase-NN-*.md` da fase atual →
`rules/<apontada pela Quick reference>` sob demanda → `docs/decisions-log.md`/`docs/adr/`
só se precisar do porquê, não do quê.

## 3. Controles por categoria

### Manutenibilidade
| Controle | Direção | Execução | Onde roda | Sinal p/ LLM? |
|---|---|---|---|---|
| `scripts/lint.sh` (`dotnet format`) | sensor | computacional | `verify.sh` | sim |
| `scripts/check-docs.sh` (teto 100 linhas/.md) | sensor | computacional | `verify.sh` | sim |
| `scripts/build.sh` (`-warnaserror`, `Nullable`) | sensor | computacional | `verify.sh` | sim |
| `rules/implementation.md` (unidade = 1 comportamento) | guia | inferencial | antes da tarefa | — |
| `.specs/LESSONS.md` (lição confirmada) | guia | inferencial | Specify/Design | — |

### Fitness de arquitetura
| Controle | Direção | Execução | Onde roda | Sinal p/ LLM? |
|---|---|---|---|---|
| `BannedApiAnalyzerTests.cs` + `BannedSymbols.txt` | sensor | computacional | `build.sh` (compilação) | sim |
| `ArchitectureTests.cs` / `PopulationArchitectureTests.cs` / `GeographyNamingArchitectureTests.cs` | sensor | computacional | `test.sh` | sim |
| `ReferentialIntegritySweepTests.cs` (por reflexão) | sensor | computacional | `test.sh` | sim |
| `WorldSnapshotTests.cs` (canônico/volátil por reflexão) | sensor | computacional | `test.sh` | sim |
| `rules/simulation-determinism.md`, `rules/database-entities.md` | guia | inferencial | antes da tarefa | — |

### Comportamento
| Controle | Direção | Execução | Onde roda | Sinal p/ LLM? |
|---|---|---|---|---|
| `docs/roadmap/phase-NN-*.md` (## Critérios de verificação) | guia | inferencial | antes da geração | — |
| `rules/eval-criteria.md` (R1–R5, checklist) | guia | inferencial | ao escrever critério | — |
| `GoldenHashesTests.cs` / `UtilityAiHashScenarioTests.cs` | sensor | computacional | `test.sh` | sim |
| Testes `Category=Scenario` (property-based, N anos) | sensor | computacional | manual/nightly | sim |
| `verify-mutation.sh` (o gate reprova de verdade) | sensor | computacional | manual | sim |
| `.specs/features/*/validation.md` (verificador independente) | sensor | inferencial | fim de feature spec-driven | sim |

## 4. Posicionamento no ciclo de vida ("keep quality left")
- **Antes de "pronto"**: `bash scripts/verify.sh` (exclui `Category=Scenario`).
- **Manual/caro**: `scripts/verify-mutation.sh`; `scripts/test.sh --filter Category=Scenario`.
- **Contínuo/drift**: `.specs/lessons.py` entre features; revisão de performance agendada ao
  fechar a Fase 8 (`ROADMAP.md`).

## 5. Loop de autocorreção
- Sensores que realimentam o agente automaticamente: `build.sh` (erro de compilação nomeia
  arquivo/linha), `lint.sh --fix` (autoaplica), `test.sh` (stack trace do teste que falhou).
- **Eval gates disponíveis para o loop-engineering**:
  - `bash scripts/verify.sh` — gate principal, saída 0 = pode concluir a tarefa.
  - `bash scripts/test.sh --filter Category=Scenario` — cenário longo, fora do gate de rotina.
  - `bash scripts/verify-mutation.sh` — valida o próprio gate; rodar só quando o gate mudar.
- Contrato implementador/validador desta fase (Economia) fica em `.specs/features/phase-05-*`
  quando a fase for aberta via `tlc-spec-driven` — este harness só garante que os gates acima
  existem e são 0/1.

## 6. Steering log
| Data | Problema recorrente | Controle criado/ajustado |
|---|---|---|
| | | |
