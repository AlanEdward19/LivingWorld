# ADR-0034: Catálogo de objetos no cenário (formato versionado, interior servido por id)

- **Status**: aceito
- **Data**: 2026-10-15
- **Decisores**: Alan (issue #47, fatia 1; proposta aprovada em chat antes do código)

## Contexto
Mobília, ferramentas e comida eram arte fixa do cliente (`assets.json`, #40): caixas coloridas por tipo de prédio. A #47 quer um modelo
único de objeto como dado (forma, uso, interações, efeitos). A fatia 1 entrega só o formato, o catálogo e a migração da mobília; uso
(fatia 2) e efeitos (fatia 3) vêm depois sobre o mesmo formato.

## Decisão
- **Cenário**: `"objectsFormat": 1` + `"Objects": [ {id, category, variationSeed, shape[], usePoints[]} ]` + `"Furnishings"`
  (`"default"` ou id numérico do tipo de prédio → lista ordenada de ids). Cada parte de `shape`:
  `{shape: box|cylinder|sphere|capsule, size, offset, color(hex 6), symmetry: none|x|z|xz}`; `offset` = centro da parte relativo à base do objeto.
- **Borda**: `ObjectCatalogScenarioLoader` recusa, com o caminho do campo, versão desconhecida, `objectsFormat` ausente, parte desconhecida,
  dimensão ≤ 0 ou não finita, cor inválida, id repetido e `Furnishings` que cita id inexistente.
- **Mundo**: `WorldState.ObjectCatalog` (nulo = cenário sem `Objects`), `[Canonical]`, omitido do snapshot quando nulo: **hash idêntico ao de antes**.
- **Snapshot de interior**: `objects: {format, catalog[], furniture[{id, variation}]}`, omitido sem catálogo. A variação é índice
  determinístico (`ObjectVariation.Index(variationSeed, posição)`); seed 0 = sem variação.
- **Cliente**: `object_factory.gd` instancia por descritor com mesh e material compartilhados por (id, variação). Sem `objects`
  (ou id fora do catálogo), cai no `assets.json`, que passa a ser só fallback.
- **Mobília atual migrada**: `DefaultObjects` gera os mesmos 9 objetos (caixa única, mesmo tamanho e cor) e a tabela por tipo de prédio; o
  `DefaultPeriodSeeder` os inclui. O interior renderizado é o mesmo.

## Alternativas consideradas
- Enum de categoria — fecharia o conjunto; mods e invenções (fatias 5–6) precisam de categorias abertas.
- Um mesh por instância — custo por objeto e por quadro; o catálogo compartilhado mantém o FPS.
- Cliente lê o catálogo do arquivo — dois caminhos de verdade; o servidor é a fonte (ADR-0005, ADR-0022).

## Consequências
- **Positivas**: objeto novo é dado; base pronta para uso/animação, efeitos, comida, invenções e mods.
- **Negativas**: `Furnishings` é um bloco irmão de `Objects` (a proposta citava só `Objects`); só apresentação até a fatia 3 dar consumidor causal.
- **Follow-ups**: fatia 2 (poses e pontos de uso), 3 (interações/efeitos pelo motor, token `alvo:magnitude`), #46 usa este catálogo.
