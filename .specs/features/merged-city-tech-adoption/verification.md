# Verificação independente da #114: fusão de cidade transfere a adoção

Verifier sem autoria do código. Base `b29ed9f2`, HEAD verificado `143058c8`. Mutações e sonda foram temporárias e removidas;
`git status` limpo antes deste commit. `compare-simulation-cost.sh` não rodado, por instrução (adiado para a integração).
Transferência (em vez de reaprendizado) é o padrão proposto pela issue, pendente do dono; não avaliada como defeito.

Nota de processo: um `grep` amplo exibiu 3 linhas da rodada anterior deste arquivo antes do fim da análise. O conteúdo
completo só foi lido depois de todas as execuções; nenhum resultado abaixo vem dele.

## Veredito: PASS

Todos os critérios da issue e do plano têm evidência própria; 12 de 12 mutações foram mortas por teste. Nenhum FAIL.

## Critérios da issue

| # | Critério | Resultado | Evidência |
|---|---|---|---|
| 1 | Regressão: filha adota `pace > 0`, mãe não; após a fusão `BonusPct(mãe) > 0` e filha fora de `AdoptedBy` | PASS | `HandleEvent_transfers_...` passa e reprova sem a troca (M1). Fusão real: `Tick` agenda e `HandleEvent` executa |
| 2 | Nenhuma lápide em `AdoptedBy` em 10 anos, emergência e fundação ligadas | PASS | `MergedCityAdoptionScenarioTests`, 3 seeds, amostra diária: passa; reprova 3/3 com M1 e M10 (`Assert.DoesNotContain`). `default.json`: `Emergence.Enabled` e `CitiesEnabled` true |
| 3 | Dois processos, mesma seed, mesmo hash | PASS | `DeterminismTwoProcessTests` passa; sonda abaixo cobre o caminho com fusão e tecnologia |
| 4 | Goldens inalterados | PASS | `GoldenHashesTests` passa; `git diff b29ed9f2...HEAD -- tests/golden tests/baselines` vazio |

Execução: `test.sh --project longrunning`, filtro `GoldenHashes|DeterminismTwoProcess|MergedCityAdoption`: 19 passam,
1 pulado (gravador de golden), 0 falhas.

**Sonda de dois processos** (teste temporário, `default.json` com a injeção do cenário, 10 anos, duas invocações de
`test.sh`). O teste existente usa `RunAndHash`, sem emergência, e não passaria por este caminho.

| Seed | Hash, processos A e B | Fusões / filhas com tecnologia fundidas | `adoptedTomb` / `dup` | Hash com M1 |
|---|---|---|---|---|
| 1 | `b95543ba...` idêntico | 1 / 1 | 0 / 0 | `acb8eeea...` (difere; `adoptedTomb` 1) |
| 2 | `98d3ea21...` idêntico | 5 / 5 | 0 / 0 | `b2f85138...` (difere; `adoptedTomb` 5) |

`OriginCity` segue na lápide em 1 e 5 tecnologias. A transferência entra no hash e muda o mundo (seed 2: 8 contra 10
tecnologias sem o fix). `Technologies` está em `HotProperties` (`IncrementalHasher.cs`), então não há hash obsoleto.

## Critérios do plano

| # | Critério | Resultado | Evidência |
|---|---|---|---|
| C1 | Mãe herda, bônus > 0, filha sai | PASS | Critério 1; M1, M10 |
| C2 | Mãe já adotava: perde só a filha, sem duplicata e sem evento | PASS | `HandleEvent_drops_...`; M2, M10, M11 |
| C3 | `CauseEventId` = `CityMerged` | PASS | `SpatialSettlementFoundingSystem.cs:203-210`; M4 |
| C4 / C4b | Sem lápide em 10 anos; não vácuo | PASS | Exige `mergedSeeded > 0`; a injeção põe filha adotante no caminho real da fusão; M1, M10 |
| C5 | Determinismo e goldens; `OriginCity` intacto | PASS | Critérios 3 e 4; M8 |
| C6 | Protótipo tardio: adota a cidade ativa, origem na filha | PASS | `TechDiscoverySystem.cs:95-97`; M7, M9 |
| C7 | `SourceSystem` e ordem por índice testados | PASS | M5; `..._in_technology_index_order` usa vetores 9, 2, 5 fora de ordem; M6 |

## Escritas em `AdoptedBy` e caminhos de lápide

Grep em `src`: `new Technology(`, `AddTechnology`, `ReplaceTechnology`, `with { AdoptedBy`.

| Ponto | Conclusão |
|---|---|
| `TechDiscoverySystem.cs:96` (conclusão de protótipo) | Resolve com `FindActiveCity`, que segue a cadeia de fusões (`WorldState.Cities.cs:40-47`) |
| `TechDiscoverySystem.cs:188` (`Spread`) | Só acrescenta de `ActiveCities()` (`:180`) |
| `WorldState.Cities.cs:93` (fusão) | A mãe vem de `FindActiveCity` (`SpatialSettlementFoundingSystem.cs:195`); roda depois de `MarkMergedInto` |
| Construtores de `WorldState` (`WorldState.cs:546`, `:753`) | Só restauram snapshot |
| Fusão em cadeia | Uma mãe fundida depois transfere de novo; filha agendada com mãe já lápide resolve a ativa |
| `MarkMergedInto` | Único chamador em `src`: `MergeCityInto` (`WorldState.Cities.cs:78`). Único chamador de `MergeCityInto`: `:202` do handler |

## Outros campos com `CityId` (grep em Domain, Simulation e `WorldState`)

- Movidos: `Building`, `Workplace`, `Household.City` e `PendingRelocationCity` (`ReplaceCityReference`), `Npc`, `ConstructionProject`.
- Histórico ou próprio da lápide: `OriginCity`, `FoundedFromCityId` (lido via `FindActiveCity`), `MergedIntoCityId`,
  `TreasuryAggregate.City`, `Fact.Location`, `WitnessedAccount.Location`, `NarrativeDraft.Location`, `ReportState.CommunityId`.
- `SpatialAddress.RefId` e portais: sem instância em tempo de execução para cidade fundada. Lista do plano confere (ver O1, O2).

## Mutações (restauradas com `git checkout`)

| Mutação | Morta por |
|---|---|
| M1 não troca a filha pela mãe | `transfers`, `drops`, `index_order`; cenário 3/3 |
| M2 duplica a mãe | `drops` |
| M3 omite `TechnologyAdopted` | `transfers`, `index_order` |
| M4 causa errada (`mergedId - 1`) | `transfers` |
| M5 `SourceSystem` errado | `transfers` |
| M6 inverte a ordem | `index_order` |
| M7 lápide adota no protótipo tardio | `Prototype_of_a_merged_city_...` |
| M8 reescreve `OriginCity` na fusão | `transfers` |
| M9 reescreve `OriginCity` no protótipo | `Prototype_of_a_merged_city_...` |
| M10 mantém a lápide e soma a mãe | `transfers`, `drops`; cenário 3/3 |
| M11 evento quando a mãe já sabia | `drops` |
| M12 payload com a filha | `transfers` |

**Gate:** `verify.sh` saiu 0 (`verify: OK`, 0 warnings, Unit 2463 passam, 3 pulados). `Category=Scenario` da fundação: 1 passa.

## Observações não bloqueantes

- O1: `plan.md` diz que `ReportState.CommunityId` vive na lápide, mas ele também está em `WorldState.Reports`. O vínculo
  segue coerente: `HistoryBeliefQuery` lê o cânone da própria comunidade.
- O2: o cânone da filha fica na lápide e `NpcBeliefQuery.cs:26` lê a cidade atual, então quem migrou deixa de acessá-lo. É
  decisão registrada no plano e a issue pede só conferir; lembra o defeito desta issue e pode merecer issue própria.
- O3: o protótipo tardio dá a adoção à mãe sem `TechnologyAdopted`; só sai `TechnologyDiscovered` com a filha no payload,
  como em toda descoberta. O plano e a issue não pedem mais.
- O4: mundos salvos antes do fix mantêm lápides em `AdoptedBy`; não há migração. A issue não pede.
- O5: o cenário de 10 anos fica em LongRunning, fora do `verify.sh` padrão (roda em `--full`). A prova rápida são os testes unitários.
- O6: `main` já tem a #113; a mesclagem é limpa (`git merge-tree`), mas `docs/audits/causal-provenance-audit-city-history.md`
  lista só `TechDiscoverySystem` como emissor de `TechnologyAdopted`. Atualizar ao integrar.
