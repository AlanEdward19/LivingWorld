# Fusão de cidade transfere a adoção de tecnologia [Estado: Implementado, aguardando verificação independente (#114)]

## Problem

`WorldState.MergeCityInto` move prédios, casas, NPCs, estoque, obras, tesouro e pool da filha para a mãe, mas deixa
`Technology.AdoptedBy` apontando a filha (lápide). Efeitos: `Mobility.BonusPct` some sem causa para os moradores que
vieram da filha (`tech.AdoptedBy.Contains(npc.City)`), a lápide nunca difunde (`TechDiscoverySystem.Spread`) e
`CultureTechnologyQuery` conta a lápide como cidade adotante.

## Decisão assumida (pendente do dono)

A fusão TRANSFERE a adoção (padrão proposto na #114): quem sabe a técnica mora agora na mãe. Registrada como AD-122 em
`docs/decisions-log.md`. Se o dono preferir que a mãe reaprenda por difusão, basta o handler não aplicar a troca.

## Flow

1. `SpatialSettlementFoundingSystem.HandleEvent` chama `world.MergeCityInto(daughter, mother)`.
2. `MergeCityInto` (após mover o resto) percorre as tecnologias por índice (ordem determinística) e, onde `AdoptedBy`
   contém a filha: remove a filha, acrescenta a mãe se ainda não estava (sem duplicar), via `ReplaceTechnology`.
3. Devolve os índices que a mãe passou a adotar; o handler loga `CityMerged` e, para cada índice, `TechnologyAdopted`
   (`SourceSystem` do sistema de fundação, `CauseEventId` = id do `CityMerged`).

## Criteria (EARS)

- C1: WHEN a filha adotou uma tecnologia com ritmo > 0 e a mãe não, AFTER the merge `Mobility.BonusPct(mãe) > 0` e
  `AdoptedBy` não contém a filha. Teste: `HandleEvent_transfers_technology_adoption_from_daughter_to_mother_with_causal_event`.
- C2: WHEN a mãe já adotava, `AdoptedBy` perde só a filha, sem duplicata e sem novo `TechnologyAdopted`.
  Teste: `HandleEvent_drops_the_tombstone_without_duplicating_when_mother_already_adopted`.
- C3: WHEN a mãe herda, `TechnologyAdopted.CauseEventId` = id do `CityMerged` (mesmo teste de C1).
- C4: Nenhuma lápide em `AdoptedBy` a cada dia por 10 anos no `scenarios/default.json` (emergência e fundação ligadas),
  3 seeds. Teste: `MergedCityAdoptionScenarioTests` (LongRunning). Nota: nesses seeds há fusões (1 a 5) mas nenhuma
  tecnologia é descoberta em 10 anos, então C4 sozinho é fraco; a prova causal é C1 a C3.
- C5: Determinismo e goldens inalterados: sem estado novo, só troca de elemento de lista existente; `OriginCity` intacto.

## Campos com CityId conferidos (Domain e WorldState)

- Movidos pela fusão (já existiam): `Building.City`, `Workplace.City`, `Household.City` e `PendingRelocationCity`,
  `Npc.City`, `ConstructionProject.City`, estoque, tesouro, pool.
- Corrigido aqui: `Technology.AdoptedBy`.
- Mantidos de propósito (história): `Technology.OriginCity`, `City.FoundedFromCityId`, `City.MergedIntoCityId`,
  `TreasuryAggregate.City` (série histórica da filha), `City.CanonSlots`/`ReportState.CommunityId` (vivem na própria
  lápide), `Fact.Location`, `WitnessedAccount.Location`, `NarrativeDraft.Location`.
- `NpcScope.CityId` é derivado de `Npc.City` em tempo de consulta.

## Fora de escopo

Perda de conhecimento quando quem sabe morre (#122).

## Desempenho

Custo extra só no instante da fusão (evento raro): uma varredura linear das tecnologias. Nada por tick. Medição de
`compare-simulation-cost.sh` NÃO rodada aqui (outras worktrees em execução geram ruído); o orquestrador mede ao final.
