# Plano lean: indicadores da cidade honestos (#112, fatia E)

Status: implementado; verificação em `verification.md`. Contrato: ADR-0042, AD-123.

## Problema
`CityIndicators` (rota `/visual/subscribe?scope=City`) exibia números que não medem o que o nome diz:
`Health` era a SOMA da saúde (cresce com a população) e `Security`/`Education`/`Infrastructure` eram a
contagem de prédios. O observatório (#112) e o cliente precisam de números com significado.

## Decisões do dono (contrato público autorizado)
- `Health` vira MÉDIA por morador vivo, materializados + pool como massa (coerente com `Population`).
  Sem campo `HealthMean`. Cidade sem vivos: 0 (mesma convenção de `Inequality`).
- `Security`, `Education`, `Infrastructure` ficam nulos ("não medido") até #118, #122 e #124.
- `Economy`, `Housing`, `Inequality` fora de escopo (não enganosos pelo texto da fatia).
- Cliente Godot: "—" para nulo; `Health` tratado como média.

## Fluxo
`CityPopulationQuery.Health` (média) e `HealthSum` (soma interna) -> `CityProjector` -> `CityIndicators`;
o mesmo `Health` alimenta o indicador `health` do delta (`LivingScopeState.BuildIndicators`).
`CityIndicatorSources` (produção) declara a fonte de cada indicador.

## Critérios (EARS)
1. QUANDO a cidade tem materializados vivos e pool, `Health` = (soma dos vivos + `HealthSum` do pool) /
   `Population`, em divisão real (321/5 = 64,2; pool só: 250/4 = 62,5).
2. MORADOR morto que ainda está em `world.Npcs` NÃO entra no numerador nem no denominador.
3. CIDADE sem vivos: `Health` = 0.
4. O indicador `health` do delta de escopo é igual a `CityPopulationQuery.Health` (cidade com > 1 morador).
5. TODO campo de `CityIndicators` tem fonte em `CityIndicatorSources`; fonte medida aponta consulta
   existente; não medida tem issue e campo anulável e vale `null` mesmo com prédios.
6. `tests/contracts/shape.city-subscribe.json` tem `security?`, `education?`, `infrastructure?`.
7. Cliente: indicador nulo ou ausente mostra "—", nunca 0; `Health` com uma casa decimal.
8. Hash canônico e goldens não mudam (nenhum sistema consome os valores).

## Portas de mão única
Tipo de `Health` (long -> double) e três chaves novas na rota de cidade: autorizadas pelo dono.

## Mutantes esperados (todos devem reprovar)
Divisão inteira; denominador só materializados; numerador sem `IsAlive` (M7a); denominador contando
mortos (M7b); `new("health", HealthSum)` no delta (M11); remover entrada de `CityIndicatorSources`.

## Desempenho
Cliente: `client-stress` 3 rodadas antes/depois em `client/godot/README.md` § Desempenho.

## Fora de escopo
Gini com o pool (#112 fatia A); fontes reais de segurança, educação e infraestrutura.
