# Lodging and rent [Estado: Implementado (2026-10-13); verificação independente pendente]

## Problem

Só os domicílios semeados têm moradia (`Household.Dwelling`); uma casa abriga N domicílios pelos andares
(city-building-separation, AD-104) sem dono nem preço. Prédios construídos para moradia (`HousingCapacityProvided`)
só contam no agregado de habitação: ninguém mora neles. Não há estalagem, taverna com quartos, cortiço nem hotel,
e perder a moradia nunca acontece.

Quando isto entrar, um cenário pode declarar prédios de hospedagem; domicílios sem teto alugam uma vaga, o
negócio dono do prédio recebe o aluguel mensal, e quem não paga perde a vaga, dorme no chão e pesa na migração.
Cenário sem hospedagem (medieval) não muda de comportamento.

## Flow

Dados no cenário, nada por época no código; reaproveita dono, tesouro, carteira e migração que já existem.

1. `BuildingRecipe.Lodging` (new, optional) - `RentPerMonth` e `GraceMonths`; vagas = `HousingCapacityProvided`
2. `DwellingCapacity` (new) - vagas de um prédio: receita com habitação, senão andares (casa semeada)
3. `LodgingSystem` (new, Monthly, após `WagePaymentSystem`) - cobra, despeja, aloca
4. `Household.RentArrearsMonths` e `EvictedAtTick` (new) - atraso e perda de moradia
5. `RestPlaceResolver` (exists) - sem teto (despejado) descansa no chão
6. `MigrationSystem.NeedsMigration` (exists) - despejado sem vaga na cidade pode migrar
7. `PopulationSeeder` (exists) - vagas por `DwellingCapacity`
8. `PricingTerms` + `BuildingValuation` (new) - valor do imóvel por local e demanda; aluguel e diária seguem o valor
9. `GuestLodging` (new) + `RestPlaceKind.Lodging` - hóspede de diária longe de casa
10. `CityBuildingMarker.Value/Lodging` (exists, +campos) e cliente Godot - preço, vagas e leitos visíveis

## Impact

| Front | What changes |
| --- | --- |
| domain | termos: hospedagem, vaga, locador, despejo |
| stored data | `Household` ganha 2 campos (vazios); evento `RentUnpaid`, `Evicted` no fim de `WorldEventKind`; hash canônico muda uma vez. Preço e diária não guardam nada (derivados do mundo) |
| public contract | `shape.city-subscribe.json` ganha `value` e `lodging` por prédio (aditivo; cliente antigo ignora) |

## Landing

| One-way door | Literal shape | Alternative rejected |
| --- | --- | --- |
| 1. campo opcional no cenário | `BuildingRecipes.<id>.Lodging = {RentPerMonth, GraceMonths}` | lista fixa de tipos "hotel/taverna" no código: quebra mundo medieval e toda época |
| 2. locador = oficina do prédio | aluguel credita `Workplace.Treasury` da oficina com `Building` igual ao prédio | dono pessoa: herança e falência já existem na oficina (`EstateSystem`) |

- Prédio de hospedagem sem oficina não cobra (moradia própria). Aditivo: cenário antigo carrega igual.

## Criteria

### S1: Aluguel (P1)

1. WHEN o mês vira THEN each household com `Dwelling` num prédio com `Lodging` e oficina SHALL pagar
   `RentPerMonth` da carteira do chefe para o tesouro da oficina, sem criar nem perder moeda
2. IF o chefe não tem o aluguel THEN the system SHALL somar 1 a `RentArrearsMonths` e registrar `RentUnpaid`
3. IF o atraso passa de `GraceMonths` THEN the system SHALL despejar: `Dwelling` nulo, `EvictedAtTick` = tick, `Evicted`

### S2: Vaga e consequência (P1)

4. WHEN há household sem `Dwelling` com carteira ≥ aluguel e vaga na cidade THEN it SHALL ocupar a vaga mais barata
   (desempate por id), sem passar de `HousingCapacityProvided`, e `Location` vira a célula interna do prédio
5. WHILE despejado THEN the household SHALL descansar com a eficiência do chão
6. IF despejado e sem vaga na cidade THEN `MigrationSystem` SHALL tratá-lo como precisando migrar
7. WHERE o cenário não declara `Lodging` THEN the system SHALL não mudar nada (hash do cenário padrão igual
   ao anterior, fora os 2 campos vazios)

### S3: Preço dinâmico (P1)

10. WHERE o tipo declara `Pricing` THEN value = `BaseValue` × (1 + peso×centralidade + peso×comércio por perto + peso×beira d'água)
    × (1 + sensibilidade×(pressão−1)), dentro de [Min, Max]; aluguel = valor × `RentYieldPerMonth`
11. The pressão SHALL ser, em hospedagem, quem precisa de teto por vaga da cidade; em estabelecimento, população por vaga de trabalho
12. The system SHALL cobrar o aluguel já com o preço de hoje; sem `Pricing` o aluguel segue fixo

### S4: Hóspede de diária (P1)

13. WHEN o NPC está a mais de `GuestMinDistance` células de casa e existe hospedagem mais perto com vaga e preço ao seu alcance THEN
    it SHALL dormir lá (`RestPlaceKind.Lodging`) e pagar a diária ao locador ao fim da noite, sem criar nem perder moeda
14. IF não há `NightlyRate`, vaga ou dinheiro THEN the NPC SHALL dormir como antes

### S5: Visual (P1)

15. WHEN o cliente mostra um prédio com `Lodging` ou `Pricing` THEN it SHALL exibir vagas, aluguel, diária e valor de hoje
16. WHEN o interior de uma hospedagem abre THEN the client SHALL colocar um leito por vaga (até o espaço livre)

### S6: Dados, determinismo, custo (P1)

17. IF `RentPerMonth` <= 0, `GraceMonths` < 0, `Pricing` incoerente ou `NightlyRate` sem `GuestMinDistance` THEN the loader SHALL recusar a receita
18. The system SHALL produzir o mesmo mundo para a mesma seed (sem RNG, iteração por id)
19. The system SHALL custar zero sem `Lodging`/`Pricing` e manter criar mundo de 6000 NPCs em até +5%

**Independent test:** `bash scripts/test.sh --filter LodgingSystemTests`.

## Out of scope

| Excluded | Why |
| --- | --- |
| compra e venda de imóvel | o valor só alimenta aluguel e diária; não há mercado de venda |
| concorrência entre locadores | o preço reage à demanda da cidade, não ao preço do vizinho |
| planta própria de hospedagem | leitos usam o piso livre da planta do tipo (poucos em casa 3x3) |

## Sources

- `.specs/features/city-building-separation/plan.md`, `PopulationSeeder`, `WagePaymentSystem`, `MigrationSystem`
