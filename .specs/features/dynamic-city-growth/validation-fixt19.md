# dynamic-city-growth — Validation fixt19

**Date**: 2026-08-23  
**Diff**: `HEAD` vs worktree (arquivos inline do post-ship fix)  
**Verifier**: sub-agent independente (autor ≠ verificador)  
**Verdict**: **PASS ✅**

## Spec-anchored evidence

| AC | Resultado esperado | Evidência `file:line` + expressão | Status |
|---|---|---|---|
| AC1 | Household empregado, alimentado e dentro dos bounds não migra só por score maior | `tests/LivingWorld.Tests/Cities/MigrationSystemTests.cs:359-360` — `Assert.Equal(origin.Id, head.City)` + `Assert.Null(household.PendingRelocationCity)`; guard real em `src/LivingWorld.Simulation/Cities/MigrationSystem.cs:74` | ✅ |
| AC2 | Household empregado/alimentado fora dos bounds pode escolher cidade melhor | `tests/LivingWorld.Tests/Cities/MigrationSystemTests.cs:371,375-377` — `head.MoveTo(9,9)` e `Assert.Equal(destination.Id, household.PendingRelocationCity)`; bounds em `MigrationSystem.cs:133-135` | ✅ |
| AC3 | Pending relocation não é reavaliado/retargeted | `tests/LivingWorld.Tests/Cities/MigrationSystemTests.cs:389-400` — destino pendente com score 0,1, terceira cidade com score 1, e `Assert.Equal(destination.Id, household.PendingRelocationCity)`; guard em `MigrationSystem.cs:68` | ✅ |
| AC4 | UI oferece 16x e chama `setSpeed(16)` | `web/tests/TimeControls.test.tsx:64-65` — click `16x` + `expect(source.setSpeed).toHaveBeenCalledWith(16)`; lista em `web/src/components/TimeControls.tsx:15` | ✅ |
| AC5 | +1 ano só pausado; avança `HoursPerYear`; running retorna 409 sem avanço; adapters mantêm UI/rota | `web/tests/TimeControls.test.tsx:161-172` — `+1 ano` disabled/running e enabled/paused; `tests/LivingWorld.Tests/Simulation/SimulationControlEndpointsTests.cs:119-122,135-138` — OK + `before + HoursPerYear`, Conflict + `before`; mock `web/tests/data/MockTimeControlSource.test.ts:40-47` — tick `8640`; real `web/tests/data/real/timeControlSource.test.ts:48-51` — POST `/simulation/advance-year` | ✅ |

**Spec-anchored check**: 5/5 ACs com outcome exato; 0 gaps de precisão.

## Ciclo pós-chegada e projeções

- `tests/LivingWorld.Tests/Cities/MigrationSystemTests.cs:404-423` conclui a chegada, roda 10 novos dias e prova NPC + household ainda no destino e nenhum pending.
- `src/LivingWorld.Simulation/Cities/RelocationArrivalSystem.cs:39-44` muda `Npc.City`/`Household.City` apenas quando todos chegam e limpa o pending.
- `src/LivingWorld.Api/Visual/CityProjector.cs:48-50` e `GlobalProjector.cs:44,62-65` derivam residentes/população/escopo do estado canônico. O trecho em World durante viagem é posição fora dos bounds, não troca fictícia de cidade; a oscilação ±1 refletia a troca canônica que o teste de 10 dias agora impede no cenário reportado.

## Discrimination sensors (scratch isolado)

| Mutação | Resultado |
|---|---|
| Ignorar `NeedsMigration` em `MigrationSystem.cs:74` | ✅ Killed por AC1 (`PendingRelocationCity` deixou de ser null) |
| Remover guard de pending em `MigrationSystem.cs:68` | ✅ Killed por AC3 após fixture discriminante (retarget para terceira cidade) |
| `HoursPerYear - 1` em `SimulationHost.cs:25` | ✅ Killed por endpoint AC5 (esperado 8641, atual 8640) |

**Sensor**: 3/3 killed; 0 survived.

## Gates

- Comando oficial focado: `bash scripts/test.sh --filter 'FullyQualifiedName~MigrationSystemTests|FullyQualifiedName~SimulationControlEndpointsTests'` (Git Bash, scratch sincronizado após lock concorrente no worktree).
- .NET: **26 passed, 0 failed, 0 skipped**. Web executada pelo mesmo script: **415 passed, 0 failed** em 67 arquivos.
- Gate global: vermelho preexistente apenas por `FamineCausalChainTests` (1/10) e `ScarcityPriceCausalTests` (5/10); fora deste diff e não bloqueia o verdict focado.
- Avisos React `act(...)` são preexistentes e não falham o gate.

## Quality / determinism

- Mudança cirúrgica, sem RNG/tempo de máquina, iteração de households/cidades ordenada e relógio anual derivado de `world.Calendar.HoursPerYear`.
- Regras seguidas: `rules/tests.md`, `rules/simulation-determinism.md`, `tlc-spec-driven/references/coding-principles.md`.
- Adapters mock/real preservam o contrato de UI/rota pedido; a diferença programática em erro 409 replica o padrão preexistente de `step()` e não integra o AC.

## Ranked gaps

Nenhum gap bloqueante ou major remanescente. **Ready ✅**
