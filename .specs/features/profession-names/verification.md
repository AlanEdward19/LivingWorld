# Verification — profession-names (#46 fatia 1)

Verifier independente (contexto novo; não escreveu o código). Commit verificado: 721c71a (branch feat/profession-names).
Refeito do zero; o verification.md anterior não foi usado como evidência.

## Veredito: PASS

## Critérios
| # | Critério | Resultado | Evidência |
|---|---|---|---|
| 1 | Todo id de profissão e habilidade dos presets embutidos tem nome | PASS | `DefaultPeriodSeeder.cs` (+12 linhas, `Dynamics.ProfessionBiases/SkillBiases` com Name: 1=Lavrador, 2=Artesão, 0=Agricultura, 7=Artesanato). `DefaultPeriodSeederTests.SeedIfEmpty_names_every_profession_and_skill_of_every_builtin_preset` itera todos os payloads semeados, via `PeriodCatalog.From` (mesma origem do `GET /periods/{id}/catalog`). `scripts/test.sh --project integration --filter Periods`: 24 passed, 0 failed. |
| 2 | Teste falha se faltar nome | PASS | Mutação (a): removido `Name` de "Artesão" -> `Failed ...names_every_profession_and_skill... "profissão 2 sem nome"`. |
| 3 | Nenhum "Profissão N" no menu com presets padrão (headless) | PASS | `client/godot/tests/test_main_menu.gd`: `test_default_presets_catalog_shows_names_not_numbered_fallback` + braço de controle `test_unnamed_profession_still_falls_back_to_numbered_label`. `bash scripts/client-test.sh`: 235 passed, 0 failed (ambos `ok`). |
| 4 | Teste de cliente discrimina | PASS | Mutação (c): `main_menu.gd:530` forçado a `"Profissão %d"` -> FAIL em `test_default_presets_catalog_shows_names_not_numbered_fallback` (e em `test_new_world_lists_periods_previews_and_creates`). |
| 5 | Hash do mundo inalterado pelos nomes | PASS | `Profession_names_do_not_change_the_world_hash` compara `CanonicalHash` com e sem `Dynamics` em todos os presets; passa. Filtro `Where(b => b.Weight != 1.0)` em `ScenarioLoaderV2.cs` (~linha 38). |
| 6 | Teste de hash discrimina | PASS | Mutação (b): filtro trocado por `Where(b => true)` -> `Failed ...Profession_names_do_not_change_the_world_hash`. |
| 7 | Pesos != 1.0 ainda funcionam | PASS | `ScenarioLoaderV2Tests` (peso 2.0, linha 184) e `PeriodCausalTests` passam (15 passed, 0 failed). |

## Gates
- `bash scripts/verify.sh`: OK (Unit 2243 passed, 3 skipped pré-existentes, 0 failed).
- `bash scripts/client-test.sh`: 235 passed, 0 failed (Godot_mono encontrado pelo script; avisos de leak no exit são do Godot, não falhas).
- Mutações (a), (b), (c) restauradas com `git checkout --`; árvore limpa exceto este arquivo.

## Observações (não bloqueantes)
- `verify.sh` roda só o projeto Unit; os testes novos de servidor estão em Integration, rodados à parte aqui (24 passed). O gate padrão não os cobre.
- Não foi subida a API real: o endpoint de catálogo foi coberto indiretamente via `PeriodCatalog.From` (mesma fonte do endpoint).
- Medição de FPS/custo por NPC-tick não aplicável: mudança é só de rótulo + filtro de pesos no carregamento.
