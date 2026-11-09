# Verificação independente: issue #113, fatia A (rodada final)

Commit verificado: `adc64b30` (base `b29ed9f2`). Verifier sem participação na autoria; a conclusão
foi formada antes de abrir a verificação anterior. O FAIL anterior (compactador omitido) está corrigido.

## Veredito: PASS (nenhum FAIL; 4 observações não bloqueantes)

| Critério | Resultado | Evidência |
|---|---|---|
| AC1 kind sem classificação reprova | PASS | M1, M2, M3 reprovam `Every_world_event_kind_is_classified_as_root_or_transformation` |
| AC2 uma lista por kind, só definidos | PASS | M4 (estático lança, 11/11 falham), M5 (kind 9999) |
| AC3 auditoria cobre 67 kinds e emissores | PASS | ver "Inventário próprio" |
| AC4 mundo não muda | PASS | ver "Mundo inalterado" |
| AC5 docs até 100 linhas | PASS | `check-docs: OK`; partes com 68, 54, 39 e 46 linhas |
| Regra de causa no plano e em AD | PASS | `plan.md` § Rule; `docs/decisions-log.md` AD-121; `tests/baselines/event-log-bytes.json` existe |
| Lista no código | PASS | `CausalPolicy.cs:23,52`: 46 T + 21 R = 67; enum tem 67 (script); sem sobra nem interseção |
| Classificação casa com a issue | PASS | morte, fundação, fusão, emprego, despejo, surto, doença, tecnologia, herança, casamento e órfão são T; nascimento, imigração autorada e autoria são R; papel da auditoria = papel do código nos 67 |
| Escopo | PASS | 9 arquivos: `CausalPolicy.cs` e teste novos, 4 docs de auditoria, plano, verificação, +1 linha em `decisions-log.md` |
| `bash scripts/verify.sh` | PASS | exit 0; `Passed: 2470, Skipped: 3, Total: 2473`; `verify: OK` |
| Goldens | PASS | `test.sh --project longrunning --filter GoldenHashes`: exit 0; 4 passam, 1 pulado (o gravador `ZZZ_record_golden_hashes`) |
| Custo | não medido | adiado para o fim da integração por instrução; não conta como FAIL |

## Inventário próprio (script com parênteses balanceados sobre todo `src/`)
- 93 chamadas `LogEvent` fora do encaminhador `TickContext.cs:18`: 91 com kind literal e 2 por
  variável (`NpcDeath.cs:28`, `FaunaLifecycleSystem.cs:185`). Todas em `src/LivingWorld.Simulation`.
- As 91 literais constam da linha do kind certo (arquivo:linha), inclusive o ternário
  `ExtraordinaryStateSystem.cs:53` nas duas linhas; `OutbreakSystem.cs:50` consta como "evento extra".
- Kind por parâmetro: `NpcDeath.Apply` (8 chamadores), `Kill` de fauna (2), `Kill` de flora (1): todos na auditoria.
- `new WorldEvent(`, 7 pontos: `TickContext.cs:29,30`, `EventLogCompactor.cs:52`, `SavedWorldLibrary.cs:60`,
  `DecisionMetrics.cs:96`, `HistoryDistortionDigest.cs:18`, mais a declaração do record. Todos na auditoria.
  Construção por `with` sobre `WorldEvent`: nenhuma. Cast e parse de kind: só compactador e importação.
- Compactador: `NoiseKinds` (`:18-21`) e usos (`PersistentWorldRunner.cs:41`, `Workers/Program.cs:75`) conferem,
  registrados nas 3 linhas (`WageUnpaid`, `ResourceLost`, `PlantMatured`).
- Sentido inverso: 108 referências arquivo:linha conferidas; única sem o kind é `OutbreakSystem.cs:20`, nota do `SystemName`.
- `SourceSystem`: nenhum chamador usa o overload legado; 11 chamadas passam causa, como a auditoria diz
  (`Effects:58` com `null`, `:64`; `Engine:68,80,93,390,399`; `MatterTransmute:49`; `ConstructOperations:30`; `StateSystem:96`; `Natality:190`).
- Migração sem evento e `SettlementFounded` sem emissor: confirmados.

Amostra lida (emissor, fonte, causa), 3 partes: `Outbreak:50,51,109`, `NpcDeath:28`, `Natality:93,100,191`,
`Lodging:88,93`, `Marriage:42`, `Courtship:96`, `TheftResolver:54`, `SpatialFounding:203`, `BookRediscovery:44`,
`Effects:59,65`, `Engine:69,81,94,391,400`, `Control:136`, `NpcInstantiation:155`, `Compactor:52`. Conferem.

## Mundo inalterado
- `git diff --name-only b29ed9f2...HEAD | grep -E 'golden|baseline|\.hash|scenarios/'`: vazio.
- Diff em `src/`: 0 linhas removidas, só `CausalPolicy.cs` novo, sem referência fora dele e do teste.

## Mutações (`bash scripts/test.sh --filter FullyQualifiedName~CausalPolicy`; base: 11/11 passam)
| Id | Alvo | Mutação | Resultado |
|---|---|---|---|
| M1 | AC1 | remove `PlantMatured` de R | reprova 2 |
| M2 | AC1 | remove `Fired` de T | reprova 2 |
| M3 | AC1 | kind novo no enum, sem classificar | reprova 2 |
| M4 | AC2 | `Death` nas duas listas | reprova 11 (construtor estático) |
| M5 | AC2 | kind indefinido 9999 em R | reprova 2 |
| M6 | `RoleOf` | devolve Root por padrão | reprova 1 |
| M7 | `IsTransformation` | true para não classificado | reprova 1 |
| M8 | `IsTransformation` | invertido | reprova 2 |
| M8b | `IsTransformation` | sempre false | reprova 1 |
| M9 | `TransformationKinds`, `RootKinds` | listas trocadas | reprova 2 |
| M10 | `TransformationKinds` | vazia | reprova 1 |
| M11 | `RootKinds` | vazia | reprova 1 |
| M12-14 | fronteira | `Death`, `Evicted`, `SettlementFounded` viram R | reprova 1 cada |
| M15-16 | fronteira | `Birth`, `AuthoredImmigration` viram T | reprova 1 cada |
| M17 | sonda | `Marriage` vira R | **sobrevive** |

Cada mutação restaurada com `git checkout -- <arquivo>`; `git status --short` vazio ao final.

## Observações (não bloqueiam)
- M17: só 5 kinds têm papel fixado em teste. Os 4 métodos públicos têm teste com poder e o plano aceita
  reclassificar trocando uma linha, mas os kinds citados na issue poderiam entrar no `Theory`.
- `MaternalDeath` (`life-economy.md`): a causa proposta é `Birth`/`StillBirth` e a nota diz que a morte vem
  "antes deles", mas `NatalitySystem.cs:93-94` faz `return`: nesse caminho nenhum dos dois é emitido. A fatia B
  precisa de outra causa (ou de evento de parto); o `return` é visível no próprio ponto de edição.
- `HistoryDistortionDigest.cs:18` é chamado por `Workers/Program.cs:199` (comando de digest, mundo próprio, sem
  log persistido): "digest de teste" é impreciso, sem efeito na classificação.
- Fechamento pendente (item 13 da issue, posterior a esta verificação): `STATE.md` e
  `docs/audits/living-world-cohesion-audit.md` não foram tocados; `CausalPolicy` ainda não tem consumidor.
