# object-effects — #47 fatia 3 (interações e efeitos aplicados pelo motor)

Status: PASS (`verification.md`, 3º Verifier independente). ADR-0038. Branch `feat/objetos-efeitos-fatia3` (a fatia 2 já está na `develop`, #101).

## Problema
Objeto só se desenha e se usa (fatias 1 e 2); nada no mundo muda por causa dele. Uma cama melhor não recupera o sono mais rápido e uma mesa não acelera a refeição.

## Fluxo
Cenário (`effects`, `interactions`) → loader valida → `WorldState.ObjectCatalog` (canônico, opcional) → `ObjectUse` (motor) acha, na mobília do tipo de prédio da moradia do NPC, o 1º objeto que atende à ação
e que ele pode usar → `DurationHours` divide a duração de Sleep/Eat por (1 + efeito) em casa; agendador, conclusão e apresentações usam a mesma função → `DecisionContext.Comfort` leva o efeito da moradia e multiplica a
utilidade de Sleep/Eat por (1 + conforto) → `usingObject` do interior respeita as mesmas interações. Sem catálogo tudo é neutro. Detalhes em `docs/domain/period-authoring-objects.md`.

## Critérios (EARS)
- QUANDO o cenário declara `effects`/`interactions` válidos, ENTÃO carregam e sobrevivem ao snapshot; inválidos (alvo desconhecido, magnitude ≤ -1, ação repetida/inexistente, requisito malformado) são recusados com o caminho.
- QUANDO a cama da moradia tem `sleep:m`, ENTÃO dormir dura `ceil(8/(1+m))` h (mín. 1) e comer idem com `eat:m`; efeito de um alvo não age no outro; fora de casa, sem móvel do tipo ou sem cumprir o requisito, não há efeito.
- QUANDO a cama é melhor (`sleep:1.0` contra `sleep:0`), ENTÃO o sono médio dos NPCs é maior em ≥ 18/20 seeds (`A_better_bed_keeps_the_npcs_better_rested_in_at_least_18_of_20_seeds`).
- QUANDO o efeito é 0, ENTÃO o mundo dos NPCs é idêntico ao de um mundo sem catálogo; QUANDO os efeitos ligam, ENTÃO o mundo muda; mesma seed e catálogo, mesmo mundo (`Effects_of_zero...`, `Turning_the_effects_on...`, `The_same_seed_and_catalog...`).
- O agendador e a conclusão concordam sobre o fim da ação (`The_wake_scheduler_and_the_completion_check_agree...`); dinheiro e estoque seguem conservados dia a dia (`Faster_meals_and_sleep_keep...`).
- O contexto de decisão carrega o conforto e a utilidade de Sleep/Eat é multiplicada por (1 + conforto), sem tocar outras ações (`The_decision_context_carries_the_comfort...`, `The_eat_comfort_scales...`).
- `usingObject` respeita `interactions` (`An_occupant_who_fails_the_objects_requirement...`). Sem catálogo ou com `effects` ausentes, nada muda (suite inteira e goldens inalterados).

## Desempenho
`MeasureHarness` (1000 NPCs, 3 rodadas alternadas, máquina ocupada). Base (develop): 0,812–0,835 µs/NPC-tick, 256 058–256 239 B/tick. Catálogo desligado: 0,815–0,836 µs (≈100%), 258 803–258 893 B/tick (101,1%); vilas idênticas.
Catálogo ligado com efeitos nos lares (mundo diferente por desenho): 0,834–0,844 µs (102,7%), 240 656–240 676 B/tick. O catálogo é indexado por id e tipo de prédio (sem texto nem enumerador por chamada) e `ObjectComfort` ocupa 8 B por contexto.

## Portas de mão única
Campos `effects`/`interactions` no descritor e a semântica "duração ÷ (1 + magnitude)" (aprovados no chat, ADR-0038). `DecisionContext` ganha `Comfort`.

## Fora de escopo
Estado por instância (desgaste), alvos `skill:`/`stock:`, dono do objeto, efeito fora de casa, comida/recursos como objeto (fatia 4), invenções (5), mods (6). Mobília padrão sem efeitos.
