# ADR-0019: Cidade fundida permanece como tombstone histórico

- **Status**: aceito
- **Data**: 2026-08-23
- **Decisores**: Alan

## Contexto

O FixT18 de `dynamic-city-growth` mostrou que uma cidade-filha fundada junto aos bounds da
cidade-mãe continua competindo com ela em `MigrationSystem`. A população oscila entre dois
registros que representam, espacialmente, um único assentamento.

Apagar a filha resolveria a competição, mas quebraria referências canônicas antigas: fatos,
relatos, portais e descendentes ainda podem apontar para seu `CityId`.

## Decisão

Uma filha cujo `FoundedFromCityId` resolve para uma mãe ativa agenda fusão quando a distância de
Chebyshev entre seus bounds crescidos é `<= AbsorptionRingCells`. O evento usa a cadência mensal
de `SpatialSettlementFoundingSystem`, espera `OrganizationTicks` e revalida a geometria ao
disparar. Se a adjacência sumiu, cancela sem mutar o mundo e pode tentar novamente no futuro.

Na confirmação:

- prédios, workplaces, households, NPCs, estoque, obras e população agregada passam para a mãe;
- migrações cujo destino era a filha são redirecionadas e encerradas quando origem e destino
  passam a ser a mesma cidade;
- a filha recebe `MergedIntoCityId` e deixa de integrar decisões e projeções;
- o registro permanece em `WorldState.Cities`, portanto referências históricas continuam válidas;
- IDs antigos resolvem operacionalmente para a cidade ativa final, inclusive em cadeias de fusão;
- `CityMerged` é registrado no event log no tick da confirmação.

`WorldState.ActiveCities()` é a visão operacional. `WorldState.Cities` continua sendo a coleção
canônica completa, não uma lista apenas de cidades vivas.

## Alternativas consideradas

- **Apagar a filha** — rejeitada porque criaria referências históricas órfãs.
- **Manter dois registros ativos e apenas mover moradores** — rejeitada porque a filha vazia
  continuaria candidata a migração, construção e projeção.
- **Fundir imediatamente ao detectar adjacência** — rejeitada porque uma aproximação transitória
  não deve apagar a autonomia do assentamento; reutilizar `OrganizationTicks` mantém o mesmo
  contrato temporal da fundação.

## Consequências

- A identidade histórica é preservada sem manter competição causal entre as cidades.
- Consumidores que tomam decisões ou projetam o presente devem usar `ActiveCities()`.
- `MergedIntoCityId` e `MergeScheduledAtTick` são estado canônico e sobrevivem a snapshot.
