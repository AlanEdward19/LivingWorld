# Verificação independente: #97 A.1 (paralelizar loops de seeds)

Commit verificado: `01f9213d`, base `b29ed9f2`. Máquina de 8 cores, com outras worktrees ativas.

**Veredito: FAIL.** A paralelização está correta (paralelo = sequencial, por hash, em 4 corridas) e o helper está
protegido por testes. Reprova por um único defeito de correção pequena: F1, um teste ainda cita seed que não é a do mundo.

| # | Critério | Resultado |
|---|---|---|
| 1 | Só código de teste; testes sem perda de poder | PASS |
| 2 | Sem estado estático que altere a simulação | PASS |
| 3 | Paralelo = sequencial, sem flakiness | PASS |
| 4a | Mutações no helper reprovam | PASS |
| 4b | Mutações nos corpos reprovam nomeando a seed | PASS |
| 4c | Seed citada é sempre a seed real do mundo | **FAIL (F1)** |
| 5 | Medições registradas com honestidade | PASS |
| 6 | `verify.sh` e `--scenarios` | PASS |

## F1: teste de divergência (2 anos) cita a seed errada (FAIL)
`tests/LivingWorld.Tests.Unit/Emergence/TechDiscoveryTests.cs:230-232`: o corpo recebe `seed` e roda dois mundos,
`First(seed)` e `First(seed + 1000)`. Uma exceção no segundo é relatada com a seed do primeiro.
Evidência (mutação `bodysim`, `throw` em `First` quando `s == 1003`):
`System.InvalidOperationException : seed 3 falhou: mutB mundo 1003 quebrado`. A mensagem real de uma falha de simulação
não traz o número 1003, e quem reproduz roda o mundo 3, que passa.
O plano promete "a seed citada na falha ser sempre a seed real do mundo (ex.: 1001)". O teste de 30 anos foi corrigido
(`:254`, lista explícita), este é o mesmo padrão e ficou de fora. Os outros 5 chamadores só rodam mundos da própria seed.
**Mudança:** rodar os 20 mundos por lista explícita, como em `:254` (`[1..10, 1001..1010]`), manter `Assert.NotEqual("", ...)`
só para os mundos de 1 a 10 e comparar `runs[i]` com `runs[i + 10]` depois.

## 1. Escopo e poder dos testes: PASS
- `git diff --name-only b29ed9f2...HEAD | grep -iE 'baseline|golden|^src/'` não retorna nada; fora de `tests/`, só `.specs/`.
- Comparação antigo x novo, teste a teste:
  - Divergência 2 anos: 1..10 + seed+1000; `Assert.NotEqual("", a)` segue por seed, dentro do corpo; contagem depois.
  - 30 anos: mundos 1,2,3,1001,1002,1003; `Count >= 3` por mundo; pares `runs[i]`/`runs[i+3]` = (1,1001), (2,1002), (3,1003).
  - 20 anos x 10 seeds: corpo intacto, os três asserts por seed.
  - Orphan: 1..20 (`Seeds`), com e sem acolhimento; o laço antigo não tinha assert, a soma é idêntica e em ordem.
  - Disease: 1..8; contagens por seed viram retorno e somam depois.
  - Codec: `Parallel.For(1,21)` = 1..20; mesmo assert.
- Traits inalterados. Sem captura de variável de loop (`seed` é parâmetro de lambda). Sem escrita concorrente em coleção:
  `RecordingSink`, `DeathSink` e `deaths` são criados por corrida; `disease` e `Pressured` são records só lidos.

## 2. Estado estático: PASS
Varredura própria em `src/` e `Tests.Shared` (static não readonly, propriedades com setter, coleções estáticas, `??=`,
`ThreadStatic`, `ThreadLocal`, `AsyncLocal`, `ArrayPool`, `Random.Shared`). Sem pools nem estado por thread.
- `Relationship.s_axesVersion`, `Building.s_layoutVersion`: `Interlocked`, monotônicos. A versão é lida antes do cálculo, então
  outro mundo só provoca miss. Em `CityOccupancy.cs:175,483` o atalho exige delta da versão == prédios novos; como cada `new Building`
  incrementa (candidatos inclusive, delta >= adicionados), mais incrementos só desligam o atalho.
- `ConditionalWeakTable` por `WorldState`/`WorldMap`/`Building`: instância por seed.
- Catálogos compartilhados: `RoutineBoundaries.Tables` (ConcurrentDictionary, função pura), `ObjectCatalog.Indexes` (`GetValue`).
- `BuildingFootprintGenerator.Cache`: função pura; `Clear` só recalcula. `GuestLodging.Caches`: `LazyInitializer`.
- `DefaultSystems()` cria sistemas novos a cada chamada. `ExtraordinaryMechanicRegistry.Default` não tem estado de instância mutável.
- `DecisionContextCache.*BuildCount` (não atômicos): nenhum teste alterado lê. `LlmResponseValidator.EnforceValidation`,
  `DistortionEngine.LlmInvocationProbe`: só testes de LLM escrevem.
- `NpcWakeScheduler.cs:177`: `Parallel.For` indexado já existente, aplicado em ordem de Id.

## 3. Paralelo = sequencial: PASS
Instrumentação temporária: modo `for` por `LW_SEQ=1` e log por seed com `CanonicalHash` em Disease (8 seeds, 2 mundos),
30 anos (6 mundos) e Orphan (20 seeds, 2 mundos): 34 linhas, 76 hashes de mundo.

| Corrida | Modo | Testes | Parede | SHA-256 do log ordenado |
|---|---|---|---|---|
| par1 | paralelo | 3/3 | 248 s | `86f76415bc260bbc` |
| seq | `for` | 3/3 | 322 s | `86f76415bc260bbc` |
| par2 | paralelo | 3/3 | 199 s | `86f76415bc260bbc` |
| par3 | paralelo | 3/3 | 241 s | `86f76415bc260bbc` |

`cmp` confirma os logs byte-idênticos. O teste de 20 anos já compara dois mundos da mesma seed sob paralelismo e passou no gate.

## 4. Mutações (cada uma restaurada com `git checkout -- tests`; `git status` limpo ao final)
| Mutação em `ParallelSeeds.cs` | Resultado |
|---|---|
| Engole falhas | reprovam 4 testes de `ParallelSeedsTests` |
| Ordem invertida | reprovam `Map_returns_results_in_seed_order...` e `Map_over_a_range...` |
| `Degree` clamp 4 / `Degree` x2 | reprova `Degree_is_bounded...` |
| Reporta a maior seed | reprova `When_several_seeds_fail...` |
| Sem `InnerException` | reprova `The_original_exception...` |
| Mensagem sem seed | reprovam 3 testes |
| Índice no lugar da seed (lista explícita) | reprova `An_explicit_seed_list...` |
| Sem guard de faixa vazia | reprova `An_empty_range...` |
| Faixa `last` exclusiva | reprova `Map_over_a_range...` |
| `MaxDegreeOfParallelism = Degree*2` / sem a opção | **sobrevivem** (ver observações) |

| Corpo (6/6 testes reprovam em cada rodada) | Mensagem |
|---|---|
| Assert falha: Disease 5, Orphan 11, 2 anos 4, 30 anos mundo 1002, 20 anos 7, Codec 13 | `seed <N> falhou: mutA`, N correto nos 6 |
| Simulação quebrada: 20 anos 6 e Codec 9 (estado alterado) | `seed 6` e `seed 9 falhou: Assert.Equal() Failure` |
| Simulação quebrada: 30 anos mundo 1003, Disease 3, Orphan 12 | seed correta |
| Simulação quebrada: 2 anos, mundo 1003 | `seed 3 falhou` (**F1**) |

## 5. Medições: PASS
Duas medições por braço, ruído declarado (293 x 406 s), sem ganho exato. Os números históricos não são reproduzíveis aqui.
Medição própria na mesma direção: 322 s sequencial contra 199-248 s paralelo, nos 3 testes juntos (com classes já simultâneas).

## 6. Gates: PASS
- `bash scripts/verify.sh`: exit 0, 213 s, `verify: OK`, Unit 2467 ok / 3 skip.
- `bash scripts/test.sh --scenarios`: exit 0, 541 s, 20 ok / 1 skip.

## Observações (não bloqueantes)
- Mudar só `MaxDegreeOfParallelism` não é observável com 8 cores: o pool não passa do grau nesses 64 corpos curtos.
- O grau é por chamada: classes simultâneas somam, então "não satura a máquina" (`ParallelSeeds.cs:8`) é impreciso.
