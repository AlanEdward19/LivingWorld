# Cliente Godot: lacunas de contrato para a etapa 2 (ADR-0047)

Levantadas na etapa 1 (AD-169), que não mudou o contrato. Cada linha diz o campo que falta, de onde viria
e onde o cliente já tem o lugar para usá-lo. Tudo só leitura, por acréscimo (decisão 4 do dono no ADR-0047).
**Estado**: a etapa 2 (AD-170) entregou os campos; a etapa 3 (AD-171) os consome. O que ainda falta está em
`.specs/features/realism-backlog/09-cliente-visual-e-jogador.md`, seção 1b.

## Corpo e ação de todos os NPCs (escopo: `livingState.npcs`, `npcUpserts`)

| Campo | Onde o cliente usaria |
|---|---|
| idade (anos) e profissão (`professionId`) | `Scenes/render/npc_renderer.gd` `apply_known_model`: hoje só quem teve a ficha lida (`/npcs/{id}`) troca de corpo; o resto fica no modelo base. Não fazemos N chamadas por quadro. |
| sexo, altura, gravidez, doença, luto/medo (sinais visíveis) | `npc_actor.gd` (corpo e pose) e a dica de `selection.gd` |
| alvo da ação (`actionTarget` {kind,id}) | já vem só em `/npcs/{id}`; no escopo daria "Ana furtando de Beto" na dica e linha entre os dois |
| nome do NPC | o escopo não traz; o cliente pagina `GET /npcs` (até 1.000) a cada 20 s para ter nomes |

## Ficha da pessoa (`GET /npcs/{id}`)

| Campo | Onde |
|---|---|
| filhos (`childIds`) | `inspector.gd` `person_links`: hoje "Filho(a)" só aparece para filhos cuja ficha já foi aberta (registro aprende pelo `motherId`/`fatherId`) |
| prédio da casa (`household` → `buildingId`) | link "Casa" abre a casa; seguir família usaria o prédio em vez do centro dos moradores conhecidos |
| nome, tipo e prédio do local de trabalho (`employer` → `{name, locationTypeId, buildingId}`) | link "Trabalho" e título da janela de vendas mostram "Local de trabalho nº N" |

## Prédios

| Campo | Onde |
|---|---|
| dono (`ownerId`) e moradores (`residentIds`) | ficha de prédio (`_describe_building`); hoje só "quem está dentro" quando o interior está aberto |
| função/nome do prédio além do tipo | título da ficha e dica |
| `groundFootprint`, `floors`, `value`, `lodging` em `buildingUpserts` | `session.gd` `fill_pending_footprints`: hoje o prédio novo aparece como canteiro 2x2 e o cliente lê UMA vez o retrato da cidade para pegar a planta |

## Processos e lavoura (`processUpserts`, `livingState.processes`)

| Campo | Onde |
|---|---|
| estágio da lavoura, cultura/recurso, dono, colheita esperada | `process_renderer.gd` `crop_color` (hoje só pelo `descriptorKey` + progresso) e ficha de processo ("Lavoura nº N") |
| nome do recurso do processo de material | ficha de processo |

## Eventos (`events[]` do delta)

| Campo | Onde |
|---|---|
| id do evento e envolvidos (autor, vítima, beneficiário, ex.: `SupportPaid`) | ficha de evento (`inspector.gd`, tipo `event`) ganharia links para as pessoas |
| `location` em mais tipos de evento | `render/event_markers.gd`: só evento com lugar vira marcador |

## Interior

| Campo | Onde |
|---|---|
| nome legível do objeto e material (`ObjectDescriptor.name`, material) | ficha de objeto e dica (hoje o id do catálogo) |
| andar de cada peça de mobília | `interior_view.gd` só registra uso no térreo |

## Mundo

| Campo | Onde |
|---|---|
| estação e clima (camada `Climate` hoje `isModeled: false`) | `terrain_renderer.gd` (cor do chão, neve) e `day_cycle.gd` |
| fauna (animais com posição e espécie) | renderizador novo + inspetor tipo `animal` |
| quem está dentro de cada cidade no mapa-múndi (densidade) | LOD de mundo (etapa 3) |

## Rotas

- **Inspeção por tipo de entidade** (`GET /entities/{kind}/{id}`: pessoa, prédio, lavoura, objeto, local de
  trabalho, casa, evento): o inspetor hoje junta store + `/npcs/{id}` + `city_snapshot`; viraria uma leitura só,
  passando pelo mesmo `Contract/world_reader.gd`.
- **Resumo leve de cidade** (nome, indicadores, moradores pendentes): a ficha de cidade fora do escopo lê o
  `subscribe?scope=City` inteiro só para mostrar indicadores.
