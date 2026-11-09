# Verificação independente: #97 A.1 (paralelizar loops de seeds)

Commit verificado: `d5693574`, base `b29ed9f2`. Máquina de 8 cores, com outras worktrees ativas.

**Veredito: FAIL.** A paralelização está correta (paralelo = sequencial, por hash, em 58 corridas).
Reprova por dois defeitos no contrato de erro e na cobertura do helper, ambos de correção pequena.

| # | Critério | Resultado |
|---|---|---|
| 1 | Só código de teste; testes sem perda de poder | PASS |
| 2 | Sem estado estático que altere a simulação | PASS |
| 3 | Paralelo = sequencial, sem flakiness | PASS |
| 4a | Contrato do helper funciona | PASS (com F2 no uso) |
| 4b | Helper protegido por teste | **FAIL (F1)** |
| 4c | Falha nomeia a seed em todos os testes | **FAIL (F2)** |
| 5 | Medições registradas com honestidade | PASS |
| 6 | `verify.sh` e `--scenarios` | PASS |

## 1. Escopo e poder dos testes: PASS
- `git diff --name-only base...HEAD | grep -E '^src/|baselines|golden'` não retorna nada.
- Mesmas seeds, horizonte e asserts, comparados no diff: 2 anos x 10 (mais seed+1000); 30 anos x 3 pares
  (`i-3+1000` dá 1001..1003, e `runs[i]` contra `runs[i+3]` refaz os pares); 20 anos x 10; Orphan 20; Disease 8; Codec 1..20.
- Asserts por seed continuam dentro do corpo paralelo. Traits inalterados.
- Sem escrita concorrente: contadores viraram retorno por seed e somam depois. `disease` e `Pressured` são records
  compartilhados só para leitura; `RecordingSink`, `DeathSink` e `deaths` são por corrida.

## 2. Estado estático: PASS
Varredura própria em `src/` e `Tests.Shared`. Não há `ThreadStatic`, `AsyncLocal` nem `ThreadLocal`.
- `Relationship.s_axesVersion`, `Building.s_layoutVersion`: atômicos. Outro mundo só aumenta o delta e força recálculo
  (`CityOccupancy.cs:149,163`, `SiblingsOf`; `LazyRelationshipFacts`).
- `ConditionalWeakTable` por `WorldState`/`WorldMap`/`Building`: instância por seed (`InitialMap(seed,..)`).
- Por catálogo compartilhado: `RoutineBoundaries.Tables` (ConcurrentDictionary, função pura), `ObjectCatalog.Indexes` (`GetValue`, só leitura).
- `BuildingFootprintGenerator.Cache`: função pura de (id, tipo, orientação); `Clear` só causa recálculo.
- `GuestLodging.Caches`: `LazyInitializer`. `DecisionContextCache.*BuildCount`: não atômicos, mas nenhum teste alterado os lê.
- `LlmResponseValidator.EnforceValidation`, `DistortionEngine.LlmInvocationProbe`: não alcançados pelos testes alterados.
- `NpcWakeScheduler.cs:177` usa `Parallel.For` indexado (já existia), e o agendamento é ordenado por Id.

## 3. Paralelo = sequencial: PASS
Instrumentação temporária grava, por seed, as chaves e o `CanonicalHash` (e contagens em Disease e Orphan), em
tech 2 anos (20 mundos), tech 20 anos (10), Disease (8) e Orphan (20): 58 linhas.

| Corrida | Modo | Pico simultâneo | Testes | Parede | SHA-256 do log ordenado |
|---|---|---|---|---|---|
| seq | `for` + `DOTNET_PROCESSOR_COUNT=1` | 1 | 4/4 | 824 s | `1c8ea167...` |
| par1 | `Parallel.For`, grau 8 | 24 (3 classes x 8) | 4/4 | 288 s | `1c8ea167...` |
| par2 | `Parallel.For`, grau 8 | 24 | 4/4 | 259 s | `1c8ea167...` |

Os três logs são byte-idênticos (`cmp`).

## 4. Helper e mutações
Testes temporários do helper (não versionados) passam no original: ordem, grau limitado, seed e exceção original
preservadas, menor seed, sem cancelar as demais.

| Mutação | Detector | Resultado |
|---|---|---|
| M1 resultado rotacionado (`ParallelSeeds.cs:31`) | teste temporário | reprova `Order` |
| M1 idem | **6 testes alterados do repo** | **SOBREVIVE (6/6 passam)** |
| M2 sem a seed na mensagem | teste temporário | reprova |
| M3 sem `InnerException` | teste temporário | reprova |
| M4 maior seed em vez da menor | teste temporário | reprova |
| M5 grau ilimitado | teste temporário | reprova `Degree_is_limited` |
| M6 engole a falha (`:28`) + assert injetado no Codec | **Codec do repo** | **SOBREVIVE (passa)** |
| T1 assert falha nas seeds 3 e 7 (Codec) | Codec | reprova: `seed 3 falhou: injetada` |
| T2 assert falha no mundo 1001 (30 anos) | 30 anos | reprova: `seed 4 falhou: injetada-1001` (F2) |
| T3 simulação não determinística na seed 3 (`src/`) | 20 anos | reprova: `seed 3 falhou: Assert.Equal() Failure` |

Cada mutação foi restaurada com `git checkout --`; `git status --short` ficou vazio ao final.

### F1: helper sem teste próprio (FAIL)
`tests/LivingWorld.Tests.Shared/Parallelism/ParallelSeeds.cs` não tem teste, contra `rules/tests.md` ("todo comportamento
novo tem teste"). O impacto é real: com M6, o helper engole as falhas e **todo assert dentro dos corpos paralelos deixa
de reprovar**, sem nenhum teste acusar. Com M1, os pares do teste de 30 anos comparam mundos errados e ele segue verde.
**Mudança:** versionar testes rápidos (fora de `Category=Scenario`) de ordem, grau, seed nomeada, `InnerException` e menor seed.

### F2: teste de 30 anos nomeia índice, não seed (FAIL)
`tests/LivingWorld.Tests.Unit/Emergence/TechDiscoveryTests.cs:254` passa o índice 1..6 como "seed". Uma falha no mundo 1001
aparece como `seed 4 falhou` (T2), e a seed 4 não existe nesse teste: quem tentar reproduzir roda outro mundo.
O plano promete `seed N falhou`, e o resumo do helper diz que o erro "sempre nomeia a seed".
**Mudança:** aceitar uma lista de seeds no helper (`[1,2,3,1001,1002,1003]`) ou incluir a seed real na exceção do corpo.

## 5. Medições: PASS
O plano registra dois pontos por braço, declara o ruído (293 contra 406 s) e não afirma ganho exato. Os números
históricos não são reproduzíveis aqui. Medição própria coerente: 4 testes, 824 s sequencial contra 259-288 s paralelo
(cerca de 3x), lembrando que o braço sequencial usou 1 core para todo o processo.

## 6. Gates: PASS
- `bash scripts/verify.sh`: exit 0, 140 s, `verify: OK`, Unit 2459 ok / 3 skip.
- `bash scripts/test.sh --scenarios`: exit 0, 403 s, 20 ok / 1 skip.

## Observações (não bloqueantes)
- O grau é por chamada: o pico real foi 24 com 3 classes simultâneas, então "não satura a máquina" é impreciso.
- `Map(1, 0)` lança `OverflowException` (o `checked` cobre a subtração) em vez de devolver vazio. Nenhum chamador usa faixa vazia.
- A lista de caches do plano omite `BuildingFootprintGenerator.Cache`, `RoutineBoundaries`, `TravelTicksCache` e `GuestLodging`, todos seguros.
