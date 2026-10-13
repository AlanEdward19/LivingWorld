# Authored households [Estado: Spec - fatia 3 do menu (criar famílias e NPCs estilo Sims, no novo mundo)]

## Problem

O cenário só tem `InitialPopulation`: `PopulationGenerator` sorteia idade, sexo, personalidade, profissão e
casais. Não dá para o usuário criar uma família ou uma pessoa específica (pedido: "igual The Sims 4").
Evidência: `PopulationSeeder.SeedInitial`, `ScenarioLoaderV2` (nenhum membro de cenário descreve NPC).

Quando isto entrar, o cenário aceita `AuthoredHouseholds`: famílias com membros definidos (nome, sexo,
idade, profissão, personalidade, parentesco) que nascem junto da população inicial, e o menu monta isso.

## Flow

1. `AuthoredHouseholdsLoader` (new, Simulation/Population) - valida o bloco na borda, erro nomeia o campo
2. `PopulationGenerator.GenerateAuthored` (new, Domain) - monta NPCs/Households sem sortear o autorado
3. `PopulationSeeder.SeedAuthored` (new) - registra no mundo, mesma colocação de casa e agenda de óbito
4. `ScenarioLoaderV2` (exists, extended) - chama o seeder depois da população sorteada
5. Cliente: `world_editor.gd` (edita o bloco em texto), `family_builder.gd` (new, tela de criar família)

## Impact

| Front | What changes |
| --- | --- |
| domain | termos: família autorada, membro autorado |
| stored data | none - NPCs/households já são persistidos; sem coluna nova |
| determinism | cenário sem `AuthoredHouseholds` = mesmo hash de hoje (S3) |

## Relations

`Member.Spouse`/`Member.Parents` apontam índices dentro da MESMA família (nunca entre famílias).

## Surface

Contrato público novo: membro de cenário `AuthoredHouseholds` (ADR-0024). Rota nova: nenhuma.

## Landing

| One-way door | Literal shape | Alternative rejected |
| --- | --- | --- |
| 1. forma do bloco | `[{"Settlement":"vila","Members":[{"Name","Sex":"Female|Male","AgeYears","ProfessionId","Personality":{10 traços}?,"Spouse":i?,"Mother":i?,"Father":i?}]}]` | rota `POST /npcs` ao vivo: fora do escopo, mexe em estado vivo e hashes |

## Criteria

### S1: Cenário (P1)

1. WHEN o bloco é válido THEN o mundo SHALL ter os membros com exatamente nome, sexo, idade, profissão e personalidade dados
2. WHEN há `Spouse`/`Mother`/`Father` THEN os vínculos SHALL ser recíprocos e na mesma casa
3. IF um campo é inválido (profissão fora do catálogo, índice inexistente, idade fora de 0..longevidade, família sem adulto) THEN o carregamento SHALL falhar nomeando o campo
4. WHEN `Settlement` nomeia um assentamento THEN a família SHALL morar nele; ausente = vila inicial

### S2: Determinismo (P1)

5. WHEN o cenário não tem o bloco THEN o hash canônico SHALL ficar idêntico ao de antes
6. Mesma seed + mesmo bloco SHALL gerar o mesmo mundo

### S3: Menu (P1)

7. WHEN o usuário monta uma família no menu THEN o cenário enviado SHALL levar o bloco e o mundo criado SHALL ter essas pessoas
8. O menu SHALL oferecer prédios do catálogo (local de trabalho: tipo, posição, vagas) editando `Workplaces`, que o cenário já aceita

## Out of scope

Editor de plantas (paredes, cômodos, mobília), criar NPC/prédio com o mundo rodando, aparência além do
corpo gerado, poderes novos. Cada um é fatia própria.

## Sources

`PopulationGenerator.cs`, `PopulationSeeder.cs`, `ScenarioLoaderV2.cs`, `.specs/features/godot-main-menu/plan.md`
