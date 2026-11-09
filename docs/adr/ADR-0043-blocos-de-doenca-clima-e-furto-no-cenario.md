# ADR-0043: Blocos `Disease`, `Climate` e `Theft` no cenário JSON

- **Status**: aceito
- **Data**: 2026-11-09
- **Decisores**: dono do produto (pedido da fatia D da #112), implementação por agente

## Contexto
Doença (`DiseaseRules`), clima (`ClimateRules`) e furto (`TheftRules`) existem no motor, mas só entravam por
`ScenarioRunner.Create`; `ScenarioLoaderV2.LoadWorld` não lia bloco algum, então nenhum cenário JSON podia ligá-los.
O cenário de referência de séculos (#112 D) precisa deles. É mudança do formato público do cenário (AGENTS.md: perguntar).

## Decisão
Três blocos opcionais na raiz do JSON, lidos por `WorldRulesScenarioLoader`, no padrão do bloco `Emergence`:
bloco ausente devolve o `Disabled` da regra (mundo e hash como antes); bloco presente exige `Enabled` (bool).

- `Disease`: `OutbreakPopulationThreshold` (int >= 1), `OutbreakDailyChance` ([0,1]) e `Diseases[]` de
  `{ Id, ContagionPerContactDay [0,1], Lethality [0,1], DurationDays >= 1, ImmunityDays >= 0 }`, ids únicos e
  ao menos uma doença quando ligado. Doença é vetor de eixos, sem nome no código (ADR-0013).
- `Climate`: `MinMilli` (>= 0) e `MaxMilli` (>= `MinMilli`); 1000 = ano médio. Padrão `Disabled` (1000/1000).
- `Theft`: `DesperateBelow` [0,100]; `BaseSuccess`, `DiscoveryChance`, `MaxLootFraction` e `GuardEffect` em [0,1];
  `PersonalityThreshold` sem limite validado (padrão 1,1); `GuardProfessionId` (-1 = sem guarda). Campo ausente usa o valor de `TheftRules.Disabled`.
  O furto só acontece se o catálogo de ações do cenário também declara `"Steal"` em `MaxDurationHours`.

Os sistemas continuam registrados sempre e checam `Enabled`, como no `Create`. Erro de campo vira `Result.Fail`
com o caminho (`Theft.BaseSuccess: fora de [0,1]`).

## Alternativas consideradas
- Um bloco único `World` — mistura três regras com ciclos de vida distintos; o padrão do projeto é um bloco por regra.
- Ligar por parâmetro de `LoadWorld` — esconderia a regra fora do arquivo do cenário e dos pins por hash.

## Consequências
- **Positivas**: cenário de séculos liga as três regras; `default.json` e todo cenário sem o bloco ficam idênticos.
- **Negativas / trade-offs**: formato público maior; o contrato está em `period-authoring-schema.md`.
- **Follow-ups**: outros módulos de regra (cultura, organizações, governo) devem seguir o mesmo padrão.
