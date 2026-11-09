# Plano: paralelizar loops de seeds dos testes Scenario (#97 A.1)

Escopo: só código de teste. Mesmas seeds, mesmos anos, mesmos asserts, mesmas categorias. Nada em `src/`.

## Prova de segurança (estado estático)
- Varredura de `static` não readonly em `src/Domain|Simulation|Infrastructure`: sem `ThreadStatic`.
- Compartilhados no processo, todos seguros sob paralelismo:
  - `Relationship.AxesVersion` e `Building.LayoutVersion`: contadores monotônicos via `Interlocked`/`Volatile`; só
    invalidam caches (`LazyRelationshipFacts`, `CityOccupancy`). A versão é lida antes de calcular, então incremento
    alheio só gera miss conservador, nunca resultado velho.
  - Caches por mundo (`ConditionalWeakTable<WorldState,...>`: `DecisionContextCache`, `MarketIndex`,
    `LaborValueIndex`, `LazyRelationshipFacts`, `CityOccupancy`): chaveados pela instância; cada seed tem o seu mundo.
  - `WorldSnapshot.SnapshotJsonOptions`, `ReferentialIntegritySweep` (ConcurrentDictionary), tabelas readonly: só leitura.
- `DecisionContextCache.*BuildCount` (contadores estáticos não atômicos) só são lidos por `DecisionContextCacheTests`;
  nenhum teste alterado os lê. O xunit já roda classes em paralelo no mesmo processo, e `PositionalSnapshotTests` /
  `PopulationBaselineTests` já usavam `Parallel.For`.
- Resultado: nenhum teste ficou de fora por estado compartilhado.

## Mudanças
- `tests/LivingWorld.Tests.Shared/Parallelism/ParallelSeeds.cs`: `Map`/`ForEach` por faixa de seeds, grau único
  `Math.Clamp(ProcessorCount,1,8)`; captura a exceção por seed e relança a de menor seed como
  `InvalidOperationException("seed N falhou: ...")` com o original em `InnerException` (sem `AggregateException`).
  Resultados em ordem de seed: a agregação pós-loop continua determinística. Aceita faixa (vazia se last < first) ou
  lista explícita de seeds, para a seed citada na falha ser sempre a seed real do mundo (ex.: 1001).
- `tests/LivingWorld.Tests.Unit/Shared/ParallelSeedsTests.cs` (rápidos, fora de Scenario): ordem, grau, falha nomeia a
  seed, `InnerException`, menor seed, lista explícita, faixa vazia. Cada um confirmado com sua mutação (engolir falha,
  rotacionar ordem, varrer de trás, grau x2, tirar o guard da faixa vazia). Limite: remover só o `MaxDegreeOfParallelism`
  não é detectável em máquina de até 8 cores, onde o padrão do `Parallel.For` já coincide com o grau.
- `TechDiscoveryTests`: 20 anos x 10 seeds; 30 anos (6 corridas = 3 pares); divergência de 2 anos x 10 seeds.
- `OntogenyDebtsTests.Orphan_care...`: 20 seeds, paralelo entre seeds; soma depois, em ordem.
- `DiseaseTests` (8 seeds) e `PositionalSnapshotTests.Codec...` (já paralelo; agora com grau limitado e seed nomeada).
- Não alterados por ganho desprezível: `Famine...` (~4 s de teste), `Collapse_rate...` (~8 s).

## Medição (Release, 8 cores, outras worktrees ativas; parede inclui ~10 s de build/arranque)
Cada teste isolado: `bash scripts/test.sh --scenarios --filter "FullyQualifiedName~<nome>"`. Duas medições por braço.
| Teste | Antes (s) | Depois (s) |
|---|---|---|
| TechDiscovery 20 anos x 10 seeds | 293 / 406 | 124 / 169 |
| Orphan_care | 302 / 172 | 82 / 90 |
| TechDiscovery 30 anos | 184 / 146 | 72 / 76 |
| TechDiscovery divergência 2 anos | 54 / 36 | 30 / 43 |
| Disease 8 seeds | 96 / 69 | 51 / 48 |
| Codec 20 seeds (já era paralelo) | 28 / 18 | 26 / 28 |
| Famine (sem mudança) | 14 / 8 | 11 / 21 |
| Collapse (sem mudança) | 20 / 12 | 20 / 23 |

Leitura: o ruído entre medições do mesmo braço é grande (293 vs 406 s). Nos testes caros a queda (~2x a ~3x) é maior
que esse ruído, mas dois pontos por braço não dão um ganho exato. Codec, Famine e Collapse ficaram dentro do ruído.

## Fora de escopo
A.2 a A.5, B, C; número de seeds/anos/categorias; asserts; `src/`; goldens e baselines.
