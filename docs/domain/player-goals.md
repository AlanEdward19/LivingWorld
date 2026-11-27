# Metas, habilidades e diário do jogador (#235, épico #228)

O jogador que controla uma pessoa precisa saber o que fazer, como num RPG, mas **nenhuma missão é escrita à mão**: as metas
nascem do estado do motor, na hora do pedido. Só leitura, sem RNG novo, sem LLM; custo zero por tick (roda sob pedido).

## Rotas (só leitura, mesma identificação de `/reach` e `/time`; 404 sem corpo)
- `GET /players/{id}/goals`: `goals[]` (`kind`, `reason`, `urgency` 0..100, `step`), `skills[]`, `urgencyLine`.
- `GET /players/{id}/diary`: `entries[]` do mais novo ao mais velho (teto 60), `controlledSinceMs`.

## De onde vem cada meta (`PlayerGoals`)
| Meta (`kind`) | Estado do motor que a cria |
|---|---|
| `need.hunger/thirst/sleep/social` | falta (100 − necessidade) ≥ 50; zerada = 100 |
| `family.child-hungry` | filho dependente da casa com fome ou sede |
| `family.spouse-sick` / `family.member-sick` | cônjuge ou morador com saúde < 50 |
| `money.rent-arrears` / `home.homeless` / `money.broke` | aluguel atrasado, sem teto, carteira zerada |
| `work.report` / `skill.grow` | tem emprego e não está no posto; ofício abaixo do teto |

## O próximo passo (`step`)
Sempre algo que os comandos de hoje fazem: `interact` (a oferta de `/reach` já está ao alcance: `kind`, `targetId`, `verb`,
`resourceId`), `go` (andar até a célula `x,y`; `distance` em células) ou `act` (a ação em `verb`). `none` só se o mundo não
oferece nada. Alcance curto: a água a ≤ 1 célula da posição fina; `nature`/`gather` (#239/#241) só junto de árvore (ver abaixo). Escolha por alcance e distância, desempate por id: determinístico.

## Habilidades (`PlayerSkills`)
O ganho é o do motor (`SkillPracticeSystem.PracticeGain`): quem está em `Work` no próprio posto sobe, NPC ou jogador, pelo mesmo
caminho (o `interact work` e o `act Work` do jogador caem nele; teste prova). Níveis são 10 faixas de `Cap/10` pontos; a consulta
traz nível, `toNext`, `fraction`, `practicing` e `gainPerPractice`.

## Diário (`PlayerDiary`)
Reaproveita a linha do tempo da biografia (`NpcBiographyQuery`, os fatos do mundo) e o registro de sessão (o que o jogador fez:
`Act`, `Interact`, `Strike`...). `milestone` marca marcos de vida; `whileControlled` separa o que veio depois de assumir o corpo.
Texto do catálogo de apresentação (`LivingEventPresentationCatalog`), nunca da LLM.

## Cliente
Tecla **J** abre "Metas e diário"; a meta mais urgente fica numa linha discreta sob o relógio (atualiza a cada 5 s).

## Ponto de partida e chão (#238)
- `GET /players/{id}/goals?start=true` acrescenta `start` (`PlayerStartQuery`, só sob pedido: a busca da mata custa): `child`, `homeless`,
  `home` (`x`,`y`,`distance`, `alone`, `members[]` com `relation` spouse/child/parent/housemate, `pantry[]`), `job`, `vacancy`, `wild`
  (a mata mais perto, a regra da oferta `nature`) e `pressing` (hunger, thirst, sleep, money, none). Sem texto: o cliente escreve o guia.
- A pose do `walk` traz `ground` (`PlayerWalk.DragOf`): `terrain`, `terrainPct` (peso sobre o terreno mais fácil), `slowPct` (quanto o passo
  está abaixo do normal) e `cause` (load, tired, weather, body, ground). O passo do jogador usa a referência do par de células, então o
  terreno em si quase não muda a velocidade; o que atrasa de fato é carga, sono, lama/neve e corpo.
- Cliente: o guia da primeira posse (`player_guide.gd`, uma vez por mundo, F1 reabre), "O que dá para fazer" na ajuda e o chão no cartão.

## Cortar lenha e ação em andamento (#239, #241)
- **Árvore**: `PlayerNature.TreeAt` é a árvore que o cliente desenha (`scenery.gd`): célula seca fora do contorno das cidades (+1), sorteio
  `cell_hash` < 0,16, tronco deslocado ±0,3 célula. O mesmo `cell_hash` nos dois lados, com teste de paridade (.NET e Godot). A oferta `nature`
  (`label` "Árvore", `fx`/`fy` = o tronco em células) só aparece a ≤ 1,5 m dele; o anel de mira realça o tronco.
- **Duração**: uma sessão corta o que cabe numa jornada (`CrewHoursPerDay`) no ritmo por unidade dos NPCs (`WorkHaul.CutHours` da carga que o
  corpo aguenta, sem a viagem, que o jogador anda). Os ~67 h antigos eram o corte da carga cheia (20 un.) de uma vez, em horas de trabalho.
- **`busy`** na pose de todo comando (aditivo, opcional): `action` (hoje `gather`), `untilMs`, `totalMs`, `remainingMs`. A tarefa mora em
  `Npc.BodyTask`; enquanto dura, `walk`/`gather` dão 409. Outras ações (beber, dormir, usar) são instantâneas no motor: só gesto.
- **Interromper**: `POST /players/{id}/interrupt` e `{"command":"interrupt"}` no canal. A carga fica proporcional ao tempo cortado, para baixo,
  sem sorteio; 409 sem ação em andamento. Entra no registro (replay).
- Cliente (`body_busy.gd`): barra acima do cartão ("Cortando lenha — 40%"), golpe de machado em laço, WASD ocupado não manda passo e diz
  "Ocupado: cortando lenha (Espaço para parar)", Espaço interrompe. Todo erro do servidor passa por `human_error` (sem prefixo nem ms).
