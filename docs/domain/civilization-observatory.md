# Observatório de civilização (issue #112)

Mede se o mundo está virando uma civilização: roda N seeds por X anos, sem persistência, e amostra o mundo
ao fim de cada ano. **Só lê**: o hash canônico é o mesmo com e sem observatório. Código em
`src/LivingWorld.Simulation/Observation/` (`CivilizationObserver`, `YearEventTally`, `LifeTable`,
`WealthInequality`) e `src/LivingWorld.Workers/CivilizationReportCommand.cs`.

## Como rodar

```
bash scripts/civ-report.sh [cenário] [seed-inicial] [seeds] [anos] [saída]
# padrão: scenarios/default.json 1 20 10 artifacts/civ-report.json
```

Seeds consecutivas (`seed-inicial` + i) rodam em paralelo; cada uma tem mundo, relógio e sink próprios. A saída
é byte-idêntica para a mesma entrada (sem data nem duração). 1 ano = `Calendar.HoursPerYear` ticks (8640 no
calendário padrão de 12 meses × 30 dias × 24 horas).

## Formato do JSON

```
{ "scenario": "default.json",
  "seeds": [ { "seed": 1, "years": [
      { "year": 1, "tick": 8640, "world": { metric: número },
        "cities": [ { "id": "<guid>", "metrics": { metric: número } } ] } ] } ],
  "summary": { "seeds": 20, "years": 10,
               "final": { metric: { "min", "mean", "max" } } } }
```

Contrato congelado em `tests/contracts/shape.civ-report.json`. `final` resume o último ano do mundo entre as seeds.

## Métricas (cidade e mundo)

| Chave | Significado |
| --- | --- |
| `population`, `population_materialized`, `population_pool` | vivos: materializados + pool agregado |
| `births`, `deaths` | do ano, só NPC (sink de eventos `NatalitySystem` e `NpcDeath`) |
| `deaths_hunger_thirst`, `deaths_disease`, `deaths_age`, `deaths_other` | por causa; somam `deaths` |
| `age_mean`, `age_median` | idade dos materializados (o pool não tem idade) |
| `life_expectancy_at_birth` | tábua de período do ano; 0 = indefinida; ruidosa em população pequena |
| `gini` | carteiras materializadas + pool como um bloco uniforme |
| `treasury` | tesouro da cidade (soma no mundo) |
| `unemployment_rate` | adultos materializados sem `Employer` / adultos materializados |
| `workshops`, `buildings` | oficinas e prédios da cidade |
| `technologies` | tecnologias adotadas (no mundo: com ao menos uma cidade adotante) |

Só no mundo: `cities_active`, `cities_founded` (fundadas no ano), `cities_merged` (eventos `CityMerged`),
`population_delta`, `net_migration` (= Δpopulação − nascimentos + mortes, resíduo; emigração do pool), e
`population_unassigned` (vivos fora de cidade ativa; deve ser 0).

Extensão: cultura, organizações, governo e crime entram como chaves novas (`metric → valor`), sem migração.

