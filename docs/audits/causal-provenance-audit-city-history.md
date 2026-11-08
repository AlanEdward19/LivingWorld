# Auditoria causal: cidade, saúde, jogador, autoria e histórico

Parte de [causal-provenance-audit.md](causal-provenance-audit.md). **T** transformação, **R** raiz.
Todos os emissores abaixo passam `SourceSystem` real e **nenhum passa causa**.
Caminhos relativos a `src/LivingWorld.Simulation/`.

## Cidade, saúde e tecnologia
| Kind | Papel | Emissor (arquivo:linha) | Causa proximal correta |
|---|---|---|---|
| `SettlementFounded` | T | **sem emissor** (só `[LivingCapability]` em `Cities/Founding/SettlementFoundingSystem.cs:27`) | condição de superlotação/limiar na cidade-mãe (`OvercrowdingBegan`) |
| `CityMerged` | T | `Cities/Founding/SpatialSettlementFoundingSystem.cs:203` | fundação da cidade-filha (`SettlementFounded`) |
| `OutbreakStarted` | T | `Health/OutbreakSystem.cs:109` | condição de superlotação da cidade (`OvercrowdingBegan`) |
| `TechnologyDiscovered` | T | `Emergence/TechDiscoverySystem.cs:96` | pressão de trajeto até o mercado, registrada como condição (`TravelPressureObserved`) |
| `TechnologyAdopted` | T | `Emergence/TechDiscoverySystem.cs:191`; `Cities/Founding/SpatialSettlementFoundingSystem.cs:209` (herança na fusão, #114: já com causa `CityMerged`) | `TechnologyDiscovered` da cultura (e laço entre cidades); na fusão, `CityMerged` |

Migração e realocação (`Cities/Migration/MigrationSystem.cs`, `RelocationArrivalSystem.cs`) não
têm kind nem emissor. Causa proximal futura: fator dominante do score (desemprego: último
`Fired`; fome: início da escassez; família: evento do laço).

## Jogador e autoria
| Kind | Papel | Emissor (arquivo:linha) | Causa proximal correta |
|---|---|---|---|
| `PlayerIncarnated` | R | `Players/PlayerCommands.cs:38` | raiz: comando do jogador |
| `PlayerDisappeared` | R | `Players/PlayerCommands.cs:77` | raiz: logout do jogador |
| `DisappearedFound` | T | `Players/PlayerSearchSystem.cs:85` | `PlayerDisappeared` do mesmo NPC |
| `SearchGaveUp` | T | `Players/PlayerSearchSystem.cs:99` | `PlayerDisappeared` do mesmo NPC |
| `AuthoringCommandApplied` | R | `Authoring/WorldAuthoringCommands.cs:42,53,141` | raiz: comando de autoria |
| `AuthoringCommandRejected` | R | `Authoring/WorldAuthoringCommands.cs:156` | raiz: comando de autoria |
| `AuthoredImmigration` | R | `Authoring/WorldAuthoringCommands.cs:104` | raiz: imigração autorada |
| `AuthoredWorkplaceFounded` | R | `Authoring/WorldAuthoringCommands.cs:127` | raiz: comando de autoria |

## Histórico e livros
| Kind | Papel | Emissor (arquivo:linha) | Causa proximal correta |
|---|---|---|---|
| `FactRecorded` | T | **sem emissor de evento** (`SignificanceCalculator.TryRecord` cria o `Fact`; `Scheduling/TickContext.cs:24` o exclui) | evento que originou o fato |
| `ReportConverted` | T | `History/Distortion/FactToReportConversionScheduler.cs:72` | evento do `Fact` convertido |
| `CompensatingCorrection` | T | **sem emissor de evento** (só `Fact`, `History/Distortion/CompensatingCorrectionOperations.cs:31`) | evento do fato corrigido |
| `BookLost` | R | `History/Books/BookOperations.cs:59` | raiz: acidente |
| `BookRediscovered` | T | `History/Books/BookRediscoverySystem.cs:44` | `BookLost` do mesmo livro |
