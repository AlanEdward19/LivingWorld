# Observatório de civilização: série anual (issue #112, fatia A)

Fatia D (parada, achado do motor): ver `plan-d.md`.
Status: implementado (fatia A); verificação independente fica com o orquestrador.

## Problem

Nenhuma ferramenta roda anos e mostra demografia, economia e sociedade ao longo do tempo. Sem a série, as
sub-issues de #111 não têm braço de controle nem evidência. Evidência: `Program.cs` do Workers só imprime hashes.

## Flow

1. `CivilizationRunner.Run` (new) carrega o cenário com a seed, troca o relógio por um com `YearEventTally` como sink.
2. Roda `HoursPerYear` ticks (8640 no calendário padrão de 12 × 30 × 24, não 8760) e chama `CivilizationObserver.Sample`.
3. `YearEventTally` (new) conta, no sink, nascimento e morte de NPC por cidade, causa e faixa etária.
4. `CivilizationObserver` (new) lê o estado e devolve `WorldYearSample` (mundo + uma `CitySample` por cidade ativa).

Fora do tick: não é `ISimulationSystem`, não escreve no mundo, não emite evento.

## Impact

| Front | O que muda |
| --- | --- |
| domain | termo novo: amostra de civilização. Nenhum estado canônico novo |
| stored data | nada; o hash canônico não muda (critério 1) |
| cohesion | serviço de leitura, sem consumidor causal: dívida explícita de observação, é a régua das sub-issues |

## Portas de mão única

Nenhuma: tipos novos, sem contrato público existente alterado. As chaves de métrica são formato aberto (extensível).

## Critérios (EARS)

1. Ao rodar o cenário padrão por 10 anos com e sem observatório, o `WorldSnapshot.CanonicalHash` deve ser igual.
2. A cada ano, a soma das populações por cidade deve igualar a população do mundo, que deve igualar vivos
   materializados + pools, com `population_unassigned` = 0.
3. A cada ano, `population_delta` = `births` − `deaths` + `net_migration`, com `net_migration` ≤ 0 (o cenário padrão
   não tem imigração de fora; migração só tira gente do pool).
4. Nascimentos e mortes contados pelo sink devem igualar os contados direto do estado (`BirthDate`/`DeathDate` no ano).
5. Em toda cidade e ano, mortes = fome/sede + doença + idade + outras.
6. A expectativa de vida de uma tábua sintética deve dar o valor conhecido (50, 280/11, 15).

Testes: `tests/LivingWorld.Tests.Unit/Observation/CivilizationObserverTests.cs` (gate, 10 anos, 2 corridas de ~3 s).

## Fórmulas e aproximações (chaves em `docs/domain/civilization-observatory.md`)

- **Expectativa de vida ao nascer** (`LifeTable`): faixas de 5 anos, última aberta. `m = mortes/exposição`;
  `q = n·m/(1 + n·m/2)`; `l₀ = 1`, `lᵢ₊₁ = lᵢ(1 − qᵢ)`; `Lᵢ = n(lᵢ + lᵢ₊₁)/2`, aberta `L = l/m`; `e₀ = ΣL`.
  Exposição da faixa = vivos no fim do ano + metade das mortes dela. Só materializados têm idade (o pool não tem
  idade nem morre), então pool fica fora de idade e de e₀. Aproximação: nascidos no ano contam como ano inteiro.
- **Gini** (`WealthInequality`): Lorenz trapezoidal ponderado; carteiras com peso 1 e o pool como UM bloco uniforme
  (`WealthSum/Count` por cabeça): subestima a desigualdade interna do pool.
- **Causas**: `Starvation`+`Dehydration` = fome/sede; `Disease`; `Death` = idade (inclui combate); resto = outras
  (exaustão, morte materna). Só eventos com `SourceSystem` `NatalitySystem`/`NpcDeath` (fauna reusa os kinds).
- **Migração líquida** = Δpopulação − nascimentos + mortes. É resíduo por definição; o teste 3 a torna falsificável
  (nunca positiva) e o 4 prova que nascimentos e mortes não vêm do próprio resíduo.
- **Desemprego**: adultos materializados (por `LifeStageRules`) sem `Employer`; o pool fica fora.
- **Fundadas**: cidades com `FoundedAtTick` no ano; **fundidas**: eventos `CityMerged`. Merge: o que a cidade
  absorvida contou no ano vai para quem a absorveu.

## Desempenho

O observatório roda uma vez por ano, fora do tick: custo por NPC-tick do motor inalterado (não toca o loop).
`compare-simulation-cost.sh` NÃO foi rodado de propósito: há outras worktrees ativas e a medição sairia com ruído;
o orquestrador mede uma vez ao final. Tempo de parede não é critério agora (inflado por concorrência).

## Fora de escopo

CLI e JSON (fatia B), diversidade (C), cenário de referência (D), `CityIndicators` (E). Cultura, organizações,
governo e crime entram como chaves novas quando as sub-issues existirem.

## Correções após a verificação independente (F1 a F4)

- **F1**: a conservação agora usa fonte independente do resíduo: população da série (`population_delta` contra a
  diferença de `population`), braço sem emigração (`net_migration` = 0) e braço com emigração real (pool de 50, taxa
  alta) em que `net_migration` = −(ids que saíram do pool sem virar NPC nem morto). `CivilizationConservationTests`.
- **F2**: `cities_founded` conta em `world.Cities` (antes só ativas; cidade fundada e fundida no mesmo ano sumia).
  Teste: Σ fundadas + iniciais = total de cidades; Σ fundidas = cidades com `MergedIntoCityId`. O baseline de
  diversidade não usa essa chave (usa `cities_active`), então **não foi regravado**.
- **F3**: testes de valor para causas, filtro de `SourceSystem`, idade, desemprego, oficinas, prédios, e₀ e fundidas
  (`CivilizationObserverValueTests`). `life_expectancy_at_birth` agora é 0 em ano sem mortes (código e doc de acordo).
- **F4**: `ArgumentOutOfRangeException` testada para seeds e anos ≤ 0.

## Poder de teste (última rodada)

22 mutações aplicadas à mão, uma a uma, todas reprovadas por ao menos um teste: troca disease/age e fome/sede;
reatribuição de fusão; Gini sem pool e com peso 1; `population_pool` zerado; exposição `alive + deaths`; oficinas e
prédios do mundo inteiro; filtro de adultos; tecnologias (mundo, cidade e chaves da diversidade); `population_unassigned`
zerado; `age_mean` zerado; tesouro do mundo; `cities_active`; ano não incrementado; nascimentos não somados na fusão.
Rodada seguinte: mais 23 mutações reprovadas (teto de `q` na tábua de vida, guardas de exposição e de faixa aberta,
idoso fora do denominador, guardas de Gini e de tesouro per capita, e todo campo de `CivilizationReport` e de
`summary`: cenário só com nome do arquivo, `tick`, `final` min/média/máx e último ano, ordem das seeds, números não
finitos, `seeds`/`years`, seed inicial, criação do diretório, guardas de argumento, e a ordem dos argumentos do modo
`civ-report` no `Program.cs`). Testes novos: `CivilizationScopeTests` (mundo de 3 anos com cidade B, pool, prédios, oficina, tecnologias, empregos,
fusão, NPC fora de cidade ativa) e casos por métrica em `TrajectoryDiversityTests`.
