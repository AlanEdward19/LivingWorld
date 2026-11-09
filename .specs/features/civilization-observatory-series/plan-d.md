# Observatório de civilização: cenário de referência (issue #112, fatia D)

Status: **PARADA de novo** (extinção sistemática, mesmo com o loader corrigido). Teste escrito e vermelho de propósito.
Continuação de `plan.md`. Decisões do dono: 100 NPCs, 3 regiões habitáveis desiguais, 300 anos, loader corrigido, blocos novos.

## Entregue

- **F8**: `CivilizationReport.WriteSummary` soma em ordem de seed (teste falha sem o fix). CLI inalterada.
- **Loader** (`ScenarioLoaderV2.cs`): passa `DefaultFamilyRules`, `DefaultPerfRules`, fauna, flora e temperatura do `Create`.
  `WorldRulesWiringTests` compara `LoadWorld` e `Create` regra a regra por reflexão (mutações: tirar flora ou família reprova).
- **Blocos `Disease`, `Climate`, `Theft`** (`WorldRulesScenarioLoader.cs`, ADR-0042); ausentes = mundo e hash como antes.
  Furto também pede `Steal` em `MaxDurationHours`; sem profissão de guarda no cenário (`GuardProfessionId` -1).
- `scenarios/test-civilization.json`: 100 NPCs (vale 50, colina 30, costa 20), mapa 24x24 procedural, vagas 96 para ~72
  adultos, as três regras ligadas (doença rara e branda, clima 500 a 1400, furto), economia, cidades, emergência, aparência.
- Teste Heavy `ReferenceCivilizationTests` (LongRunning, `Category=Heavy`: só o `--nightly` roda; `--full` não): 20 seeds ×
  300 anos pelo `CivilizationRunner`, a cada ano população > 0 em ≥ 1 cidade, ninguém acima de `MaxLongevityYears`, moeda
  conservada (carteiras + oficinas + cofres + pools − cunhado + destruído); grava `artifacts/civ-report-reference.json`.
  Três pares de mutação rápidos (estado são passa, quebrado reprova) rodam no `--full`.

## Efeito de cada fiação (religação uma a uma, test-civilization, 3 seeds, 10 anos)

| Fiação | mortes ano 1 | nascidos 10 anos | vivos ano 10 |
| --- | --- | --- | --- |
| nenhuma / só perf / só fauna / só flora / só temperatura | 39, 45, 54 | 19, 17, 10 | 37, 36, 36 |
| só `FamilyRules` (= todas) | 38, 39, 75 | 21, 23, 3 | 67, 51, 20 |

Só a família move a população; as outras quatro têm efeito zero aqui (a ecologia inicial do `Create` não é semeada no JSON).

## Pins regravados (AD-125)

- `tests/baselines/civilization-diversity.json` Gate (default, 20 seeds × 10 anos): índice 0,26875 → 0,25448; cidades 0,5137 →
  0,5040; gini 0,2179 → 0,2455; população 0,2928 → 0,2556; tesouro/cabeça 0,3194 → 0,2673; tecnologias 0 → 0.
- `EconomyDepthScenarioTests.Lumberjack_is_paid...`: salário vale o da profissão × peso da criação (esperado 141 → 154).
- Intactos: `GoldenHashesTests` e todo o LongRunning (146 passam), Unit rápido (2548) e Scenario, Integration (449).

## Fome do ano 1 (com vagas ≥ adultos)

Mortes no ano 1 com o loader corrigido, 6 seeds: 18 a 39 em 100 (fome/sede 13 a 36, média 20%); `Create(100)`: 13 a 23.
Desemprego ~0, então vagas não explicam. Cidades desligadas: fome 10 a 33; limiares de fundação 0,9: 8 a 30; emigração
baixa: igual. Logo não é dinâmica de cidades. Com o buffer inicial ×6 (experimento) as mortes caem a ~10%: o buffer do
`ScenarioRunner.Seeding.cs:92-105` (50 por membro, cobre um mês) é curto e o laço compra/trabalho não o substitui a tempo;
é o mesmo no `Create`. Comprar exige o comprador estar no mercado (`BehaviorDecisionSystem.Effects.cs:209-216`); a casa
só repõe quando um membro vai lá. Não ajustei por força bruta.

## Extinção (números)

| Medida | Resultado |
| --- | --- |
| 20 seeds × 300 anos (Heavy) | 20 de 20 extintas: anos 70 a 155 (mediana ~120); pico de 76 a 134 vivos |
| seeds 14 e 19 | todos os 100 morrem no ano 1 (97 e 24+74 por fome/sede e exaustão) |
| 3 regras desligadas (3 seeds × 100 anos) | mesma queda (vivos ano 90: 5, 1, 0); logo não vem de doença, clima nem furto |
| sem doença / sem clima | queda mais lenta em doença, mesma em clima |

Funil de reprodução (seed 1, anos 30 a 50): mulheres férteis sobem (21 a 25) e solteiras de 10 para 19, casadas caem de 11
para 6, homens casados de 11 para 5; candidatas a concepção 7 a 11 antes, 5 a 6 depois (gestações 4 para 0 a 2). A regra de concepção bate
com o observado (ver diagnóstico anterior); o que estreita é a formação de casais: `CourtshipSystem` só considera
candidatos com `Relationship` prévia (AD-061) e rejeita parentes de 1º grau (`CourtshipSystem.cs:102-110`); com 5 a 9
cidades pequenas e coorte fechada sobram solteiros sem conhecidos fora da família. Hipótese, não medida por evento.

## Medidas (Release, máquina dividida, 8 núcleos)

| Corrida | Parede | RSS máximo |
| --- | --- | --- |
| 1 seed × 100 anos (seed 1, viva até ~70) | 96,6 s | 104 MB |
| 1 seed × 300 anos (seed 1, extinta no 127) | 135 s | 130 MB |
| 20 seeds × 300 anos (teste Heavy inteiro) | 1350 s (22,5 min), 4263 s de CPU | 556 MB |

Cabe no noturno em tempo (extintos custam menos que vivos: o total pode subir se o mundo se sustentar).

## Pendente

Resolver a formação de casais (e a fome do ano 1) para o mundo se sustentar; então gravar `Reference.Diversity` no baseline
(hoje só `Scenario/FirstSeed/Seeds/Years`), rodar o Heavy verde e documentar o uso em `docs/domain/civilization-observatory.md`.
