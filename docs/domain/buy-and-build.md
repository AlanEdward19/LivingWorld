# Comprar, posicionar e construir (#264, épico #246)

Modo comprar e construir do sandbox: um catálogo de móveis à venda, a casa que o morador monta e reforma, a beleza do cômodo. **Nada disto está no código**:
móvel, preço, beleza, categoria, tinta e custo da obra vêm de `scenarios/catalog/home-build.json` (bloco `Home`), estendido pelo período
(`CatalogExtends: [..., "moods", "home-build"]`). O código tem só o mecanismo. Decisão: AD-230. Sandbox: [sandbox.md](sandbox.md); cômodos: [house-rooms.md](house-rooms.md).

## O bloco `Home` (`HomeCatalog`, `HomeLoader`)
- `rules`: `resalePercent` (quanto a loja devolve ao recomprar), `deliveryMin` (sem uso desde o #265: a entrega é `haul.delivery`), `orderMarkupPercent` (a encomenda custa mais), `maxPieces`.
- `categories[]` `{id, name}`; `items[]` `{object, category, price, beauty, resource, wall?, rooms?, craft?:{hours, materials:{recurso: qtd}}}`: o `object` é um do catálogo de
  objetos (os novos, `plant`, `painting`, `bookshelf`, `rug`, `armchair`, `floorlamp`, são objetos como os outros, com partes e usos); `resource` é o recurso em que a loja o guarda;
  `wall` pede o fundo na parede; `rooms` limita os cômodos (o chuveiro no banheiro); `craft` diz o que a loja gasta e quanto leva para fazer sob encomenda.
- `paints[]` `{id, name, kind: floor|wall, color, beauty, price, default?}`; `build` `{contractor, wall, door, demolish, cell, maxExtraCells}` (preços em moeda do mundo).
- Referência quebrada recusa o cenário com o caminho do campo (`Home.items[0].object`); sem o bloco, o mundo não tem modo comprar e o hash não muda.

## Origem realista e moeda conservada
- A **loja de móveis** é um local do cenário (`Workplaces[]` do tipo `Home.build.contractor`) com estoque (um recurso por móvel), caixa e quem a abastece: o marceneiro (profissão 3)
  trabalha pela receita do local (`Recipes["2"]`: madeira vira móvel). Comprar tira 1 do estoque da loja (fica **reservado em nome da casa**, ver abaixo) e leva a moeda da carteira ao caixa; **sem estoque**, o motor encomenda
  se a loja tem o material (sai do estoque dela), cobra o acréscimo e a loja o faz no prazo (`Parcel.ReadyAtMs`); sem material, "esgotado".
- Vender devolve `resalePercent` do preço **do caixa da loja** (sem caixa, recusa) e o móvel volta ao estoque; descartar não devolve nada. Nada nasce por regra.
- A obra (parede, porta, ampliar, pintura) é paga ao mesmo caixa (o marceneiro faz). Falta de dinheiro recusa com o motivo.

## O que o motor guarda: `Building.Edit` (`HomeEdit`)
Nulo = nada mudou (fora do snapshot e do hash). Móveis comprados (índice depois dos do catálogo, só cresce; vendido fica com `Gone`, o índice não troca: luzes e usos seguem),
movidos e vendidos; arestas editadas (`wall|open|door`); células de piso novas; pinturas; encomendas. A planta e os cômodos **de origem** continuam funções puras do prédio
(o planejador de móveis trabalha neles: a obra não reembaralha a mobília do catálogo); `HomeEditor` aplica a obra por cima: células e paredes de fora refeitas, cômodos de novo
como regiões ligadas por aresta aberta, cada um com o tipo do cômodo de origem em que mais tem células (dividir a sala dá duas "sala"). `BuildingLayoutCache`, `BuildingRooms`,
`HomeFurniture` e o chão do passo (`PlayerGround`) leem a planta de agora, então o corpo e os NPCs respeitam a parede nova.

## Validação (`HomeCommands`, verbos do `interact {kind:"home"}`)
`buy:objeto` · `place:seq:andar:x:y:giro` · `move:peça:andar:x:y:giro` · `sell:peça` · `discard:peça` · `wall|open|door|shut:andar:ax:ay:bx:by` · `extend:x:y` · `paint:floor|wall:andar:x:y:tinta`.
Lugar (`place`, o móvel já no depósito): piso livre (nunca porta nem escada), sem outro móvel nem encomenda, fora da passagem das portas, cômodo permitido, fundo na parede se o móvel pede, e **toda célula livre segue
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
`buy_mode.gd` (B): a câmera sobe (`camera_rig.set_build_view`, ortogonal), painel com abas Loja / Na casa / Construir, categorias, cartões com miniatura desenhada por código
(`furniture_thumb.gd`, o objeto do catálogo num SubViewport), preço, efeitos, beleza e estoque; fantasma verde ou vermelho pela resposta do motor (com o motivo), Q/E/R giram, clique
põe, botão direito e Esc cancelam; popup de pausa como os outros. Passos de captura: `buy`, `buyitem`, `buyfind`, `buyat`, `buyclick`, `buycut`, `buyhome`, `partner`.

## #266: pegada, giro e modelos
O fantasma e a validação do motor usam a **pegada inteira** no giro pedido (`FurniturePlacement`): sobrepor qualquer célula de outro móvel, atravessar parede, tapar a porta ou deixar o móvel sem lado livre
é vermelho, com o motivo. A miniatura é o modelo procedural do catálogo (`render/furniture_models.gd`), o mesmo da casa. Ver [house-rooms.md](house-rooms.md).
põe, botão direito e Esc cancelam; popup de pausa como os outros. Em 'Loja' o cartão escolhido mostra peso, volume e como trazer e o botão Comprar; em 'Seus pedidos' cada móvel pago diz onde está e os botões do transporte. Passos de captura: `buy`, `buyitem`, `buyfind`, `buyat`, `buyclick`, `buycut`, `buyhome`, `partner`.

## O móvel vem da loja (#265, AD-232)
Pagar não põe o móvel em casa: ele fica **reservado na loja em nome da casa** (`Parcel`, `Building.Edit.Parcels`: `Where` = `shop`, `hands`, `car`, `courier` ou `depot`). Dado em `Home.haul`
(`HomeHaul`, validado ao carregar com o campo no erro): `heft` (kg e litros de cada móvel), `hands` (`maxKg`, `maxLiters`, `slowPercent`, `helperFactor`), `vehicles` e `fleet` (porta-malas `trunkKg` e
`trunkLiters`, `mps`; `fleet` = o que a casa própria tem estacionado em frente à porta), `delivery` (`fee`, `perKg`, `openHour`/`closeHour`, `prepMin`, `mps`) e `reachM` (a que distância da porta se pega e se deixa).
- **Nas mãos** (`HomeFreight`): `fetch:seq` na loja (ao alcance) leva só o que cabe nas mãos; a carga atrasa o passo na proporção do peso (`LoadFactor` em `SpeedMps`; dividida entre dois com `fetchhelp:seq`,
  que pede outra pessoa da casa adulta também na loja); `drop:seq` em casa leva ao depósito. Grande demais recusa com os caminhos (carro, ajuda, entrega).
- **De carro**: `load:seq` (carro na loja, corpo ao lado, cabe em kg e litros), `drive:shop|home` (o corpo entra no carro e anda a rota do chão à velocidade do veículo; `Edit.Car` guarda onde parou, nulo = de
  partida), `unload:seq` (carro em casa) leva ao depósito. Sem carro no `fleet`, só mãos e entrega.
- **Entrega**: `deliver:seq` cobra a taxa (carteira → caixa da loja); quem entrega é o primeiro empregado vivo da loja que ninguém conduz; sai no próximo horário (`NextDeparture`), anda a rota real
  (`HomeFreight.Path`, a mesma busca do `Approach`) e o evento agendado deixa o móvel no depósito; o mesmo entregador só sai de novo depois de voltar. Aviso na ficha (`home.arrived`).
- **Depósito**: `place` (lugar validado como antes), `stash:peça` (guarda de volta), `sellp:seq` (a loja recompra), `cancel:seq` (desfaz a compra do que está pronto na loja: devolve tudo).
- Contrato aditivo: `haul` em `GET /players/{id}/home` (pedidos com `actions` já respondidas pelo motor, carro, entregadores com rota e tempos, peso de cada móvel) e `home` na ficha do corpo (`carry`, `arrived`, `coming`).
- Cliente: `home_freight_layer.gd` desenha o móvel nas mãos, o carro e o entregador só do que o motor diz e avisa "Seu pedido chegou". Limites: o corpo do entregador no mapa segue a rotina dele; sem IA de compra
  de móvel (o parceiro não decide comprar) e o ajudante não anda sozinho com quem carrega.

## #268: entrega com hora
A entrega diz a hora do jogo calculada pelo motor ("Seu Tomás entrega Sofá por 30: chega amanhã às 08:12", `CareerWork.When`), no preview do botão, no `parcel.dueText` e no preparo da loja (`readyText`), em vez de "em 788 min".
"Ir à loja" é uma linha da roda do E de qualquer lugar (oferta `freight`, verbo `goto:shop`: o mesmo "ir até", desfecho `going`).
