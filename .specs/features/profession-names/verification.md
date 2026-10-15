# Verification — profession-names (#46 fatia 1)

Verifier: independente (sub-agent novo; não escreveu o código). Commit verificado: cedecb8.
Diff: `git diff develop...HEAD` (seeder, ScenarioLoaderV2, DefaultPeriodSeederTests, plan.md).

## Critérios

| # | Critério | Veredito | Evidência |
|---|---|---|---|
| 1 | `GET /periods/<preset>/catalog` devolve nome p/ todo id de profissão e habilidade nos 3 presets | PASS | API real (`dotnet run`, :5214) + curl nos 3 presets: `professionIds [1,2] / professionNames {1:Lavrador,2:Artesão} / skillIds [0,7] / skillNames {0:Agricultura,7:Artesanato}` em vila-pequena, cidade-media e grande-metropole. Código: `DefaultPeriodSeeder.cs:72-83` (bloco único em `BuildPayload`, vale p/ os 3 presets). |
| 2 | Existe teste que FALHA se preset tiver profissão/habilidade sem nome | PASS | `DefaultPeriodSeederTests.cs` `SeedIfEmpty_names_every_profession_and_skill_of_every_builtin_preset`. Mutação A (removi `Name` "Artesão"): FAIL `profissão 2 sem nome`. Mutação B (removi `Name` "Artesanato"): FAIL `habilidade 7 sem nome`. Restaurado. |
| 3 | Nenhum "Profissão N" no menu com presets padrão | PASS (servidor) / NÃO VERIFICÁVEL (cliente) | `main_menu.gd:530` só cai em `"Profissão %d"` se `professionNames` não tem o id; com crit. 1 isso não ocorre nos presets. `godot` não instalado (`which godot` vazio) -> `client-test.sh` não rodou. Lacuna: a issue pede "teste headless"; nenhum teste de cliente foi adicionado (`tests/test_main_menu.gd:15` só cobre um período fake). A garantia vem do teste de servidor (crit. 2). |
| 4 | Hash do mundo não muda (nome é rótulo) | PASS | `Profession_names_do_not_change_the_world_hash` compara hash do preset com `Dynamics` vs. sem `Dynamics` nos 3 presets: verde. Mutação C (`Where(b => true)` em `ScenarioLoaderV2.cs:38`, ou seja, reverter o filtro): FAIL no mesmo teste. Restaurado. |
| 5 | Sorteio de profissão equivalente ao uniforme com só pesos 1.0 | PASS | `ScenarioLoaderV2.cs:37-44`: pesos 1.0 são filtrados; `weights.Count==0` => catálogo original, `ProfessionWeights` nulo => ramo uniforme original (`PopulationCatalog.cs:36-39`). Ausente do dicionário já valia 1.0 (`GetValueOrDefault(id,1.0)`, linhas 42/47), logo descartar 1.0 não altera nenhum caso misto. |
| 6 | Pesos != 1.0 continuam funcionando | PASS | Mutação D (`Where(b => false)`, descarta todos os pesos): FAIL `PeriodCausalTests.Declared_profession_bias_shifts_the_initial_population_toward_the_favored_profession_in_most_seeds`. Restaurado. |

## Mutações (todas reprovaram; todas restauradas)
A/B: remover Name de profissão/habilidade -> teste de nomes reprova.
C: reverter filtro `Weight != 1.0` -> teste de hash reprova.
D: descartar todos os pesos -> `PeriodCausalTests` reprova (unit).
Após cada mutação: `git checkout -- <arquivo>`; `git status --short` limpo.

## Gate
- `bash scripts/test.sh --project integration --filter "DefaultPeriodSeederTests|PeriodsEndpointTests"`: 14 passed, 0 failed.
- `bash scripts/verify.sh`: `verify: OK` (unit: 2243 passed, 3 skipped, 0 failed).
- `bash scripts/client-test.sh`: NÃO RODADO (Godot ausente neste ambiente). O diff não toca `client/godot/`.

## Observações (não bloqueantes)
- Nenhum teste HTTP lê `/periods/<preset>/catalog` dos presets semeados; o teste de nomes valida via `PeriodCatalog.From`, a mesma fonte do endpoint (verificado manualmente acima).
- Métricas de desempenho não exigidas aqui (mudança só de dados/carregador).

## Veredito geral: PASS (sem FAIL). Ressalva: critério 3 no cliente não verificado por falta de Godot.
