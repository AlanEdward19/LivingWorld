# Comprar, posicionar e construir (#264, épico #246)

Modo comprar e construir do sandbox: um catálogo de móveis à venda, a casa que o morador monta e reforma, a beleza do cômodo. **Nada disto está no código**:
móvel, preço, beleza, categoria, tinta e custo da obra vêm de `scenarios/catalog/home-build.json` (bloco `Home`), estendido pelo período
(`CatalogExtends: [..., "moods", "home-build"]`). O código tem só o mecanismo. Decisão: AD-230. Sandbox: [sandbox.md](sandbox.md); cômodos: [house-rooms.md](house-rooms.md).

## O bloco `Home` (`HomeCatalog`, `HomeLoader`)
- `rules`: `resalePercent` (quanto a loja devolve ao recomprar), `deliveryMin` (da compra do estoque à entrega; 0 = na hora), `orderMarkupPercent` (a encomenda custa mais), `maxPieces`.
- `categories[]` `{id, name}`; `items[]` `{object, category, price, beauty, resource, wall?, rooms?, craft?:{hours, materials:{recurso: qtd}}}`: o `object` é um do catálogo de
  objetos (os novos, `plant`, `painting`, `bookshelf`, `rug`, `armchair`, `floorlamp`, são objetos como os outros, com partes e usos); `resource` é o recurso em que a loja o guarda;
  `wall` pede o fundo na parede; `rooms` limita os cômodos (o chuveiro no banheiro); `craft` diz o que a loja gasta e quanto leva para fazer sob encomenda.
- `paints[]` `{id, name, kind: floor|wall, color, beauty, price, default?}`; `build` `{contractor, wall, door, demolish, cell, maxExtraCells}` (preços em moeda do mundo).
- Referência quebrada recusa o cenário com o caminho do campo (`Home.items[0].object`); sem o bloco, o mundo não tem modo comprar e o hash não muda.

## Origem realista e moeda conservada
- A **loja de móveis** é um local do cenário (`Workplaces[]` do tipo `Home.build.contractor`) com estoque (um recurso por móvel), caixa e quem a abastece: o marceneiro (profissão 3)
  trabalha pela receita do local (`Recipes["2"]`: madeira vira móvel). Comprar tira 1 do estoque da loja e leva a moeda da carteira ao caixa; **sem estoque**, o motor encomenda
  se a loja tem o material (sai do estoque dela), cobra o acréscimo e entrega no prazo (evento agendado, `BodyTaskSystem`); sem material, "esgotado".
- Vender devolve `resalePercent` do preço **do caixa da loja** (sem caixa, recusa) e o móvel volta ao estoque; descartar não devolve nada. Nada nasce por regra.
- A obra (parede, porta, ampliar, pintura) é paga ao mesmo caixa (o marceneiro faz). Falta de dinheiro recusa com o motivo.

## O que o motor guarda: `Building.Edit` (`HomeEdit`)
Nulo = nada mudou (fora do snapshot e do hash). Móveis comprados (índice depois dos do catálogo, só cresce; vendido fica com `Gone`, o índice não troca: luzes e usos seguem),
movidos e vendidos; arestas editadas (`wall|open|door`); células de piso novas; pinturas; encomendas. A planta e os cômodos **de origem** continuam funções puras do prédio
(o planejador de móveis trabalha neles: a obra não reembaralha a mobília do catálogo); `HomeEditor` aplica a obra por cima: células e paredes de fora refeitas, cômodos de novo
como regiões ligadas por aresta aberta, cada um com o tipo do cômodo de origem em que mais tem células (dividir a sala dá duas "sala"). `BuildingLayoutCache`, `BuildingRooms`,
`HomeFurniture` e o chão do passo (`PlayerGround`) leem a planta de agora, então o corpo e os NPCs respeitam a parede nova.

## Validação (`HomeCommands`, verbos do `interact {kind:"home"}`)
`buy:objeto:andar:x:y:giro` · `move:peça:andar:x:y:giro` · `sell:peça` · `discard:peça` · `wall|open|door|shut:andar:ax:ay:bx:by` · `extend:x:y` · `paint:floor|wall:andar:x:y:tinta`.
Lugar: piso livre (nunca porta nem escada), sem outro móvel nem encomenda, fora da passagem das portas, cômodo permitido, fundo na parede se o móvel pede, e **toda célula livre segue
ligada à porta** (a mesma regra do planejador). Obra: só a aresta interior certa (parede de fora não muda), e depois de cada mudança a conexão com a porta é conferida (nada prende
ninguém); ampliar só encostado na casa, dentro do mapa, sem rua nem outro prédio, até `maxExtraCells`. Vai pela fila e pelo registro de sessões (replay igual).

## Beleza e o momento (`RoomBeauty`)
Beleza do cômodo = soma da beleza dos móveis que estão nele + média da pintura do piso + média da pintura das paredes (o não pintado vale a tinta `default`). Cache por versão da obra.
O catálogo de humor ganha o **gatilho de ambiente** `{kind: "state", ambient: "beauty", above|below}` (`MomentTrigger.Ambient`): vale enquanto a pessoa está no cômodo, lido na hora
(os momentos `pretty-room` "Cômodo bonito" e `ugly-room` "Cômodo feio" em `moods.json`).

## Contrato (só aditivo)
`GET /players/{id}/home` (catálogo com estoque, peças, cômodos com beleza, tintas, preço da obra, encomendas), `GET /players/{id}/home/check?verb=` (`{ok, reason, price, refund, note}`),
`interior.paint` (marcas e cor de cada tinta). Comprar e construir usam o `interact` de sempre (resposta com `note`).

## Cliente
`buy_mode.gd` (B): a câmera sobe (`camera_rig.set_build_view`, ortogonal), painel com abas Comprar / Minha casa / Construir, categorias, cartões com miniatura desenhada por código
(`furniture_thumb.gd`, o objeto do catálogo num SubViewport), preço, efeitos, beleza e estoque; fantasma verde ou vermelho pela resposta do motor (com o motivo), Q/E/R giram, clique
põe, botão direito e Esc cancelam; popup de pausa como os outros. Passos de captura: `buy`, `buyitem`, `buyfind`, `buyat`, `buyclick`, `buycut`, `buyhome`, `partner`.
