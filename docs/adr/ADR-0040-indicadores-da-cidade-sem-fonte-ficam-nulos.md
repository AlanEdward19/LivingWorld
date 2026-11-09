# ADR-0040: Indicadores da cidade: saúde é média; o que não tem fonte fica nulo

- **Status**: aceito
- **Data**: 2026-11-09
- **Decisores**: dono do projeto (issue #112, fatia E; mudança de contrato público autorizada)

## Contexto
`CityIndicators` (`CitySnapshot.indicators`, rota `/visual/subscribe?scope=City`) mostrava números que não
medem o que o nome diz. `Health` era a SOMA da saúde dos moradores: crescia com a população e dava a
impressão de cidade mais saudável. `Security`, `Education` e `Infrastructure` (`CityPopulationQuery`) eram
a contagem de prédios concluídos, a mesma para os três (`SPEC_DEVIATION` antigo). O observatório de
civilização (#112) e o cliente precisam de números que signifiquem o que dizem.

## Decisão
- `Health` passa a ser a **média** por morador vivo, na mesma massa de `Population`: (saúde dos
  materializados + `HealthSum` do pool) / (materializados + pool), em divisão real. Sem nenhum vivo: 0
  (mesma convenção de `Inequality`). Não existe `HealthMean`; o tipo vai de `long` para `double`
  (também no indicador `health` dos deltas de escopo). A soma fica interna: `CityPopulationQuery.HealthSum`.
- `Security`, `Education` e `Infrastructure` entram em `CityIndicators` como `long?` e são sempre `null`
  ("não medido") até #118, #122 e #124 darem fonte real. `CityPopulationQuery` perde os três métodos
  baseados em contagem de prédios. Nenhum sistema da simulação os consumia (verificado por busca em
  `src/`, `tests/` e `client/godot/`); mundo e goldens não mudam.
- Cada indicador declara sua fonte em `CityIndicatorSources` (produção). Teste por reflexão reprova
  indicador sem fonte, fonte órfã, fonte "medida" sem consulta existente e "não medida" sem campo anulável.
- `Economy` (= `Wealth`) e `Inequality` (só carteiras materializadas) ficam como estão: não eram parte
  do texto da fatia; a revisão deles segue na #112 fatia A (Gini com o pool) se o dono quiser.

## Alternativas consideradas
- Acrescentar `HealthMean` e manter a soma: duas métricas de saúde, a enganosa continuaria exposta.
- Manter a contagem de prédios sob outro nome: continuaria sem medir segurança, educação ou infraestrutura.

## Consequências
- **Positivas**: nenhum número finge medir o que não mede; fonte nova obriga a declarar.
- **Negativas / trade-offs**: `health` muda de escala (0-100 em vez de soma) e de tipo; cliente precisa
  tratar `null` ("—"). `tests/contracts/shape.city-subscribe.json` regravado.
- **Follow-ups**: #118, #122 e #124 trocam `NotMeasured` por `Measured` ao entregar a fonte.
