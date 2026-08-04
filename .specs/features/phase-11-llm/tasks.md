# Fase 11 (Interacao com LLM) Tasks
## Execution Protocol (MANDATORY -- do not skip)
Implement these tasks com a skill `tlc-spec-driven` ativa (fluxo Execute completo). Se a skill falhar, parar.
---
**Design**: `.specs/features/phase-11-llm/design.md`  
**Status**: Implementado (T1-T10 + T9b + fixes de fechamento), aguardando decisão sobre Verifier
## Test Coverage Matrix
> Generated from codebase + guidelines: `AGENTS.md`, `rules/llm-boundary.md`, `scripts/test.sh`, `scripts/verify.sh`.
| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Domain/AI contracts (`LlmContext`, DTOs, rules) | unit | 1:1 com ACs de validação, recusa/aceite e compatibilidade | `tests/LivingWorld.Tests/Llm/*Tests.cs` | `bash scripts/test.sh --filter Category!=Scenario` |
| Simulation orchestration (`Conversation*`, validator, assembler) | integration | happy + edge + falha (provider down, DTO inválido, ação proibida) | `tests/LivingWorld.Tests/Llm/*Tests.cs` | `bash scripts/test.sh --filter Category!=Scenario` |
| API endpoints de conversa | integration | start/send/end com 200/4xx e rejeição social determinística | `tests/LivingWorld.Tests/Llm/*EndpointTests.cs` | `bash scripts/test.sh --filter Category!=Scenario` |
| Security gates (truth vs belief, injection, egress) | unit + arquitetura | reflexão cobrindo handlers + par de mutação obrigatório | `tests/LivingWorld.Tests/Llm/*Security*Tests.cs` | `bash scripts/verify.sh` |
| Job de compactação | integration | preserva canônico e IDs >= limiar | `tests/LivingWorld.Tests/Llm/*Compaction*Tests.cs` | `bash scripts/test.sh --filter Category!=Scenario` |
## Parallelism Assessment
> Generated from codebase — confirm before Execute.
| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| --- | --- | --- | --- |
| unit | Yes | cada teste instancia estado próprio | `BehaviorDecisionSystemTests.cs` |
| integration | Yes | `WebApplicationFactory`/`ScenarioRunner` por teste | `NpcEndpointTests.cs` |
| determinismo 2-processos | No | depende de comparação sequencial de dois processos | `DeterminismTwoProcessTests.cs` |
| arquitetura/reflexão | Yes | leitura de assembly compilado, sem estado mutável | `ArchitectureTests.cs` |

## Gate Check Commands
| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | task com unit/integration local | `bash scripts/test.sh --filter Category!=Scenario` |
| Full | task de segurança/fronteira/API | `bash scripts/verify.sh` |
| Build | fechamento de fase | `bash scripts/build.sh && bash scripts/lint.sh && bash scripts/test.sh --filter Category!=Scenario` |

## Execution Plan
### Phase 1 (Sequential)
`T1 -> T2 -> T3`
### Phase 2 (Parallel + merge)
`T3 -> { T4 [P], T5 [P] } -> T6`
### Phase 3 (Sequential)
`T6 -> T7 -> T8`
### Phase 4 (Sequential)
`T8 -> T9 -> T10`

## Task Breakdown
### T1: Criar `LlmRules` e política de disponibilidade social — ✅ done (1156d9a)
**What**: regra de aceite/recusa + compatibilidade com ação corrente (não parar automaticamente). **Where**: `src/LivingWorld.Domain/Llm/LlmRules.cs`, `src/LivingWorld.Simulation/Llm/ConversationAvailabilityPolicy.cs`. **Depends on**: None. **Reuses**: `BehaviorDecisionSystem`. **Requirement**: LLM-01, LLM-02. **Tests**: unit. **Gate**: Quick.

### T2: Criar sessão de conversa e ciclo start/send/end — ✅ done (8265ae8)
**What**: `ConversationSession`, store em memória canônica/volátil conforme design, expiração por scheduler. **Where**: `src/LivingWorld.Simulation/Llm/ConversationSession*.cs`. **Depends on**: T1. **Reuses**: `EventScheduler`, `TickContext`. **Requirement**: LLM-03. **Tests**: integration. **Gate**: Quick.

### T3: Expandir contrato de contexto LLM sem quebrar fronteira — ✅ done (d903a99)
**What**: ampliar `LlmContext`/DTO para crença, memória relevante, allowed actions e metadados de sessão. **Where**: `src/LivingWorld.AI/ILlmProvider.cs`, `FakeLlmProvider.cs`, `NullLlmProvider.cs`. **Depends on**: T2. **Reuses**: contrato `ILlmProvider`. **Requirement**: LLM-04, LLM-05. **Tests**: unit. **Gate**: Quick.

### T4 [P]: Implementar `NpcBeliefQuery` + `LlmContextAssembler` — ✅ done (337755c)
**What**: montar prompt apenas com crença/memória do NPC, nunca verdade global. **Where**: `src/LivingWorld.Simulation/History/NpcBeliefQuery.cs`, `src/LivingWorld.Simulation/Llm/LlmContextAssembler.cs`. **Depends on**: T3. **Requirement**: LLM-05, LLM-06. **Tests**: integration. **Gate**: Quick.

### T5 [P]: Implementar `LlmResponseValidator` — ✅ done (273d94a) — gap: sem log real de violação, só resultado tipado (sem infra de log ainda)
**What**: parse DTO -> schema -> `ProposedActions subset AllowedActions`; rejeição total + log + fallback trigger. **Where**: `src/LivingWorld.Simulation/Llm/LlmResponseValidator.cs`. **Depends on**: T3. **Requirement**: LLM-07, LLM-08. **Tests**: unit. **Gate**: Quick.

### T6: Implementar `ConversationOrchestrator` + efeitos válidos — ✅ done (b793009)
**What**: pipeline start/send/end, aplicação de efeitos permitidos, fallback determinístico sem fato canônico novo. **Where**: `src/LivingWorld.Simulation/Llm/ConversationOrchestrator.cs`, `ConversationEffectsApplier.cs`, `FallbackResponder.cs`. **Depends on**: T4, T5. **Requirement**: LLM-09, LLM-10, LLM-11. **Tests**: integration. **Gate**: Full.

### T7: Expor endpoints de conversa na API — ✅ done (276dbe9)
**What**: endpoints `POST /conversations/start|send|end` com respostas de aceite/recusa e erros esperados. **Where**: `src/LivingWorld.Api/Program.cs` (+ handlers). **Depends on**: T6. **Reuses**: padrão endpoint de `NpcInspectionQuery`. **Requirement**: LLM-01..03. **Tests**: integration. **Gate**: Full.

### T8: Segurança Truth vs Belief + corpus de injeção — ✅ done (5375949), corpus 22 entradas. Fix pós-hoc: f053296 (Simulation->AI violation corrigida)
**What**: testes por reflexão cobrindo handlers de jogo + corpus versionado `tests/fixtures/prompt-injection/*.json` com asserts objetivos e par de mutação. **Where**: `tests/LivingWorld.Tests/Llm/*Security*Tests.cs`, `tests/fixtures/prompt-injection/`. **Depends on**: T7. **Requirement**: LLM-12, LLM-14, LLM-15. **Tests**: unit + arquitetura. **Gate**: Full.

### T9: Bloqueio de egress de rede no gate — ✅ done (9fc1492)

### T9b: Modelo de memória do NPC + `Recall` ponderado (gap descoberto na execução — roadmap itens 1-2, não existia task própria) — ✅ done (de1d03f). Baseline de hash canônico regravado em 3a3548d (mudança legítima, ADR-0014). Gap remanescente: ConversationEffectsApplier (T6) ainda usa lista efêmera própria, não o NpcMemory novo — flag pra fechamento.
**What**: memória por NPC nas 5 categorias (operacional/episódica/semântica/social/cultural), cada registro com importância (0-100), tick de origem, participantes e local. `Recall(npc, query, n)` pontua por importância + recência + relevância (pesos declarados em `LlmRules`, T1) e desempata por ID de memória — determinístico entre execuções do mesmo mundo semeado. Split canônico/volátil (ADR-0014, padrão já usado em `WorldState.cs` com `[Canonical]`/`[Volatile]`): registros com importância >= limiar do cenário são canônicos (entram no hash canônico); abaixo do limiar são voláteis (compactáveis livremente, fora do hash canônico) — é essa separação que torna T10 possível sem tocar hash canônico. Retrofit: `LlmContextAssembler` (T4, já commitado) hoje sempre manda `RelevantMemories: null` — passa a chamar `Recall` de verdade. **Where**: modelo/armazenamento em `src/LivingWorld.Domain/Llm/` ou local que fizer sentido junto ao resto de memória do domínio (decidir olhando padrão de `Fact`/`Relationship` em `WorldState.cs`), `Recall` em `src/LivingWorld.Simulation/Llm/` ou `History/`, retrofit em `src/LivingWorld.Simulation/Llm/LlmContextAssembler.cs`. **Requirement**: LLM-04..06 (spec.md story "Contexto por crença e memória do NPC"). **Tests**: unit + integration — `Recall(npc, query, 5)` devolve as mesmas 5 memórias, mesma ordem, em duas execuções do mesmo mundo semeado (critério de verificação do spec). **Gate**: full (filtrado).

### T10: Job de compactação de memória — ✅ done (aa2db6b)
**What**: compactar baixa importância em batch, preservar IDs >= limiar e hash canônico. Depende do modelo real de memória criado em T9b (não do que T4 tinha antes). **Where**: `src/LivingWorld.Simulation/Llm/MemoryCompactionJob.cs`, `src/LivingWorld.Workers/Program.cs` (wiring). **Depends on**: T9b. **Requirement**: LLM-16, LLM-17, LLM-18, LLM-19. **Tests**: integration. **Gate**: Full.

## Diagram-Definition Cross-Check
| Task | Depends On (body) | Diagram | Status |
| --- | --- | --- | --- |
| T1 | None | root | ✅ |
| T2 | T1 | T1->T2 | ✅ |
| T3 | T2 | T2->T3 | ✅ |
| T4 | T3 | T3->{T4,T5} | ✅ |
| T5 | T3 | T3->{T4,T5} | ✅ |
| T6 | T4,T5 | {T4,T5}->T6 | ✅ |
| T7 | T6 | T6->T7 | ✅ |
| T8 | T7 | T7->T8 | ✅ |
| T9 | T8 | T8->T9 | ✅ |
| T10 | T9 | T9->T10 | ✅ |

## Test Co-location Validation
| Task | Code Layer | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1 | Domain/Policy | unit | unit | ✅ |
| T2 | Simulation session | integration | integration | ✅ |
| T3 | AI contract | unit | unit | ✅ |
| T4 | Context assembly | integration | integration | ✅ |
| T5 | Validator | unit | unit | ✅ |
| T6 | Orchestrator | integration | integration | ✅ |
| T7 | API endpoints | integration | integration | ✅ |
| T8 | Security gates | unit+arquitetura | unit+arquitetura | ✅ |
| T9 | Network guard | integration | integration | ✅ |
| T10 | Compaction job | integration | integration | ✅ |
