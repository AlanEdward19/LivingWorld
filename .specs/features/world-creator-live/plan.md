# world-creator-live — #46 fatia 4 (criar família e local de trabalho com o mundo rodando)

Status: PASS (`verification.md`). ADR-0033. Branch `feat/world-creator`.

## Problema
`AuthoredHouseholds`/`Workplaces` só valiam ao criar o mundo. Faltava criar gente e prédio no mundo vivo, com rastro causal e a mesma validação.

## Fluxo
Console de admin (aba Criar) → `POST /authoring/households|workplaces` (token de admin, 409 em pausa de admin) → `WorldAuthoringService` →
loaders do cenário → `PopulationSeeder.SeedAuthored` / `WorkplacePlacer` (extraído do loader) → `Mint` do pecúlio → evento `AuthoredImmigration` /
`AuthoredWorkplaceFounded` → auditoria. Detalhes em `docs/domain/live-world-creation.md`.

## Critérios (EARS)
- QUANDO uma criação válida chega, ENTÃO o hash muda e o evento de origem está no log (`A_valid_family_arrives_changes_the_hash...`, `A_workplace_is_founded...`).
- QUANDO é inválida, ENTÃO 400 com o campo e hash igual, sem evento (`An_invalid_family_answers_400...`, `An_invalid_workplace_answers_400...`, `A_rejected_family_writes_nothing`).
- QUANDO há pausa de admin, ENTÃO 409 e nada muda; sem token, 403 (`Admin_pause_refuses_creation...`, `Without_the_admin_token...`).
- A família criada vive um ano pelas regras de sempre (`The_created_family_lives_a_year_by_the_usual_rules`); o dinheiro é cunhado, não criado.
- Mesma seed + mesmo comando = mesmo mundo em 2 processos (`Same_seed_and_live_creation_give_the_same_world_across_two_separate_processes`).
- Cenário sem rota usada: hashes e goldens iguais (a extração do `WorkplacePlacer` não muda nada).
- Cliente: corpos das rotas com inteiros como inteiros, resumo e recusas em palavras, botão que pede o criador (`test_live_creation.gd`).

## Desempenho
Nenhum sistema novo no tick: o custo por NPC-tick só muda se a rota for chamada. Motor sem usar a rota (`ScenarioRunner.Create`, seed 42, 1000 NPCs, 720 ticks, medido pelo Verifier, `a366629` → HEAD): 0,508 → 0,509 µs/NPC-tick (100%) e 172 234 → 172 297 B/tick (100,04%), dentro de 103% / 102%. Cliente: nada novo por quadro (aba só existe com token).

## Portas de mão única
Duas rotas, dois `WorldEventKind` no fim do enum, formato do corpo = cenário, `X-Admin-Token` obrigatório.

## Fora de escopo
Fila de comandos por tick, prédio de plantas livres (fatia 5), local de trabalho com posição escolhida no mapa (hoje por número).
