# Phase 16 — Potência: Tasks

## Execution Protocol

Executar com `tlc-spec-driven`; cada tarefa inclui testes e gate. Não commitar nesta árvore suja
até o gate global já pendente voltar a passar.

**Design**: `.specs/features/phase-16-powers/design.md`  
**Status**: In Progress

## Test Coverage Matrix

> Fontes: `AGENTS.md`, `rules/tests.md` e amostra `*ScenarioLoaderTests.cs`.

| Camada | Tipo | Cobertura | Local | Comando |
|---|---|---|---|---|
| Domain/loader | unit | 1:1 POW-01..03 + edges | `tests/**/Extraordinary` | `bash scripts/test.sh --filter Extraordinary` |
| runtime systems | unit/determinism | ramos + mesmos seeds | `tests/**/Extraordinary` | mesmo filtro |
| cenário causal | paired scenario | controle/tratamento | `tests/**/Extraordinary` | filtro `Category=Scenario` |

## Parallelism Assessment

| Tipo | Seguro? | Evidência |
|---|---|---|
| unit puro | sim | objetos por teste, sem store global |
| cenário | não assumido | `rules/tests.md` separa cenários caros |

## Gate Check Commands

| Gate | Comando |
|---|---|
| Quick | `bash scripts/test.sh --filter Extraordinary` |
| Build | `bash scripts/build.sh` |
| Final | `bash scripts/verify.sh` |

## Execution Plan

```text
T1 -> T2 -> T3 -> T4 -> T5
```

## Task Breakdown

### T1 — Borda composicional e gate desligado

**Where**: `Domain/Extraordinary`, `Simulation/Extraordinary`, testes correspondentes  
**Depends on**: none  
**Requirements**: POW-01, POW-02, POW-03  
**Done when**: bloco ausente vira `Disabled`; descritores válidos preservam eixos; entradas
inválidas retornam `Failure`; plano desligado contém zero portadores/eventos/sistemas; ligado
falha explicitamente enquanto runtime não existe; gate Quick passa.  
**Tests**: unit · **Gate**: Quick · **Status**: Complete (6/6 + 433/433 web)

### T2 — Integração canônica e registro seletivo

**Where**: `ScenarioLoaderV2`, `WorldState`, snapshot/hash, composição do relógio  
**Depends on**: T1 · **Requirements**: POW-01, POW-04, POW-07  
**Done when**: desligado não registra sistema; ligado persiste descritores e registra somente os
sistemas da fase; aparência/necessidade/senescência/manifestação ficam consultáveis;
round-trip/hash cobertos.  
**Tests**: unit/determinism · **Gate**: Build · **Status**: Complete (16/16 + 440/440 web)

### T3 — Aplicação de efeito e custo

**Where**: `Simulation/Extraordinary`  
**Depends on**: T2 · **Requirements**: POW-05  
**Done when**: alvo declarado é a única mutação; custo é debitado no uso; resultado e causalidade
entram no event log; controle pareado preserva conservação.  
**Tests**: unit/scenario · **Gate**: Build · **Status**: Pending

### T4 — Aquisição e manifestação

**Where**: `Simulation/Extraordinary`  
**Depends on**: T3 · **Requirements**: POW-06  
**Done when**: regras autoradas dirigem transições e exemplos de artefato responsivo,
transformação noturna e transformação cíclica usam o mesmo motor.  
**Tests**: unit/determinism · **Gate**: Build · **Status**: Pending

### T5 — Integração social, LOD e web

**Where**: projeções/API/web e adaptadores de cultura/LOD  
**Depends on**: T4 · **Requirements**: POW-04..06  
**Done when**: portadores conhecidos aparecem por LOD, reação vem da cultura, formulário autora
descritores e o gate Final passa.  
**Tests**: integration/web · **Gate**: Final · **Status**: Pending

## Cross-checks

| Task | Depends | Diagrama | Teste co-localizado |
|---|---|---|---|
| T1 | none | início | sim |
| T2 | T1 | T1→T2 | sim |
| T3 | T2 | T2→T3 | sim |
| T4 | T3 | T3→T4 | sim |
| T5 | T4 | T4→T5 | sim |
