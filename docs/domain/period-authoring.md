# Autoria de período (para IA externa)

Índice do contrato canônico para gerar um `periodDefinition` válido e enviá-lo à rota
`POST /periods` do LivingWorld. Escrito para uma IA (ou pessoa) **fora** deste projeto — não
pressupõe acesso ao código-fonte, só a estes arquivos.

Leia [`society.md`](society.md), [`economy.md`](economy.md) e
[`genetics-and-family.md`](genetics-and-family.md) para o **significado** dos conceitos
(profissão, economia, população). Estes arquivos cobrem só a **forma** do dado.

## O que é um período aqui

Um período **não é um catálogo fixo de conteúdo** ("idade medieval tem ferreiro e lavrador").
É um *startpoint*: mapa inicial, população inicial, regras de comportamento/economia/cidade, e
opcionalmente **vieses e regras de evolução** que dizem como profissões podem nascer, se fundir,
se dividir ou desaparecer: cada regra dispara uma única vez, no primeiro tick em que o clock
alcança seu `TriggerTick` (ausência de `TriggerTick` = dispara desde o tick 0), reatribui os NPCs
afetados e nunca reaplica (idempotente por pertença ao catálogo, sem estado extra no mundo). O
motor nunca vê um nome de profissão/época — só ids inteiros que este período declara.

## Documentos

| Documento | Conteúdo |
|---|---|
| [`period-authoring-schema.md`](period-authoring-schema.md) | Campos obrigatórios: mapa, população, comportamento, economia, cidades |
| [`period-authoring-dynamics.md`](period-authoring-dynamics.md) | Bloco opcional `Dynamics` — vieses e regras de transformação de profissão/habilidade |
| [`period-authoring-flow.md`](period-authoring-flow.md) | Exemplos válido/inválido, fluxo de `POST /periods`/`POST /worlds/start`, checklist final |

Referências de formato real: `scenarios/default.json`, `scenarios/test-scifi.json`, e os
payloads de teste em `tests/LivingWorld.Tests/Periods/`.
