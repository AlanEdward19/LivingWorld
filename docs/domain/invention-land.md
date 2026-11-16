# Levar por terra: o veículo puxado que alguém inventa (AD-153)

Parte de [invenção por composição física](invention.md) (ADR-0045). Nenhum veículo escrito: trenó, padiola, carrinho ou
carroça é o nome que alguém dá a um design que a sonda de terra mostrou puxar mais carga que os braços levam. Não há
animal de tiro no mundo: quem puxa é a pessoa que foi ao mercado.

## A sonda de terra (`PhysicsEvaluator.Haul`, só inteiros)
- **Quem toca o chão**: as partes de base mais baixa. Para cada sentido (X, Y) cada uma resiste de um jeito e vale o sentido
  que resiste menos (média das partes de contato, peso dividido por igual):
  - **rola**: peça redonda no plano do movimento (cilindro e cápsula têm o eixo em X e rolam em Y; esfera rola em qualquer
    um) **presa à mãe por junção de eixo**. Resistência = braço de rolamento ÷ raio + atrito do eixo × raio do pino ÷ raio.
    Redonda sem eixo, ou caixa com eixo, não rola: arrasta;
  - **desliza**: peça esguia no sentido (comprimento ≥ `RunnerSlenderness` × largura): atrito do patim, 0,30 áspero, menor
    quanto mais liso o acabamento do material ou do revestimento (o mesmo `FinishPm` que dá a aparência), com piso de 0,22 em
    terra (AD-162: madeira sobre madeira 0,25–0,5; os 0,10 da madeira encerada só valem em neve, que o mundo não tem);
  - **arrasta**: o resto, atrito de arrasto;
  - **tomba**: se as partes de contato cobrem, de lado, menos da metade da largura da coisa, ela deita e arrasta (um patim
    fino ou uma roda só sob um tabuleiro largo não seguram; o carrinho de mão equilibrado pela pessoa fica no backlog 8e).
- **Carga** = a menor entre: o que a força humana sustentada puxa (F = μ·(massa + carga)·g ≤ `HumanPullN`); o lugar (oco, ou o
  maior tabuleiro maciço × `PileHeightMm`, × `CargoBulkKgM3`); a armação (a viga do avaliador com a carga no meio, sem as rodas);
  o **eixo** (pino em balanço até o meio da roda, do material e da espessura da mãe, no máximo metade do diâmetro da roda) e a
  **roda** (o disco atravessado) com a sua parte do peso. Busca binária na carga.
- **Falhas**: inválido; `TooHeavy` (vazia já pesa mais do que se puxa); `TooSmall` (sem lugar para uma unidade); `Breaks`
  (armação, pino ou disco não aguentam uma unidade). Saída: modo (arrasta, desliza, rola), massa, carga, resistência ‰,
  força vazia (N) e as horas de fazer (as mesmas das outras sondas).

## Quem inventa e por quê (o motor de sempre, sem caminho paralelo)
1. **Problema**: a compra (e a água do poço) corta no que uma pessoa leva **também por terra** (`LandCargo`), como já cortava
   com água no caminho (AD-143): `FootCarryKg` nos braços, o recipiente da casa (AD-147) ou o veículo. O que fica no mercado vira
   frustração de carregar (`PersonalCarry.FeltCargoCut`): uma ida e volta inteira por carga que ficou (`WholeTripsForCut`; antes,
   a fração cortada da viagem), e o volume que faltou.
2. **Ideia** (`CarryInvention`, mensal): além do oco do tamanho do que faltou, um **tabuleiro** de cada material conhecido com a
   área para a carga (`HaulInvention.NeedDeck`), e a busca ganha o operador genérico "pôr por baixo" (`DesignSearch.Support`):
   uma peça redonda (cilindro deitado ou esfera) ou esguia, sozinha ou em par simétrico, presa por eixo, fixa, amarrada ou em
   corrediça. Puxado pela falha: pesado demais ou pequeno pede apoio, medida maior, eixo, outra forma.
3. **Juízo** (`HaulInvention.Judge`): cada variante passa pela sonda de conter **e** pela de terra, com a crença da pessoa; vale
   a que resolve mais. Por terra só conta o que passa do que os braços já levam, até a necessidade; o peso próprio pesa pouco
   (vai no chão). A casa que já tem veículo e segue cortada tenta superá-lo (`ImproveCarrier`).
4. **Protótipo, falha, ensino, cópia vendo, encomenda, venda, lápide**: os do recipiente (AD-147). A função fica com o design
   (`InventionLedger.HaulDesigns`, nulo até o primeiro): 2 = levar por terra. Quem chega ao mercado com veículo é visto (o
   veículo chama mais atenção que o recipiente); quem segue cortado sem um olha os expostos.

## Consumidor causal
- **Compra e poço** (`InventionMarket.CargoGrams`): a carga por viagem passa a ser a do melhor veículo da casa (com a qualidade
  da peça) quando ela supera os braços e o recipiente. Com água no caminho o veículo vadeia como quem anda (o mapa não tem
  profundidade); a embarcação segue valendo se levar mais. `NoteTrip` mede as idas em que o veículo levou além do corpo.
- **Frustração**: o corte por terra alimenta o mesmo problema de carregar, então recipientes também nascem mais.

## Aparência
`GET /designs` e `GET /designs/{id}` trazem partes, materiais e formas como sempre; `function` ganha o valor `"haul"`. Rodas são
cilindros deitados ao longo de X presos por `axle`: o cliente já desenha a malha; o sentido do giro é derivável da forma e da
junção (animação: backlog 05 item 11). Nenhum campo novo exposto (o que a sonda mede fica para aprovação, backlog 05 item 8g).

## Ainda não (backlog 05, itens 8a–8j)
Tração animal; terreno e caminho na resistência; velocidade com carga; colheita, obra, frete entre oficinas e
`Npc.CarriedResourceId`; estabilidade (carrinho de mão, rampa); dividir a carga com quem vai junto; campos da sonda na API;
ar; profundidade do vau; desgaste pelo uso.

## Números
Padrão em `InventionRules.Land.cs` (Domain); `LandCargo = false` e `LandVehicles = false` voltam ao mundo de antes. Valores e
marcas "precisa de conferência" no registro AD-153 de `docs/decisions-log.md` e no backlog 08.
