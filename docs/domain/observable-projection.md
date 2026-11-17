# Projeção do observável (ADR-0047 etapa 2, AD-170)

O cliente assiste o mundo pelo escopo assinado (`/visual/subscribe`, `/visual/ws`, `/visual/sse`): snapshot e depois delta por
tick, só com o que mudou. A projeção **só lê** o estado do motor (`src/LivingWorld.Api/Visual/Observable/`): sem RNG, sem estado
novo, sem mudar ordem de nada. Sem assinante o laço não projeta (`TickLoopService.PublishDeltas` sai antes).

## No escopo (snapshot `livingState` e delta)
| Onde | Campo novo | O que é |
|---|---|---|
| `npcs[]` / `npcUpserts[]` | `actionStartedAtTick`, `actionTarget`, `intent`, `intentTarget` | ação com início e alvo, intenção |
| `npcs[].observable` | `name`, `sex`, `ageYears`, `lifeStage`, `professionId`, `professionName` | quem é |
| idem | `heightCm`, `growthFraction`, `pregnancy{fraction,babies}` | corpo; gravidez só depois de `ShowsAfterDays` |
| idem | `illness{diseaseId,symptomatic,bedridden}` | infecção ativa |
| idem | `emotions{grief,fear,anger,joy,shame,loneliness}`, `dominantEmotion`, `mourning` | emoção em décimos; luto = tristeza ≥ 0,1 |
| idem | `carried[]{resourceId,quantity,resourceName}`, `held{itemId,designId}` | carga pessoal e recipiente |
| `cities[].density` (Mundo) | `inside`, `pooled`, `actions[]{action,count}`, `temperatureC` | quem está dentro, sem mandar cada um |
| `buildings[]` | `floors`, `groundFootprint[]`, `value`, `lodging` | planta completa: prédio novo desenha pelo delta |
| `buildingStates[]` | `typeName`, `function`, `workplaceId`, `locationTypeId/Name`, `ownerId`, `householdIds`, `residentIds`, `workerIds`, `stock[]`, `pantryUnits`, `closed` | função e ocupação |
| `crops[]` | `plot`, `resourceId/Name`, `status`, `stage`, `fraction`, `thirsty`, `sownHectares`, `arableHectares`, `standing`, `farmId`, `ownerId`, `expectedHarvest` | lavoura |
| `animals[]` (Cidade) | `species`, `position`, `energy`, `carriesDisease`, `predatorOf` | fauna individual |
| `faunaGroups[]` (Mundo) | `species`, `regionId`, `count`, `center` | fauna agregada |
| `leavings[]` | `location`, `deadId`, `householdId`, `heirHouseholdId`, `units`, `itemId`, `designId`, `fetcherId`, `carrierId` | espólio no chão |
| `objects[]` (Cidade) | `designId`, `householdId`, `holderId`, `location`, `forSale`, `marketId`, `rootShape`, `rootMaterial`, `qualityFraction` | peça inventada; partes em `/designs/{id}` |
| `climate` | `month`, `seasonIndex`, `seasonKey`, `temperatureC`, `regionId`, `cropYearFactorMilli`, `drought` | estação e clima do escopo |
| `processes[]` | `resourceId`, `resourceName` | recurso do processo de material |
| `events[]` | `eventId`, `involved{subjectId,otherId,authorId,victimId,relatedIds,householdId,buildingId,workplaceId,caseId,leavingId,designId,cityId,amount,outcome,recordOnly}` | envolvidos lidos do payload |

Delta: `buildingStateUpserts/Removed`, `cropUpserts/Removed`, `animalUpserts/Removed`, `faunaGroupUpserts/Removed` (chave
`espécie:região`), `leavingUpserts/Removed`, `objectUpserts/Removed`, `climate` (nulo = não mudou). Em `npcUpserts`, `observable`
**omitido = não mudou** (o NPC que só andou não reenvia). Camada `layers.Climate` passa a `isModeled: true` com
`{month, seasonIndex, seasonKey, regions[]{regionId, center, temperatureC, cropYearFactorMilli, drought}}`.

Crime: `involved` traz autor, vítima, caso e desfecho, sem descrição; violência sexual, doméstica e contra criança com
`recordOnly: true` (só registro, sem cena). O `label` existente não mudou. Posição do evento: célula do espólio; senão a pessoa
do primeiro campo quando ela é envolvida; senão sujeito, autor, vítima, casa, oficina ou prédio.

## Inspeção
`GET /entities/{kind}/{id}` → `{kind, id, name, state, links[]{relation,kind,id,name}, recentEvents[]}`. Tipos: `npc` (`pessoa`),
`building` (`predio`), `crop` (`lavoura`), `animal`, `object` (`objeto`), `leaving` (`espolio`), `city` (`cidade`, id = Guid,
resumo leve), `household` (`casa`), `workplace` (`oficina`). 404 para tipo ou id desconhecido. `state` de pessoa reaproveita
`/npcs/{id}`; `recentEvents` vem do buffer de eventos da API (desde o último esvaziamento) e dos fatos com a pessoa.
`/npcs/{id}` ganhou `childIds`, `householdBuildingId` e `employment{id,locationTypeId,buildingId,ownerId,name}`.
Nomes (`professionName`, `typeName`, `resourceName`…) vêm do catálogo do período do mundo; mundo sem período fica sem nome.

## Lacunas do motor (não inventadas)
- Animal sem idade, porte nem atividade (só espécie, célula, energia, doença).
- Lavoura ocupa uma célula: não há área em células (só hectares semeados/aráveis).
- Moradia não tem dono (só oficina tem `Owner`); dono de prédio = dono da oficina dele.
- Luto não tem limiar no motor: `mourning` é tristeza ≥ 0,1 (apresentação). Estação é o quarto do ano; o nome segue a curva padrão.
- Interior: `ObjectDescriptor` não tem nome nem material (o id é o nome legível) e a mobília não tem andar nem posição.
- Alvo da ação só existe para trabalho, sono e conversa com o cônjuge (`NpcInspectionQuery.TargetOf`); o resto vem em `intentTarget`.

## Custo (cidade de 500 NPCs, 240 ticks, `ScopeProjectionCostHarness`, 3 rodadas)
Sem assinante: motor igual (nenhuma projeção roda; `compare-simulation-cost.sh`). Com assinante nos escopos Cidade e Mundo:
números no AD-170 do [decisions-log](../decisions-log.md).
