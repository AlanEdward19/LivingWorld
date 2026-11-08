# Verificação independente: issue #113, fatia A (rodada 2)

Commit verificado: `dfaba21b` (base `b29ed9f2`). Verifier sem participação na autoria; a conclusão
abaixo foi formada antes de abrir a verificação anterior.

## Veredito: FAIL (1 FAIL bloqueante, AC3)

| Critério | Resultado | Evidência |
|---|---|---|
| AC1 kind sem classificação reprova | PASS | M1, M2 e M13 reprovam `Every_world_event_kind_is_classified_as_root_or_transformation` |
| AC2 uma lista por kind, só definidos | PASS | M3 (estático lança, 11/11 falham), M4 (kind 9999), M13 |
| AC3 auditoria cobre os emissores | **FAIL** | 67/67 kinds têm linha, mas falta um emissor: ver abaixo |
| AC4 mundo não muda | PASS | ver "Mundo inalterado" |
| AC5 docs até 100 linhas | PASS | `check-docs: OK`; partes com 58, 50, 46 e 39 linhas |
| Regra de causa no plano e em AD | PASS | `plan.md` § Rule; `docs/decisions-log.md` AD-121 |
| Lista no código | PASS | `CausalPolicy.cs:23,52`: 46 T + 21 R = 67; enum tem 67 (script); sem sobra nem interseção |
| Classificação casa com a issue | PASS | morte, fusão, fundação, emprego, despejo, surto, doença, tecnologia, herança, casamento e órfão são T; nascimento, imigração autorada e autoria são R |
| Escopo | PASS | `git diff --name-status`: 8 arquivos novos, só `docs/decisions-log.md` modificado (+1 linha) |
| `bash scripts/verify.sh` | PASS | rc=0; `Passed: 2470, Skipped: 3, Total: 2473`; 0 warnings; `verify: OK` |
| Custo | não medido | `compare-simulation-cost.sh` adiado por instrução; não conta como FAIL |

## FAIL: emissor omitido na auditoria

`src/LivingWorld.Infrastructure/EventLog/EventLogCompactor.cs:52`:
`kept.Add(new WorldEvent(firstTick, (WorldEventKind)kind, ..., SourceSystem: AggregateSource))`.

- Kind passado por variável; `NoiseKinds` (`:18-21`) = `ResourceLost`, `WageUnpaid` (**T**), `PlantMatured`.
- Caminho de produção: `PersistentWorldRunner.cs:41`, antes de gravar; também `Workers/Program.cs:75`.
- O evento sai com `SourceSystem = "EventLogCompactor"`, `EventId = 0` e sem causa.
- `grep -rn Compactor docs/audits/causal-provenance-audit*.md` não retorna nada. A linha de
  `WageUnpaid` (`life-economy.md`) lista só `WagePaymentSystem.cs:53`, e a varredura declarada no
  índice cobre apenas `LogEvent` e `NpcDeath.Apply`/`Kill`.
- Por que importa: os `WageUnpaid` individuais somem do log persistido. A auditoria propõe
  `WageUnpaid` como causa de `Fired`, `RentUnpaid` e `Starvation`, e essas cadeias não resolvem no
  log gravado. O gate da fatia D, com sink de teste, não veria isso.

**Correção necessária:** registrar o compactador nas linhas dos 3 kinds (fonte, sem causa,
`EventId = 0`) e anotar para as fatias B e D o efeito sobre causas que apontam para kinds compactados.

## Inventário próprio
Script sobre `src/`, com parênteses balanceados, em 93 chamadas `LogEvent` fora de `TickContext`:
- Todas as 91 chamadas de kind literal estão na linha do kind correto da auditoria (arquivo:linha).
- 2 chamadas por variável: `NpcDeath.cs:28` e `FaunaLifecycleSystem.cs:185`. Todos os chamadores
  (`NpcDeath.Apply` ×8, `Kill` de fauna ×2, `Kill` de flora) constam da auditoria, com o kind certo.
- Ternário `ExtraordinaryStateSystem.cs:53` (`Manifested`/`Dormant`): coberto nas duas linhas.
- Sentido inverso: toda referência arquivo:linha da auditoria é emissão, ou é a nota declarada
  (`SettlementFoundingSystem.cs:27`, `TickContext.cs:24`, `CompensatingCorrectionOperations.cs:31`,
  `OutbreakSystem.cs:20`).
- `SourceSystem`: nenhuma chamada usa o overload legado nem passa `"Unknown"`.
- Causa: só 11 chamadas têm 4º argumento (`Effects:58,64`, `MatterTransmute:49`,
  `ConstructOperations:30`, `StateSystem:96`, `Engine:68,80,93,390,399`, `Natality:190`), como a auditoria diz.
- Migração não emite evento; `SettlementFounded` não tem emissor: confirmado.

Amostra lida linha a linha (31 linhas, 3 partes): `MortalitySystem:31`, `NeedsDecay:92`, `Outbreak:51,109`,
`Natality:93,191`, `WagePayment:53`, `Lodging:93`, `OrphanCare:78`, `Estate:47`, `Fauna:174`,
`Flora:188,197`, `SpatialFounding:203`, `SettlementFounding:27`, `TechDiscovery:96,188`,
`PlayerCommands:77`, `PlayerSearch:85`, `Authoring:104`, `FactToReport:72`, `BookRediscovery:44`,
`WorldState.Economy:96`, `MatterTransmute:50`, `TradeLedger:34`, `Engine:400`,
`ConstructOperations:31`, `StateSystem:97`, `CombatEncounter:113`, `Control:136`, `Effects:65`.
Todas conferem. Fontes: `fauna-lifecycle`, `flora-lifecycle` e `health-outbreak` conferem com `SystemName`.

## Mundo inalterado
- `git diff --name-only b29ed9f2...HEAD | grep -E 'baseline|golden|hash|\.json$'`: vazio.
- Nenhum emissor em `src/` mudou; o único `.cs` novo de produção é `CausalPolicy.cs`, sem referência
  em `src/` fora dele mesmo. Goldens de LongRunning não rodaram (fora do `verify.sh` padrão).

## Mutações (`bash scripts/test.sh --filter FullyQualifiedName~CausalPolicyTests`; base: 11/11 passam)
| Id | Mutação | Resultado |
|---|---|---|
| M1 | remove `Hired` de T | reprova 2 |
| M2 | remove `Minted` de R | reprova 2 |
| M3 | `Birth` nas duas listas | reprova 11 (construtor estático) |
| M4 | kind indefinido na lista | reprova 2 |
| M5 | `IsTransformation` invertido | reprova 2 |
| M6 | `IsTransformation` true para não classificado | reprova 1 |
| M7 | `RoleOf` devolve Root por padrão | reprova 1 |
| M8 | `RoleOf` invertido | reprova 5 |
| M9 | `TransformationKinds` devolve raízes | reprova 2 |
| M10 | `RootKinds` devolve transformações | reprova 2 |
| M11 | `Death` movido para R | reprova 1 |
| M12 | `Birth` movido para T | reprova 1 |
| M13 | kind novo no enum | reprova 2 |
| M14 | `Marriage` movido para R (sondagem) | **sobrevive** |

Cada mutação restaurada com `git checkout -- <arquivo>`; `git status --short` vazio ao final.

## Observações (não bloqueiam)
- M14: só 5 kinds têm papel fixado em teste. O plano aceita reclassificar trocando uma linha, mas
  casamento e os demais kinds citados na issue poderiam entrar no `Theory`.
- O índice diz que `SettlementFounded` só aparece na capacidade e no catálogo; há também o leitor
  `LivingScopeState.cs:245`.
- O índice diz que `"Unknown"` só existe como padrão; `SavedWorldLibrary.cs:61` também o usa ao importar.
  Esse ponto e `HistoryDistortionDigest.cs:18` não geram evento novo no log, por isso não são FAIL.
