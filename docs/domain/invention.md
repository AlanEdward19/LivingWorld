# Invenção por composição física (ADR-0045, AD-141)

Quem inventa é a pessoa. Leis físicas são código; artefatos nunca. O cenário declara só a natureza
(materiais brutos e a água); o motor mede; o NPC compõe. "Barco" é o nome que alguém dá a um design
que o avaliador mostrou flutuar, carregar e andar. Primeira fatia: atravessar água.

## Peças
- **`MaterialSpec`** (`InventionRules.Materials`): densidade, tração, impermeabilidade, peça natural
  (tronco, feixe, lâmina, bloco) e seus limites, parede mínima, minutos de coleta e de trabalho por kg,
  vida útil, se só nasce à beira d'água, se só serve de revestimento, se se molda sobre armação, e a
  aparência (cor, acabamento, grão, condutividade). Id próprio, sem nome no motor.
- **`Design`**: até 16 `DesignPart` (box, cylinder, sphere, capsule; medidas em mm; parede 0 = maciça;
  material, revestimento, junção à mãe, offset). Id = FNV-1a da estrutura. Proveniência: quem, quando,
  pais e origem (inventou, aprendeu, copiou, engenharia reversa).
- **`InventionLedger`** (canônico, `WorldState.Invention`): designs vivos, lápides, `KnownDesign` por
  pessoa (fidelidade ‰), itens (`CraftedItem`, de um domicílio, até gastar) e mentes (frustração em
  minutos, ocupado até, tentativas, falhas, último protótipo que falhou, material em que desconfia).
  Vazio no tick 0; nenhum cenário tem bloco de designs.

## Avaliador (`PhysicsEvaluator`, só inteiros)
Massa e volume por primitiva (π = 355/113); empuxo: a cota d'água sai por busca binária até o volume
deslocado igualar massa + pessoa (casco oco estanque desloca o volume externo; oco que encharca, só o
material); borda livre mínima; estabilidade: KB + BM − KG ≥ mínimo (BM = I da linha d'água / volume);
resistência: viga com a pessoa no meio (M = P·L/8) contra módulo de seção × tração / fator de segurança
das partes que vencem o vão, pesadas pela junção; velocidade: v³ = 2·P·η / (ρ·(Cd·A + Cf·S)), com η
maior se houver parte chata articulada que empurra a água. Falhas: inválido (a natureza não dá a peça),
afunda, vira, quebra, lento. A receita de construção (material por id e horas) sai do mesmo design.

## Como o NPC chega à ideia (`InventionSystem`, mensal)
1. **Vivência**: ao chegar de um trajeto cuja reta cruza células com água, a pé, o tempo perdido no
   vau vira frustração (`InventionIntake.OnTravelArrived`, no instante, sem varredura). Decai 30%/mês.
2. **Gatilho**: acima do limiar, chance por mês = base × frustração × abertura × tempo livre × habilidade.
3. **Ideia**: copia um item visto na cidade (com perda) ou concebe 8 variantes a partir do protótipo
   que falhou, dos designs que sabe e da peça natural dos materiais que viu boiar (só quem mora perto
   da água vê o junco); operadores genéricos (medida, duplicar ao lado, escavar, revestir, trocar
   material, parte chata, juntar designs, tirar parte, junção, primitiva), puxados pela última falha.
4. **Julgamento**: o avaliador com a crença (densidade e tração com erro que cai com a habilidade);
   só constrói o que acredita funcionar.
5. **Protótipo**: as horas do design espalhadas no tempo livre (3 h/dia com emprego, 8 sem); na
   conclusão, a peça como saiu das mãos (erro de fabricação) passa pelo avaliador real.
6. **Falha ensina**: lembrança (`InventionFailed`: raiva e vergonha), ponto de partida da próxima
   tentativa e, se quebrou ou afundou pelo material, desconfiança dele. Sucesso: `InventionWorked`.

## Difusão, produção e perda
Quem sabe ensina por mês alguém da casa ou amigo próximo (segredo se ambicioso e pouco altruísta), com
perda de fidelidade. Itens gastam (vida do material mais curto × qualidade). Na morte, o saber vai junto;
design sem quem saiba e sem item vira lápide (`DesignTombstone`). Item que sobrou pode ser copiado de novo.

## A peça na economia e na vida (fatia 2, AD-143)
- **Produção** (`DesignWorkshop`): quem sabe faz a sua ou, se a cidade procurou no mês passado mais do
  que há exposto, uma para vender. A receita sai do design (`PhysicsEvaluator.Recipe`, `BuildMinutes`);
  a encomenda anda uma vez por dia dentro da `ProductionSystem`, com as horas livres × força × capacidade.
- **Material de estoque**: abrir a encomenda tira primeiro o material guardado na casa (sobra de
  construção que falhou: `SalvagePm`) e, se o material circula na economia (`MaterialSpec.ResourceId`),
  a despensa e o mercado (pagando); o que já estava guardado não é colhido (`GatherMinutes`).
- **Venda** (`InventionMarket`): a peça fica exposta no mercado de comida da cidade com preço = horas ×
  preço da hora de quem fez (+ material pago). Quem chega ao mercado precisando atravessar água sem ter
  com quê compra a mais barata se ela valer (horas perdidas por mês × preço da sua hora × meses que a
  peça dura) e sobrar para a comida; não precisa saber fazer. Moeda vai a quem fez (ou herdeiro, ou caixa
  do mercado). O preço anda por mês com a regra do mercado de recursos: 1 + sensibilidade × (1 − oferta/procura).
- **Carga por viagem**: com água entre a casa e o mercado, a compra (e a água do poço) corta no que cabe:
  `FootCarryKg` a pé, a carga útil medida da peça da casa (`UnitGrams` dá o peso de cada recurso).
- **Pesca** (`Fishing`, mensal): casa com peça que funciona e água perto pesca no tempo livre de quem tem
  mais; rendimento = g/h × alcance (velocidade/passo, 0,5..2) × firmeza (GM/(3×mínimo), 0,2..1) × peixe
  que resta/capacidade, limitado pela carga útil por saída; estoque de peixe por cidade, logístico. Vira
  comida na despensa até o que a casa come no mês.
- **Cópia por evento**: quem chega remando mostra a peça; quem está perto, de outra casa e com o problema
  da água, guarda o design (`InventionMind.SeenDesignId`) e copia dele na próxima tentativa.
- **Lápide e relatos**: ao virar lápide, conta-se quantos fatos da história (e relatos deles) falam do
  design (`Accounts`); `GET /designs/by-fact/{id}` leva do fato à lápide. Relato vivo na cidade aumenta a
  vontade de tentar (`RumorAttemptBoostPm`). Sem história ligada, nenhum relato.
- **Pessoa 0**: `LifeEventIntake` lê "ninguém" como campo ausente ou ilegível (-1), não como 0: a pessoa 0
  passa a lembrar do que vive. Na lembrança, `Other` = 0 ainda é "ninguém" (backlog 7a).

## O que a pessoa leva consigo (AD-147)
- **Despensa em casa** (`PantryNeedsPresence`, **desligada por padrão**: ligada, morria-se de sede preso no mercado vazio): a mais
  de uma hora de caminho, come e bebe do que leva, da ração da oficina, ou volta para comer. **Carga** (`InventionLedger.Carried`): peso = `FootCarryKg`; mãos nuas `HandsVolumeMl` (uma
  ração, nada de `LiquidResourceIds`); `UnitMl` é o volume de cada recurso. Ao sair, leva o que sede e fome pedem até voltar (ida +
  estada + volta), deixando uma unidade por dia de cada outro morador; longe e crítico, consome antes de decidir; em casa, devolve.
- **Recipiente é design** (`PhysicsEvaluator.Contain`): oco (ml) e a parte estanque, massa, carga que a mesma viga aguenta até o
  oco cheio d'água e o teto da pessoa, e tira (parte chata e fina, junção flexível) que prende ao corpo. Falhas `TooHeavy`, `TooSmall`.
- **Problema de carregar** (`CarryMinds`): compra cortada pelo que cabe e voltar para comer viram frustração com o volume que faltou;
  a ideia parte de um oco desse tamanho em cada material conhecido; o resto (crença, protótipo, ensino, cópia no mercado,
  encomenda, venda com procura por função) é o da travessia. A função fica com o design (`CarryDesigns`). O recipiente da casa amplia
  a carga pessoal e a compra a pé (preso ao corpo soma aos braços). `GET /designs/{id}`: `function` e `holds`.

## Consumidor causal
`Mobility.TravelTicks` e `HouseholdTicks` (vau ou remo), tamanho da compra (carga, recipiente), despensa (pesca), sede e fome
longe de casa (carga pessoal), carteiras, `ProductionSystem` (encomendas), vontade de tentar (relatos).

## Contrato com o cliente
`GET /designs`, `GET /designs/{id}` (lápide traz `accounts` e `lostTick`) e `GET /designs/by-fact/{id}`:
partes e materiais; `object_factory.gd::build_design` monta uma malha por parte.

## Ainda não
Animação, pose de uso, terra e ar, química, robôs, substituir o `TechDiscoverySystem` (backlog `05-invencao-emergente.md`).

## Números
Padrão em `InventionRules` (Domain); `InventionRules.Disabled` = mundo de antes. Valores e marcas "precisa de conferência"
nos registros AD-141, AD-143 e AD-147 de `docs/decisions-log.md`.
