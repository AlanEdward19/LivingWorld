# Verification — world-creator-live (#46 fatia 4)

Verifier independente, contexto em branco. Commit `d071aeb` (branch `feat/world-creator`), diff `a366629..HEAD`.
Veredito: **PASS** (3 ressalvas, nenhuma bloqueante). Árvore limpa após as mutações; worktree e arquivo temporário removidos.

## Critérios (evidência própria)
| # | Critério | Resultado | Evidência |
|---|---|---|---|
| a | Família/local válidos mudam o hash e entram no log causal | PASS | `test.sh --filter LiveCreation`: unit 5/5, integration 9/9. Log: `WorldAuthoringCommands.cs:96-99,126-129` (`AuthoredImmigration`/`AuthoredWorkplaceFounded`); enum no fim `WorldEventKind.cs:200-204`; texto `LivingEventPresentationCatalog.cs:27-28`; `LivingWorldCapabilityCatalog.cs:35` (AUTHORING). API real: 200 com `householdId/npcIds` e `workplaceId/buildingId/x/y` |
| b | Inválida: 400 com o campo, hash igual, nada escrito | PASS | Curl real: `ProfessionId: fora do catálogo do período` [400], `MaxVacancies: precisa ser ao menos 1` [400]. Testes `An_invalid_family_...`, `An_invalid_workplace_...` (3 casos), `A_rejected_family_writes_nothing` comparam o hash. `WorldAuthoringService.cs` (fim de `WorldAuthoringCommands.cs`) chama o comando direto, sem `Run` |
| c | Pausa de admin 409; pausa comum não impede; sem token 403 | PASS | Curl: sem token [403] nas 2 rotas; após `POST /admin/world/pause` 409 nas 2 e `/npcs` 22 -> 22; após resume [200]. Pausa comum: `LivingWorldApiFactory.cs:62` faz `SimulationHost.Pause()` (sem `AdminPause`) e todos os testes de 200 rodam nela. `AuthoringEndpoints.cs:67-70` |
| d | Família vive 1 ano; dinheiro cunhado | PASS | `The_created_family_lives_a_year_by_the_usual_rules` (vivos, +1 ano de idade, ao menos um empregado, domicílio). `Newcomers_are_fed_...`: `MoneyMinted` sobe exatamente `50*3`; local: `+1000` (`WorldAuthoringCommands.cs:94,125`) |
| e | 2 processos | PASS | `test.sh --project longrunning --filter DeterminismTwoProcessTests`: 11/11, inclui `Same_seed_and_live_creation_...` (`;2;1` prova que criou) |
| f | `WorkplacePlacer` não mudou o cenário; contratos | PASS | `git diff a366629..HEAD -- tests/golden tests/baselines` vazio. `tests/contracts`: só `openapi.v1.json` (+47, 2 rotas) e `shape.authoring-household/-workplace.json`. `test.sh` 2270 ok; longrunning completo 130 ok/8 skip; `GoldenHashes` 4 ok |
| g | Validação idêntica à do cenário | PASS | `WorldAuthoringCommands.cs:65` usa `AuthoredHouseholdsLoader.Load` (o mesmo de `ScenarioLoaderV2.cs:176`); `:112` usa `EconomyScenarioLoader.LoadWorkplace` (extraído e reusado por `ParseWorkplaces`, `EconomyScenarioLoader.cs:176`). Curl: `Skills:{"0":40}` aceito [200]; `Appearance` recusado [400] "o período não declara o bloco Appearance" no mundo padrão (mesma regra do cenário; `appearanceEnabled` vem de `world.AppearanceRules`). Não há teste da rota com `Appearance` aceito (ver ressalva 2) |
| h | Cliente | PASS | `client-test.sh` 252/0 (6 de `test_live_creation`). Caminho lido: botão `admin_window.gd` "Criar família…" -> `family_requested` -> `app.gd:340` -> `open_family_builder` (`app.gd:672-684`) -> `done` -> `create_households` -> `api.admin_create_household` (`api_client.gd:138-143`) -> `POST /authoring/households`; local: botão -> `create_workplace` -> `POST /authoring/workplaces` |
| i | Desempenho | PASS | Rota não registra sistema no tick (diff sem `DefaultSystems`/clock). Sonda temporária (seed 42, 1000 NPCs, 720 ticks, 3 rodadas; a 1ª inclui JIT), base `a366629` vs HEAD: rodada 3 `0,508` vs `0,509` µs/NPC-tick; bytes/tick `172234` vs `172297` (100,04%); rodada 2 `0,752` vs `0,723`; `175587` vs `175527`; NPC-ticks vivos idênticos (701842). Dentro de 103%/102%. `ScaleScenarioSensorTests` 2 ok/1 skip |

## Mutações (restauradas com `git checkout`)
| Mutação | Reprovou? |
|---|---|
| i. `CreateHousehold/Workplace` voltam a usar `Run` (loga rejeição) | SIM: 4 testes (`An_invalid_family_...`, 3 do workplace) |
| ii. remove `world.Mint` do pecúlio da família | SIM: `Newcomers_are_fed_with_minted_money_...` |
| iii. remove a checagem `AdminPause is not null` | SIM: `Admin_pause_refuses_creation_...` |
| iv. remove o `Authorize` | SIM: `Without_the_admin_token_...` |
| v. remove a validação `MaxVacancies < 1` | SIM: `An_invalid_workplace_...(MaxVacancies)` |
| vi-a. remove `world.AddBuilding` do `WorkplacePlacer` | SIM: dezenas (`LodgingSystemTests`, etc.) |
| vi-b. posição final fixa (0,0) | SIM: 2 em `ScenarioLoaderV2Tests` (os goldens longrunning NÃO pegam; o unit pega) |
| vii. `create_households` retorna cedo | SIM: 2 em `test_live_creation` (250/2) |

## Gates
`verify.sh` exit 0; `client-test.sh` 252/0; `test.sh --project integration` 429/0; `check-docs.sh` OK (docs 29-36 linhas).

## Ressalvas (não bloqueiam)
1. **Desempenho não registrado no `plan.md`** (regra 7 da issue pede números antes/depois lá; a seção só afirma "nenhum sistema novo"). Registrar os números acima.
2. **Cobertura**: nenhum teste exercita `Appearance` aceito pela rota (mundo de teste sem bloco de aparência); `Skills` com id fora do catálogo é aceito (igual ao cenário, herdado do loader). O fio `app.gd` (abrir criador, `done` -> POST) só é verificado por leitura; os testes do cliente cobrem o `admin_window`.
3. **Concorrência**: as rotas mutam `host.Current` na thread HTTP sem trava enquanto `TICK_LOOP_ENABLED=true` roda o relógio (sem lock em `src`, igual às demais rotas de autoria). Risco pré-existente do padrão, agora exposto por criar NPCs/prédios; a API real subiu e respondeu sem erro, mas não foi estressada.
