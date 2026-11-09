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

## Diagnóstico (sondas descartadas; nada de produção, cenário ou golden alterado)

**Veredito principal: bug de fiação do loader, não calibração.** `ScenarioLoaderV2.LoadWorld` (`ScenarioLoaderV2.cs:55-72`)
não passa `familyRules`, `perfRules`, `animalSpeciesRules`, `plantSpeciesRules` nem `biomeSeasonTemperatureRules` ao
`WorldState`, que cai em `FamilyRules.Disabled` (`WorldState.cs:511,700`; `FamilyRules.cs:181-186` diz "nunca usado por um
cenário real"). `ScenarioRunner.Create` passa todos (`ScenarioRunner.cs:326-331`). Medido (100 NPCs, mesmas seeds 1 a 3):

| Mundo | mortos no ano 1 | nascidos até o ano 10 | vivos no ano 10 |
| --- | --- | --- | --- |
| `LoadWorld` + test-civilization | 39, 45, 54 | 19, 17, 10 | 37, 36, 36 |
| `ScenarioRunner.Create(100)` | 22, 13, 23 | 36, 41, 28 | 97, 97, 87 |

Os baselines que sustentam 100 anos (`gate-village-population`, `live-default-population`, AD-082/AD-093) usam `Create`:
a extinção do caminho JSON é antiga (nunca coberto), não regressão. Não atribuí qual das fiações ausentes pesa mais.

**(b) Fecundidade: a regra está certa; é consequência de (a).** Regra: `NatalitySystem.cs:34-58` (idade fértil,
cônjuge vivo, piso de saúde/relacionamento/despensa em `:197-222`, sorteio contra `AnnualConceptionChance` 0,25).
Funil medido no fim do ano (default 20 NPCs e test-civ 100, 3 seeds, anos 1 a 12): mulheres férteis, casadas, coabitando,
saúde, relacionamento e despensa passam em quase todas; o estreitamento é o número de casais (default 1 a 4; test-civ
8 a 12 no ano 1, 3 a 4 no ano 20) e a despensa em ~20%. Candidatas somadas anos 1-11 × 0,25 contra nascimentos
(test-civ seed 1: 18 previstos, 21 obs.; seed 2: 15 previstos, 22 obs.; default seed 2: 7 previstos, 9 obs.): **bate**,
sem condição oculta. Com poucos casais e coorte envelhecendo (idade média +1 por ano) a população não repõe.

**(a) Fome: parte do cenário, parte inconclusiva.** Mortos de fome/sede no ano 1: 34 a 43 em 100 (default: 0 a 6 em 20).
Comida existe (estoque de oficinas 3000 a 4000, carteiras 9000+), mas casas ficam sem despensa e morrem inclusive
empregados com carteira cheia; distância casa-mercado não explica (mortes em todas as faixas, 3 de 4 a 0-2 células).
Buffer inicial ×6: mortes caem para 8 a 14 em 100 (default seed 1 ainda perde 2), então o bootstrap de um mês
(`ScenarioRunner.Seeding.cs:92-105`, 50 por membro) é curto para 100 NPCs e o laço compra/trabalho não o substitui a tempo.
Faltam 55 vagas para 72 adultos no cenário. Inconclusivo se a causa final é cenário (vagas/estoque) ou o laço de compra.

## Menor mudança por causa

- Fiação: passar no loader os mesmos padrões do `Create` (família, perf, fauna, flora, temperatura). Altera o hash de
  todo mundo carregado por JSON, logo os pins/goldens que usam cenário JSON (testes de `Periods/` e `Emergence/`)
  precisariam de regravação com AD; `GoldenHashesTests` usa `Create` e não muda.
- Fome do cenário novo: vagas ≥ adultos e estoque inicial por casa maior (dados de `test-civilization.json`; sem golden).

## Pendente

Depois da fiação: teste 20 seeds × 300 anos (população > 0 em ≥ 1 cidade, ninguém acima de `MaxLongevityYears`, moeda
conservada, relatório como artefato), medir 20 seeds, baseline de diversidade do cenário (hoje só o gate do `default.json`:
0,26875), docs de uso em `docs/domain/civilization-observatory.md`.
