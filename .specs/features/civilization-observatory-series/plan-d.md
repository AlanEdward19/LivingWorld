# Observatório de civilização: cenário de referência (issue #112, fatia D)

Status: **PARADA, achado do motor** (extinção sistemática). Teste de 20 seeds × 300 anos NÃO escrito (ver abaixo).
Continuação de `plan.md`.

## Entregue

- **F8**: `CivilizationReport.WriteSummary` soma `final.*.mean` em ordem de seed (teste
  `Summary_does_not_depend_on_the_input_order_of_the_runs`, falha sem o fix). Saída da CLI inalterada.
- `scenarios/test-civilization.json`: 100 NPCs (vale 50, colina 30, costa 20), mapa 24x24, 3 assentamentos em regiões
  distintas com recursos desiguais (vale: lavoura de 16 vagas; colina: lavoura de 5, mais oficina e pedreira; costa:
  lavoura de 8, sem pedreira), economia, cidades, emergência e aparência herdada ligadas. `default.json` e goldens intocados.

## Módulos que NÃO dá para ligar por JSON hoje

Doença (`DiseaseRules`), clima (`ClimateRules`) e furto (`TheftRules`) só entram por `ScenarioRunner.Create`:
`ScenarioLoaderV2.LoadWorld` não lê bloco algum para eles. Hospedagem (`LodgingSystem`) já roda sempre, sem regra.
Criar os blocos é mudar o formato público do cenário: decisão do dono (modelo: `EmergenceScenarioLoader`).

## Medidas (seed 1, Release, `dotnet Workers.dll civ-report`, máquina dividida com outros agentes)

| Corrida | Parede | RSS máximo | Pegada de pico |
| --- | --- | --- | --- |
| 1 seed × 100 anos | 28,9 s | 88 MB | 71 MB |
| 1 seed × 300 anos | 37,6 s | 90 MB | 78 MB |

O 300 anos engana: a seed 1 está extinta desde o ano ~80, então 220 anos custam quase nada. Com população viva o custo
é ~0,3 s/ano nesta máquina (100 anos = 28,9 s com ~25 vivos em média). 20 seeds × 300 anos em paralelo não foi medido
de propósito: o horizonte só vale a pena medir depois de resolvida a extinção.

## Categoria do teste (decidida, ainda sem teste)

`verify.sh --full` roda `Category=Scenario` (Unit) e LongRunning **sem** `Category=Heavy`; só `--nightly` roda
`test.sh --project longrunning --heavy --filter "Category=Heavy"`. O teste vai em `tests/LivingWorld.Tests.LongRunning`
com `[Trait("Category","Heavy")]`: é a única que não pesa no `--full`. `Scenario` (como diz a issue) pesaria nele.

## Achado: extinção sistemática (não consertado, não afrouxado)

Nascimentos ficam abaixo das mortes e a população some, inclusive no `default.json` (não alterado por esta fatia).

| Cenário | Seed | Evidência |
| --- | --- | --- |
| default (20 NPCs) | 2 | ano 1: 20; ano 40: 4; ano 100: 0 |
| default | 1, 3, 4 | ano 100: 1, 3, 3 vivos (de 20) |
| test-civilization (100) | 2 | ano 1: 55 (45 mortes, 43 fome/sede); ano 40: 13; ano 80: 0 |
| test-civilization | 4, 5, 6 | extintas até os anos 80, 80 e 60; seed 3 com 8 no ano 100 |
| test-civilization | 2 | 100 anos: 36 nascimentos contra 136 mortes; idade média sobe ~1 por ano (coorte envelhece) |

Duas causas separadas, a investigar antes de qualquer assert: (a) mortalidade de fome/sede no ano 1 no cenário novo
(43% da população; provavelmente ajuste do cenário: estoque inicial para 100, não é do motor); (b) fecundidade efetiva
muito abaixo da reposição (1 a 2 nascimentos por ano com 40 vivos, `AnnualConceptionChance` 0,25), que mata também o
`default.json`. A (b) é o achado do motor: decidir se é calibração (`AnnualConceptionChance`, pareamento, gestação) ou bug.

## Pendente

Teste 20 seeds × 300 anos (asserts: população > 0 em ≥ 1 cidade, ninguém acima de `MaxLongevityYears`, moeda conservada,
relatório gravado como artefato); baseline de diversidade do cenário em `tests/baselines/civilization-diversity.json`
(hoje só o gate do `default.json`, não alterado: índice 0,26875); docs de uso em `docs/domain/civilization-observatory.md`.
