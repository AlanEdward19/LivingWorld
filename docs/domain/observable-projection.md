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
| `npcs[]` (AD-185) | `routeDepartedTick`, `routeOver[]` (índices em `route`) | saída da viagem; células por cima (escada, laje, telhado) |
| `buildings[].lot` (AD-185) | `accessCells[]`, `accessViaBuildingId`, `accessState` (`reserved`/`planned`/`trodden`) | geometria do acesso |
| `buildingStates[]` | `typeName`, `function`, `workplaceId`, `locationTypeId/Name`, `ownerId`, `householdIds`, `residentIds`, `workerIds`, `stock[]`, `pantryUnits`, `closed` | função e ocupação |
| `crops[]` | `plot`, `resourceId/Name`, `status`, `stage`, `fraction`, `thirsty`, `sownHectares`, `arableHectares`, `standing`, `farmId`, `ownerId`, `expectedHarvest` | lavoura |
| `animals[]` (Cidade) | `species`, `position`, `energy`, `carriesDisease`, `predatorOf` | fauna individual |
| `faunaGroups[]` (Mundo) | `species`, `regionId`, `count`, `center` | fauna agregada |
| `leavings[]` | `location`, `deadId`, `householdId`, `heirHouseholdId`, `units`, `itemId`, `designId`, `fetcherId`, `carrierId` | espólio no chão |
| `objects[]` (Cidade) | `designId`, `householdId`, `holderId`, `location`, `forSale`, `marketId`, `rootShape`, `rootMaterial`, `qualityFraction` | peça inventada; partes em `/designs/{id}` |
| `climate` | `month`, `seasonIndex`, `seasonKey`, `temperatureC`, `regionId`, `cropYearFactorMilli`, `drought`, `weather{…}` (AD-180, [climate](climate.md)) | estação, clima e tempo do dia |
| `processes[]` | `resourceId`, `resourceName` | recurso do processo de material |
| `events[]` | `eventId`, `involved{subjectId,otherId,authorId,victimId,relatedIds,householdId,buildingId,workplaceId,caseId,leavingId,designId,cityId,amount,outcome,recordOnly}` | envolvidos lidos do payload |

Delta: `buildingStateUpserts/Removed`, `cropUpserts/Removed`, `animalUpserts/Removed`, `faunaGroupUpserts/Removed` (chave
`espécie:região`), `leavingUpserts/Removed`, `objectUpserts/Removed`, `climate` (nulo = não mudou). Camada `layers.Climate` passa a
`isModeled: true` com `{month, seasonIndex, seasonKey, regions[]{regionId, center, temperatureC, cropYearFactorMilli, drought, weather}}`.

## Delta enxuto (AD-172): regra de leitura do cliente
Nenhum campo saiu do contrato. O **snapshot** escreve os nulos (`campo: null`). O **delta de escopo** (WS, SSE e
`/visual/replay`; `VisualWire`) **omite toda chave nula**, e o cliente lê ausente e `null` do mesmo jeito:
1. **Ausente/nulo = igual ao anterior** — só estes, só em `npcUpserts[i]` de NPC já conhecido no escopo: `observable`, `city`,
   `appearance` e o **grupo da ação** (`actionStartedAtTick` ausente = `actionStartedAtTick`, `actionTarget`, `intent`,
   `intentTarget` iguais aos anteriores). E o `climate` do topo do delta (ausente = não mudou).
2. **Ausente/nulo = nulo** — todo o resto, inclusive dentro do grupo da ação quando `actionStartedAtTick` veio (ex.: sem
   `actionTarget` = sem alvo) e todo campo dos outros upserts (processo, prédio, lavoura, espólio, objeto, evento...): upsert troca
   o registro inteiro.
3. **Sem colisão**: se um campo da regra 1 precisa voltar a nulo, o NPC vem em `npcRemoved` **e** em `npcUpserts` no mesmo
   delta: descarte o que tinha e leia o upsert inteiro pela regra 2. NPC novo ou que volta ao escopo também vem inteiro.
`intentTarget` é texto opaco (formato reservado `tipo:id`, ex. `workplace:3`); o motor ainda não o preenche, então hoje vem
sempre ausente. Formas: `tests/contracts/shape.*.json` marcam `campo?` (pode faltar ou vir nulo) em todo campo anulável; são
gravadas de um mundo rodando 240 ticks mais exemplos cheio e mínimo, e `RunningWorldContractTests` confere cada snapshot e delta.

**Processos que andam sozinhos.** `rest` e `food` com `remainingHours > 0` e `progress < 1` não são reenviados quando só
avançaram no relógio. A cada delta, todo processo conhecido que não veio em `processUpserts` nem em `processRemoved` avança
`dt = delta.tick − tick do último snapshot/delta aplicado`: `duração = round(remainingHours / (1 − progress))`,
`remainingHours = max(0, remainingHours − dt)`, `progress = clamp(1 − remainingHours / duração, 0, 1)`. Os outros tipos ficam como
estão. O servidor só pula o processo quando essa mesma conta (`ProcessAdvance`) dá exatamente o valor real; senão manda o upsert.
O cliente guarda o tick de cada snapshot e delta que aplica. Escopo que fica sem assinante perde a base: o primeiro delta depois
de reassinar traz tudo.

Crime: `involved` traz autor, vítima, caso e desfecho, sem descrição; violência sexual, doméstica e contra criança com
`recordOnly: true` (só registro, sem cena). O `label` existente não mudou. Posição do evento: célula do espólio; senão a pessoa
do primeiro campo quando ela é envolvida; senão sujeito, autor, vítima, casa, oficina ou prédio.

## Inspeção
`GET /entities/{kind}/{id}` → `{kind, id, name, state, links[]{relation,kind,id,name}, recentEvents[]}`. Tipos: `npc` (`pessoa`),
`building` (`predio`), `crop` (`lavoura`), `animal`, `object` (`objeto`), `leaving` (`espolio`), `city` (`cidade`, id = Guid,
resumo leve), `household` (`casa`), `workplace` (`oficina`), `event` (`evento`, id do log, do buffer da API; AD-172). 404
para tipo ou id desconhecido. Prédio sem nome de tipo no período: nome do local da oficina, senão `casa de <responsável>`. `state` de pessoa reaproveita
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

## Custo (cidade de 500 NPCs, 240 ticks, `ScopeProjectionCostHarness`, 3 rodadas alternadas)
Sem assinante o laço não projeta (`compare-simulation-cost.sh`). Com assinante, o laço monta **uma** leitura do tick
(`ScopeProjectionFrame`) para todos os escopos, com a do tick anterior: NPC e prédio sem mudança visível reaproveitam o registro
(sem alocar; o diff compara por referência). Nada disso entra no mundo. Números no AD-172 do [decisions-log](../decisions-log.md):
projeção 9,6–12,8 → 4,3–4,5 µs/NPC-tick (tick ~6,6), abaixo dos 5,8–8,4 de antes da AD-170.
