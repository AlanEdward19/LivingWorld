# object-catalog — #47 fatia 1 (formato de objeto + catálogo com fallback)

Status: PASS (`verification.md`, 7º Verifier independente). ADR-0034. Branch `feat/objetos-catalogo-fatia1`.

## Problema
Mobília é caixa colorida fixa no `assets.json` do cliente; não há modelo de objeto como dado. Sem ele, uso, efeitos, comida, invenções e mods (fatias 2–6) não têm onde pendurar.

## Fluxo
Cenário (`objectsFormat`, `Objects`, `Furnishings`) → `ObjectCatalogScenarioLoader` (valida) → `WorldState.ObjectCatalog` (snapshot) →
`InteriorProjector` (`objects` no snapshot de interior) → `WorldStore.interior.objects` → `interior_view` + `object_factory` (mesh/material por id e variação).
Sem catálogo: caminho antigo (`assets.json`). Detalhes em `docs/domain/period-authoring-objects.md`.

## Critérios (EARS)
- QUANDO o cenário tem `Objects` válido, ENTÃO carrega e a mobília por tipo de prédio sai do catálogo (`The_default_objects_load...`, `The_interior_snapshot_lists_the_catalog_furniture...`).
- QUANDO a forma é incoerente (dimensão ≤ 0, parte desconhecida, versão desconhecida, id repetido, mobília órfã), ENTÃO recusa com o caminho (`A_non_positive_dimension...`, `An_unknown_part...`, `An_unknown_format_version...`, `A_duplicate_id...`, `A_furnishing_that_names...`).
- QUANDO o cenário não tem `Objects`, ENTÃO nada muda: campo omitido do snapshot, hash igual, interior pelo `assets.json` (`A_world_without_Objects_keeps_the_hash...`, `..._has_no_objects_field`, `test_without_objects_the_assets_json_fallback_is_used`).
- QUANDO há `Objects`, ENTÃO o catálogo sobrevive ao snapshot e entra no hash (`The_catalog_survives_a_snapshot_round_trip...`).
- A mesma seed e posição dão a mesma variação; seed 0 nunca varia (`The_same_seed_and_slot...`, `Furniture_variation_is_the_same...`).
- O interior do catálogo é igual ao de hoje: contagem, posição, tamanho e cor das peças (`test_interior_from_the_catalog_matches_the_assets_json_interior`).
- Mesmo (id, variação) compartilha mesh e material; 50 objetos criam 1 mesh por (id, variação) (`test_instances_of_the_same_id...`, `test_fifty_objects...`).
- As chaves JSON do servidor são as que o cliente lê (`The_interior_snapshot_json_has_the_keys_the_client_reads`, `test_the_store_keeps_the_objects...`).

## Desempenho
Nada novo no tick (catálogo só no snapshot; nulo por padrão). Motor, `MeasureHarness` (1000 NPCs, melhor de 3, 2 rodadas alternadas develop/branch):
antes 0,795 / 0,973 µs, 255 315 / 255 296 B/tick; depois 0,805 / 0,787 µs, 255 280 / 255 244 B/tick (melhor: 0,795 → 0,787 = 99%; alocação 100%); vilas idênticas.
Cliente `stress:500` (3 rodadas): antes 66,8¹ / 76,7 / 76,0; depois 75,9 / 76,2 / 76,7. ¹ primeira execução do worktree novo (importação).

## Portas de mão única
Formato `objectsFormat: 1` no cenário e `objects` no snapshot de interior (aprovado no chat, ADR-0034). `Furnishings` como bloco irmão.

## Fora de escopo
Poses e pontos de uso em ação (fatia 2), interações/efeitos (3), comida (4), invenções (5), mods (6), objetos fora do interior (cidade/mundo).
