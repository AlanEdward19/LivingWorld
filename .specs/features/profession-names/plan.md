# profession-names — #46 fatia 1 (nomes das profissões)

Status: implementado, aguardando Verifier independente.

## Problema
Presets de `DefaultPeriodSeeder` declaravam `ProfessionIds: [1, 2]` sem nome; o menu mostrava "Profissão N".

## Fluxo
Nomes vivem em `Dynamics.ProfessionBiases[].Name` / `SkillBiases[].Name` (já expostos por
`GET /periods/{id}/catalog`). Presets passam a declarar 1=Lavrador, 2=Artesão; habilidades 0=Agricultura, 7=Artesanato.

## Critérios (EARS)
- QUANDO o catálogo de qualquer preset embutido é lido, ENTÃO toda profissão e habilidade tem nome
  (`SeedIfEmpty_names_every_profession_and_skill_of_every_builtin_preset` — falha se faltar nome).
- QUANDO um viés só de rótulo (peso 1.0) é declarado, ENTÃO o hash do mundo é igual ao do período sem `Dynamics`
  (`Profession_names_do_not_change_the_world_hash`).

## Decisão
O snapshot serializa `ProfessionWeights`; declarar viés mudava o hash mesmo com sorteio idêntico. `ScenarioLoaderV2`
agora só carrega pesos != 1.0 (1.0 já é o implícito) — semântica do sorteio inalterada.

## Fora de escopo
Fatias 2–6 da #46 (mudam contrato público; exigem ADR e pergunta).
