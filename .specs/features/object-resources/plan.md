# object-resources — #47 fatia 4 (comida e recursos como objetos)

Status: PASS (`verification.md`, Verifier independente). ADR-0039. Branch `feat/objetos-comida-fatia4` (as fatias 1–3 já estão na `develop`).

## Problema
Comer restaura fome e sede sempre a 100 (fixo em `TryApplyEat`) e os recursos do cenário não têm forma nem efeito próprios. A fatia 4 faz o recurso virar objeto do catálogo, com o efeito ao consumir vindo do descritor, sem mudar a regra de hoje.

## Fluxo
Cenário (`Objects[]` com `resourceId` e `effects: ["hunger:<n>", "thirst:<n>"]`) → loader valida (id único, nível 0–100, alvos só de recurso) → `WorldState.ObjectCatalog.ForResource(id)` →
`TryApplyEat` consome 1 unidade como sempre e restaura a necessidade a `max(atual, nível)` (nível = `ObjectUse.ConsumeLevel`, 100 sem descritor). Detalhes em `docs/domain/period-authoring-objects.md`.

## Critérios (EARS)
- QUANDO o cenário declara um objeto de recurso válido, ENTÃO carrega (`resourceId`, níveis) e sobrevive ao snapshot; inválido (id ≤ 0 ou repetido, nível fora de 0–100, alvo trocado entre recurso e mobília, alvo repetido) é recusado com o caminho.
- QUANDO um NPC come, ENTÃO fome e sede sobem ao nível do descritor de cada recurso (e nunca descem), consumindo 1 unidade de cada, como antes (`Eating_restores_hunger...`, `Eating_never_lowers...`, `A_smaller_meal_still_consumes_one_unit...`).
- PARIDADE: sem catálogo, sem descritor ou com `hunger:100`/`thirst:100`, ENTÃO o mundo dos NPCs (digest com estado e consumo) é idêntico ao de hoje e o snapshot de mundo sem `Objects` não traz `ObjectCatalog` nem `resourceId` (`Equivalent_descriptors_leave...`, `A_world_without_Objects_keeps...`, `Without_a_descriptor_or_a_catalog...`).
- TRATAMENTO: comida que restaura menos (`hunger:50`) faz consumir mais comida em ≥ 18/20 seeds, e o mundo muda ao mudar o nível (`A_food_that_restores_less...`, `Changing_the_descriptors_level...`); mesma seed e descritores, mesmo mundo.
- Dinheiro e estoque seguem conservados dia a dia com refeições menores (`Smaller_meals_keep_the_money_and_stock...`).
- Os recursos padrão (comida 1, água 2) vêm como objetos neutros (`hunger:100`/`thirst:100`) em `DefaultObjects`.

## Desempenho
`MeasureHarness` (1000 NPCs, 3 rodadas alternadas, base = cabeça da fatia 3 equivalente à develop). Catálogo desligado: base 0,813 / 0,823 / 0,849 µs/NPC-tick e 258 766–258 803 B/tick; depois 0,820 / 0,813 / 0,834 µs e 258 742–258 784 B/tick
(melhor 0,813 → 0,813 = 100%; alocação 100%); vilas idênticas. Catálogo ligado (informativo): 0,847 µs, 240 548 B/tick. O custo extra é uma consulta indexada por consumo.

## Portas de mão única
Campo `resourceId` no descritor e os alvos `hunger`/`thirst` com semântica de nível (aprovados no chat, ADR-0039).

## Fora de escopo
`Edible`/`Preparation` como efeitos do descritor, qualidade por lote, efeito que depende de quem come, forma do recurso no cliente (estoque, mão), invenções (fatia 5) e mods (6).
