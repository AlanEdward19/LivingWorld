# Verificação independente: issue #64 (registro de primitivas)

Base `b29ed9f2`, HEAD verificado `23d0606f`. Critério 7 (sensor de escala) adiado por decisão do orquestrador; rota HTTP e `ROADMAP.md` fora de escopo.

**Veredito: FAIL** (um FAIL: validação na carga, item 3 da issue). Demais critérios PASS.

## Resultado por critério

| # | Critério | Resultado | Evidência |
| --- | --- | --- | --- |
| 1 | Paridade de hash | PASS | `git diff b29ed9f2 HEAD -- tests/golden` vazio. `test.sh --project longrunning --filter 'GoldenHashesTests\|DeterminismTwoProcessTests'`: 16 passam, 1 skip (`ZZZ_record`), rc 0 |
| 2 | Poder novo só por dados | PASS | `DataOnlyPowerTests.Novel_power...`; mortos M04, M05, M08, M18, M19, R30, R34, R35 |
| 3 | Teste de arquitetura | PASS | Sem literal fora da tabela (`git grep`); switch real em `BondMechanic` (M20) e literal prefixado (M21) reprovam |
| 4 | `npc.mana:+5` recusado citando descritor e token | PASS | `Unknown_effect_token...`; mortos R23, R24, M25, M26 |
| 5 | Catálogo por reflexão | PASS | `Every_mechanic_type...`; mortos M27 (fora do registro) e M28 (`Primitives` vazio) |
| 6 | Base sem poder | PASS | `Default_scenario_hash...` reprova com R36; gate `Enabled` protegido por `Disabled_configuration...` (R38) |
| Item 1 | Tabela única, clamps preservados | PASS | Ver paridade abaixo |
| Item 3 | Recusar **qualquer** token que nenhuma mecânica aceite | **FAIL** | Ver F1 |
| Nota | `phase-16-powers.md` atualizada | PASS | Linhas 3-11, com pendências do dono |
| Gate | `bash scripts/verify.sh` | PASS | rc 0, `verify: OK`, Unit 2473 passam, 3 skips |

## F1 (FAIL): a validação casa só a cabeça do token

`ExtraordinaryPrimitiveCatalog.cs:36` aceita se `Matches` casa; `IExtraordinaryMechanic.cs:38` só olha prefixo e o caractere seguinte, nunca os argumentos. Sonda temporária (carga real e `Invoke` real, mesmo token), todos independentes do estado do mundo:

| Token | Carga | Invocação |
| --- | --- | --- |
| `npc.health`, `npc.health:0`, `npc.health:abc` | aceita | falha: magnitude |
| custo `carrier.hunger`, `:abc`, `:-2`, `:0` | aceita | falha: magnitude positiva |
| custo `household.resource.abc:1`, `household.resource.9` | aceita | falha |
| `transfer.health:-1`, `npc.clone`, `combat.strike:` | aceita | falha: magnitude |
| `mind.read:x`, `soul.persist-as-ghost:1`, `control.body-swap:1` | aceita | falha: alvo não suportado |
| `area:radius:abc`, `bond.share:health:abc`, `construct.create:bad` | aceita | falha |
| `luck.curse:abc`, `matter.transmute:1:2`, `gravity.self`, `appearance.impersonate:abc` | aceita | falha |
| `bond.oath:` (vazio) | aceita | falha: contradiz `plan.md:53` ("que há consequência") |

Impacto: o erro continua aparecendo só na invocação, que é o defeito descrito no item 3, e todo uso do poder registra `ExtraordinaryUseFailed`. O catálogo declara `carrier.hunger:<n>` mas a carga não o impõe, o que também engana o editor do #46. Só `bond.oath` está declarado em "Decisões de risco".

Correção esperada: validar a sintaxe dos argumentos sem contexto de mundo (magnitude, sufixo proibido em token exato, formato aberto), com teste para os casos acima. Dependências de estado (`mind.commune`, saldo, NPC existente) continuam na invocação.

## Validação: tokens recusados que a mecânica aceita

Recusados na carga e `OK` na invocação: `movement.foo:1`, `area:foo`, `npc.teleportx:1`, `npc.force-actionx:1`. **Não é FAIL**: nenhum produz efeito próprio.
- `movement.*`: só `movement.flight` e `movement.speed-multiplier` têm consumo (`GravityMechanic.cs:98-103`). Outra chave só afetaria a cobrança por prefixo (`ExtraordinaryLocomotion.cs:102`) e a urgência (`BehaviorDecisionSystem.Utility.cs:208`). Nenhum uso no repositório. Risco já registrado no plano.
- `area:foo` não é seletor (`AreaTargetResolver.cs:17`): no-op, mesma classe, não citado no plano.
- `npc.teleportx`/`npc.force-actionx` só passavam por leniência antiga do parser.

Todos os demais tokens válidos da sonda (cada formato de cada mecânica, inclusive `npc.thirst:sunlight:-3` e `transfer.health:5:from-target`) são aceitos na carga.

Fixtures: `movement:speed`, `movement:construct` e `fatigue:2` não tinham mecânica (`FAIL` na invocação pela sonda). Os testes alterados não invocam o poder; a troca não enfraquece nada.

Caminhos fora do loader: snapshot desserializa direto (`WorldSnapshot.cs:202`), `MixDescriptorBuilder` usa `PassesPrepareContract`, nenhum arquivo `scenarios/` tem bloco `Extraordinary`. Mundos salvos não quebram.

## Paridade mecânica por mecânica (`git show b29ed9f2` x HEAD)

- `NpcStatMechanic`: soma em `long` e clamp 0..100; ordem parse, chave, closure igual. Paridade.
- `CarrierCostMechanic`: subtrai em `int` sem clamp próprio. O `available < 0` antigo só filtrava chave desconhecida (leituras são >= 0). Paridade.
- `TransferMechanic`: débito bruto, `room` = `Max` - leitura, crédito limitado; ordem preservada. Paridade.
- `BondMechanic`: `Clamp` igual a `ClampNeed`; chave validada por `TryParseShare` antes. Paridade.
- `ExtraordinaryLocomotion`: mesma sequência e o mesmo `-1` para chave desconhecida. Paridade.
- Alocação: `TryGet` por span, delegates `static`, sem alocar por acesso. Exceção em Observações.

## Mutações (restauradas; `git status` limpo ao final)

Filtro `Extraordinary` no Unit (362 testes); sobreviventes repetidos em Unit completo (2476), Scenario, Integration e LongRunning.

| Alvo | Mortas | Sobreviventes |
| --- | --- | --- |
| Tabela | M03, R02, R30, R32, R33, R34, R35 | R01 (leitura hunger), R31 (leitura sleep) |
| Carrier | M04, M05 | R07 (`CostAvailable` lê o alvo), M06 equivalente |
| NpcStat | M08 | M09 equivalente |
| Transfer | M11, R12 | M10 equivalente |
| Bond | M13 | M14 equivalente |
| Locomoção | M15 | R16 (saldo infinito) |
| Falha/dados | M17, M18, M19 | nenhuma |
| Arquitetura | M20, M21 | M22 (ver Observações) |
| Carga/catálogo | R23, R24, M25, M26, M27, M28 | nenhuma |
| Hash/gate | R36, R38 | R37 (null liga sistemas sem efeito no hash) |

M06, M09, M10, M14 são equivalentes: os setters já limitam (`Npc.cs:465`, `LazyNeed.cs:27`), logo trocar clamp por não-clamp não é observável. M12, M23, M24 não compilaram com `if (false)` e foram refeitas como R12, R23, R24.

## Observações não bloqueantes

1. R01, R31, R07, R16: o código está correto na leitura, mas falta teste. Cada mecânica tem ao menos uma mutação morta. R02, R32, R33 só morrem no LongRunning, fora do `verify.sh` padrão.
2. `ExtraordinaryInvocationEngine.cs:327`: concatena duas strings por modo de falha a cada invocação, onde antes havia literal; contradiz "zero alocação" de `plan.md:11`. Não medido (critério 7).
3. M22: o regex do teste de arquitetura não pega literal com dois-pontos (`"carrier.health:"`).
4. `PeriodsEndpoints.cs:95` usa `.Value!`: template já salvo com token agora recusado gera exceção. Nenhum template do repositório tem o bloco.
