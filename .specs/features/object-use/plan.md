# object-use — #47 fatia 2 (uso e animação atrelados ao objeto)

Status: PASS (`verification.md`, 2º Verifier independente). ADR-0037. Branch `feat/objetos-uso-fatia2`, sobre a fatia 1, já mergeada (PR #99).

## Problema
A pose do NPC vem só da ação e a posição só da célula do servidor: dormir é "deitado em pé no chão com uma cama desenhada junto". O objeto não diz onde o corpo encosta nem que pose faz.

## Fluxo
Cenário (`usePoints[]` com `action`, `pose`, `offset`, `facing`) → loader valida → `InteriorProjector` calcula `usingObject` por ocupante (1º objeto da mobília do tipo de prédio
cujo ponto atende a ação) no snapshot e no delta → `WorldStore` guarda o ocupante inteiro → `interior_view` posiciona o NPC no ponto de uso da peça e `animation_catalog.pose_for(ação, pose_do_objeto)`
resolve a pose. Sem catálogo/`usingObject`: caminho de antes. Detalhes em `docs/domain/period-authoring-objects.md`.

## Critérios (EARS)
- QUANDO um NPC dorme num prédio cuja mobília tem cama com ponto de uso `Sleep`, ENTÃO o ocupante traz `usingObject = "bed"` e o cliente o põe no ponto de uso da cama com a pose declarada (`A_sleeping_occupant_of_a_house_uses_the_bed...`, `test_a_sleeper_is_placed_at_the_beds_use_point...`).
- QUANDO o objeto não declara pose, ENTÃO vale a pose da ação (`test_an_object_without_a_declared_pose...`, `A_use_point_without_action_or_pose_is_valid...`).
- QUANDO o objeto declara uma pose, ENTÃO ela vence a da ação (`test_the_pose_comes_from_the_object...`, `test_a_declared_pose_wins_over_the_action_pose...`).
- QUANDO a ação não é atendida por nenhuma peça do prédio, ou não há catálogo, ENTÃO nada muda: sem `usingObject`, posição e cama própria de antes (`An_action_no_furniture_serves...`, `Only_the_furniture_of_the_building_type_counts`, `A_world_without_Objects_reports_no_object...`, `test_a_world_without_objects_behaves_as_before`).
- A escolha é a primeira peça da mobília que atende à ação (`The_first_piece_in_furnishing_order...`); vários NPCs no mesmo objeto ocupam peças diferentes em ordem de id (`test_two_sleepers_take_different_beds...`, `test_a_third_sleeper_wraps_around...`).
- Ação ou pose desconhecida no cenário é recusada com o caminho (`An_unknown_action_id_or_pose_is_refused...`); o vocabulário de poses é o do cliente (`The_pose_vocabulary_is_the_one_the_client...`).
- Trocar só o objeto em uso é uma mudança de delta (`Changing_only_the_object_in_use_counts_as_a_move...`, `test_the_pose_changes_when_only_the_object_in_use_changes`, `test_the_store_keeps_using_object...`).
- Outro andar não usa objeto do térreo (`test_an_upper_floor_occupant_does_not_use...`). O hash do mundo não muda (o campo não é estado).

## Desempenho
Motor: nada novo no tick; `usingObject` só é calculado ao projetar um interior observado (mapa ação→objeto por chamada, mobília de ≤ 6 peças). Cliente: nada por quadro; só ao abrir/atualizar interior.
Motor, `MeasureHarness` (1000 NPCs, 4 rodadas alternadas, cabeça da fatia 1 → esta; máquina com carga 8–11, rodadas ruidosas): melhor µs/NPC-tick 0,780 → 0,773 (99%); alocação 255 275–255 371 → 255 278–255 426 B/tick (≈100%); trajetórias das vilas idênticas.
Cliente `stress:500` (3 rodadas, carga alta): 77,8 / 72,9 / 74,9 FPS (base registrada 75,9–77,2; piso acima de 50, dentro de ±3 quando a máquina está ociosa).

## Portas de mão única
`usePoints[].action`/`pose` no cenário e `usingObject` no ocupante do interior (aprovados no chat, ADR-0037).

## Fora de escopo
Interações com requisitos e efeitos aplicados pelo motor (fatia 3), posição das peças pelo servidor, uso de objetos fora do interior, animações novas além das 8 poses.
