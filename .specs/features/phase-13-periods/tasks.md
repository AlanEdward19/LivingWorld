# Fase 13 (Multiplos periodos) Tasks
## Execution Protocol (MANDATORY -- do not skip)
Implement these tasks com a skill `tlc-spec-driven` ativa (fluxo Execute completo). Se a skill falhar, parar.
---
**Design**: `.specs/features/phase-13-periods/design.md`  
**Status**: Draft

## Test Coverage Matrix
> Generated from codebase + guidelines: `AGENTS.md`, `rules/eval-criteria.md`, `rules/simulation-determinism.md`, `rules/llm-boundary.md`, `scripts/test.sh`, `scripts/verify.sh`.
| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Scenario/period loaders + validator (`Periods/*Loader`, `PeriodDefinitionValidator`) | unit + integration | 1:1 com PERIOD-01..06 + erros por campo/caminho + casos negativos de contrato | `tests/LivingWorld.Tests/Periods/*Tests.cs` | `bash scripts/test.sh --filter Category!=Scenario` |
| Persistência de templates (`Infrastructure`, EF mapping/migration) | integration | happy + conflito de versão + round-trip de payload + consulta por id/versão | `tests/LivingWorld.Tests/Periods/*Repository*Tests.cs` | `bash scripts/test.sh --filter Category!=Scenario` |
| API (`POST/GET /periods`, `POST /worlds/start`) | integration | 200/400/404/409 + contrato de erro determinístico + start por template | `tests/LivingWorld.Tests/Periods/*EndpointTests.cs` | `bash scripts/verify.sh` |
| Determinismo/arquitetura/causalidade | unit + architecture + scenario curto | anti-literal de período, mesma seed mesmo hash, controle/tratamento com direção | `tests/LivingWorld.Tests/Periods/*Determinism*Tests.cs` | `bash scripts/verify.sh` |
| Documentação operacional (`period-authoring.md`) | none | completude do contrato + exemplos válidos/ inválidos + fluxo de cadastro | `docs/domain/period-authoring.md` | build gate only |

## Parallelism Assessment
> Generated from codebase — confirm before Execute.
| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| --- | --- | --- | --- |
| unit | Yes | objetos/JSON por teste sem estado compartilhado | `BehaviorScenarioLoaderTests.cs` |
| integration (API) | Yes | host de teste isolado por execução | `NpcEndpointTests.cs` |
| integration (persistência/migration) | No | mesma infraestrutura sqlite/migration tende a colidir sem isolamento explícito | `PersistentWorldRunnerTests.cs` |
| architecture/reflection | Yes | leitura de assembly, sem mutação de estado | `ArchitectureTests.cs` |
| scenario/determinismo | No | comparação sequencial de execução e hashes | `DeterminismTwoProcessTests.cs` |

## Gate Check Commands
| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | task com unit/integration local sem migração/cenário | `bash scripts/test.sh --filter Category!=Scenario` |
| Full | task com API, persistência, arquitetura ou determinismo | `bash scripts/verify.sh` |
| Build | fechamento de fase | `bash scripts/build.sh && bash scripts/lint.sh && bash scripts/test.sh --filter Category!=Scenario` |

## Execution Plan
### Phase 1 (Sequential)
`T1 -> T2 -> T3`
### Phase 2 (Sequential + merge)
`T3 -> T4 -> T5`
### Phase 3 (Parallel + merge)
`T5 -> { T6 [P], T7 [P] } -> T8`
### Phase 4 (Sequential)
`T8 -> T9 -> T10`
### Phase 5 (Sequential — adicionada pós-T10, feedback do usuário)
`T10 -> T11 -> T12`

## Task Breakdown
### T1: Definir contrato dinâmico de período no cenário
**What**: criar modelo/loader para bloco de startpoint dinâmico (vieses + regras de transformação de profissões/habilidades). **Where**: `src/LivingWorld.Simulation/Periods/PeriodDynamicsLoader.cs` (+ modelos). **Depends on**: None. **Reuses**: padrão `*ScenarioLoader` atual. **Requirement**: PERIOD-01..03. **Tests**: unit. **Gate**: Quick.

### T2: Criar `PeriodDefinitionValidator` composto
**What**: orquestrar validação de mapa/população/comportamento/economia/cidades + dinâmicas, com erro determinístico por campo/caminho. **Where**: `src/LivingWorld.Simulation/Periods/PeriodDefinitionValidator.cs`. **Depends on**: T1. **Reuses**: `MapScenarioLoader`, `PopulationScenarioLoader`, `BehaviorScenarioLoader`, `EconomyScenarioLoader`, `CityScenarioLoader`. **Requirement**: PERIOD-04..06, PERIOD-07..10. **Tests**: unit + integration. **Gate**: Quick.

### T3: Integrar cenário completo no runtime (`ScenarioLoaderV2`)
**What**: substituir integração parcial por pipeline que pluga todos os loaders e remove fallback hardcoded para campos presentes no período. **Where**: `src/LivingWorld.Simulation/ScenarioLoader.cs` (ou novo `ScenarioLoaderV2.cs` + wiring). **Depends on**: T2. **Reuses**: `WorldState`, `ScenarioRunner.DefaultSystems()`. **Requirement**: PERIOD-01..06. **Tests**: integration. **Gate**: Full.

### T4: Persistência de templates de período
**What**: adicionar modelo/tabela/repositório para templates versionados (`PeriodId+Version`, payload, origem, timestamp). **Where**: `src/LivingWorld.Infrastructure/*PeriodTemplate*`, `WorldDbContext`, migration EF. **Depends on**: T3. **Reuses**: padrão `IWorldRepository` + EF migrations. **Requirement**: PERIOD-07..10. **Tests**: integration. **Gate**: Full.

### T5: Expor endpoints de catálogo de períodos
**What**: implementar `POST /periods`, `GET /periods`, `GET /periods/{id}` com validação, conflito e contrato de erro determinístico. **Where**: `src/LivingWorld.Api/Program.cs` (+ handlers). **Depends on**: T4. **Reuses**: padrão endpoint existente (`/npcs/{id}`). **Requirement**: PERIOD-07..10. **Tests**: integration. **Gate**: Full.

### T6 [P]: Expor start de mundo por template registrado
**What**: implementar `POST /worlds/start` que resolve `periodId`, aplica `seed` e inicializa mundo pelo mesmo pipeline dos templates base. **Where**: `src/LivingWorld.Api/Program.cs` + serviço de bootstrap. **Depends on**: T5. **Reuses**: `ScenarioLoader` integrado e repositório de template. **Requirement**: PERIOD-04..06, PERIOD-07..10. **Tests**: integration. **Gate**: Full.

### T7 [P]: Entregar documentação operacional para IA externa
**What**: criar `docs/domain/period-authoring.md` com contrato canônico, schema, exemplos positivos/negativos e fluxo de envio para `POST /periods`. **Where**: `docs/domain/period-authoring.md`. **Depends on**: T5. **Reuses**: `scenarios/default.json`, `scenarios/test-scifi.json` como referência de formato. **Requirement**: PERIOD-11..13. **Tests**: none. **Gate**: Build.

### T8: Templates base da fase 13
**What**: adicionar templates de referência (pré-histórico, medieval, moderno, futurista, criaturas) no formato novo e registrar baseline de compatibilidade. **Where**: `scenarios/periods/*.json` (novo diretório) + fixtures de teste. **Depends on**: T6, T7. **Requirement**: PERIOD-17..18. **Tests**: integration + scenario curto. **Gate**: Full.

### T9: Blindagem arquitetural + determinismo
**What**: testes que proibem literais de período no domínio/simulação e provam mesmo startpoint+seed => mesmo hash; startpoints diferentes => hash diferente. **Where**: `tests/LivingWorld.Tests/Periods/*Architecture*Tests.cs`, `*Determinism*Tests.cs`. **Depends on**: T8. **Requirement**: PERIOD-04..06, PERIOD-14..16. **Tests**: architecture + scenario curto. **Gate**: Full.

### T10: Causalidade de vieses com braço de controle
**What**: harness de controle/tratamento com mesma seed para validar direção de viés declarado e baseline de horizonte mínimo em `tests/baselines/`. **Where**: `tests/LivingWorld.Tests/Periods/*Causal*Tests.cs`, `tests/baselines/period-evolution-horizon.json`. **Depends on**: T9. **Requirement**: PERIOD-14..16. **Tests**: scenario curto (+ nightly quando necessário). **Gate**: Full.

### T11: Abrir catálogo de habilidade (`SkillType`) como dado dinâmico do período
**What**: substituir o enum fechado `SkillType`/os 13 campos fixos de `SkillSet` por um catálogo aberto por id — mesmo padrão de `ProfessionType`/`PopulationCatalog` (motor só vê id; nome é dado externo do período/IA, nunca literal em `src/`, ver AD-023/AD-025). `PeriodDynamicsLoader.SkillBias`/futuras regras de transformação de habilidade passam a referenciar esse id aberto em vez de `Enum.TryParse<SkillType>`. Resolve o único ponto onde o motor hoje decide por identidade de habilidade específica: `SkillTeachingSystem.GainFromTutoring` lê `SkillType.Teaching` como literal (multiplicador de tutoria) — vira id declarado por regra, mesmo padrão de `SkillsRules.SkillByProfession`. **Where**: `src/LivingWorld.Domain/Population/SkillSet.cs`, `SkillType.cs` (remover), `SkillsRules.cs`, `src/LivingWorld.Simulation/Population/SkillTeachingSystem.cs`, `src/LivingWorld.Simulation/Periods/PeriodDynamicsLoader.cs`, `docs/domain/period-authoring-dynamics.md` (atualizar seção "catálogo fechado"). **Depends on**: T10. **Reuses**: `ProfessionType`/`PopulationCatalog` como precedente direto. **Requirement**: PERIOD-19..21. **Tests**: unit (SkillSet/SkillsRules com id aberto) + architecture (nenhum nome de habilidade vira literal de decisão, mesmo padrão de `PeriodArchitectureTests`) + integration (`SkillTeachingSystem` com skill-de-tutoria declarada por regra, não mais enum fixo). **Gate**: Full.

### T12: Expor leitura do catálogo ativo (profissão + habilidade) via API
**What**: rota de leitura que devolve os ids (e nomes, quando declarados) de profissão/habilidade de um período/template registrado — reaproveita o formato de resposta de `GET /periods`. Design exato da rota (estender `GET /periods/{id}` vs. rota nova `GET /periods/{id}/catalog`) fica pra fase de Design desta task, não decidido aqui. **Where**: `src/LivingWorld.Api/*.cs` (endpoint novo ou extensão de `PeriodsEndpoints.cs`). **Depends on**: T11. **Reuses**: padrão de resposta de `PeriodsEndpoints`. **Requirement**: PERIOD-22..23. **Tests**: integration (endpoint). **Gate**: Full.

## Diagram-Definition Cross-Check
| Task | Depends On (body) | Diagram | Status |
| --- | --- | --- | --- |
| T1 | None | root | ✅ |
| T2 | T1 | T1->T2 | ✅ |
| T3 | T2 | T2->T3 | ✅ |
| T4 | T3 | T3->T4 | ✅ |
| T5 | T4 | T4->T5 | ✅ |
| T6 | T5 | T5->{T6,T7} | ✅ |
| T7 | T5 | T5->{T6,T7} | ✅ |
| T8 | T6,T7 | {T6,T7}->T8 | ✅ |
| T9 | T8 | T8->T9 | ✅ |
| T10 | T9 | T9->T10 | ✅ |
| T11 | T10 | T10->T11 | ✅ |
| T12 | T11 | T11->T12 | ✅ |

## Test Co-location Validation
| Task | Code Layer | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1 | Period dynamics loader | unit+integration | unit | ✅ (integration coberta em T2) |
| T2 | Validator composto | unit+integration | unit+integration | ✅ |
| T3 | Runtime loader integration | integration | integration | ✅ |
| T4 | Persistence/migration | integration | integration | ✅ |
| T5 | Period catalog API | integration | integration | ✅ |
| T6 | World start API | integration | integration | ✅ |
| T7 | External authoring docs | none | none | ✅ |
| T8 | Reference templates | integration+scenario | integration+scenario | ✅ |
| T9 | Architecture/determinism | architecture+scenario | architecture+scenario | ✅ |
| T10 | Causal control harness | scenario | scenario | ✅ |
| T11 | Open skill catalog | unit+architecture+integration | unit+architecture+integration | ✅ |
| T12 | Active catalog read API | integration | integration | ✅ |
