# Sandbox do casal (#252, S1 da fatia jogável #246)

Um casal numa casa de subúrbio, para validar a gameplay inteira num mundo pequeno: controlar qualquer um dos dois, usar os móveis,
trocar de pessoa. O mundo grande ficou de lado (parece morto: dezenas de pessoas coladas no mesmo bloco). **Nada disto está no código do
motor**: é o período `scenarios/periods/sandbox-casal.json`. Decisão: AD-218.

## O período (dados)
- `Presentation.QuickStart: true`: o cartão (`GET /periods` → `card.quickStart`) vira o botão **Sandbox: casal** no título, que cria e
  abre o mundo direto (semente nova a cada criação); o assistente de "Novo mundo" não o lista. O arquivo é embutido na API
  (`DefaultPeriodSeeder.DataPeriodIds`) e uma versão nova entra no repositório de períodos quando o arquivo muda.
- `AuthoredHouseholds`: Marina (32) e Rafael (35), casados, uma casa. `InitialPopulation` 0: ninguém mais. A cidade (`Cities`) é a
  "Jardim das Acácias", autorada, porque sem população sorteada nenhuma cidade seria criada.
- `Cells`: o mapa 24×24 é **autorado e plano** (terreno 1, altitude 0, sem água); o gerador espalhava 10% de água e a casa não achava lote.
- `Rooms.shapes`: `{"-1": {"width": 7, "depth": 6, "floors": 1}}`, o tamanho da casa em células de 1,5 m (10,5 × 9 m). Vale por tipo de
  prédio, é gravado em `Building.Shape` ao semear a casa e a planta, os cômodos e a mobília saem dele (limites: 3 a 16 por lado, 1 a 3 andares).
  Sem `shapes` a casa é a de sempre (3×3).
- `Rooms.kinds` / `plans`: sala (porta de entrada), cozinha, quarto e banheiro, com peso e mínimo de células; ver [house-rooms.md](house-rooms.md).
- `AbsorptionRingCells: 10` e `CityLayout`: a folga em volta do limite da cidade onde o lote pode cair. Uma cidade de 2 moradores tem limite 3×3 e
  só com a folga declarada a casa 7×6 acha lote de frente para a rua (sem ela caía no anel legado, a 9 células da rua).
- `Objects` (cada um com `name`, o nome que o jogador lê) e `Furnishings["-1"]`: cama de casal, geladeira, fogão, mesa, 2 cadeiras e sofá,
  com os usos que o motor já entende (Sleep, Eat, Socialize). Pia, chuveiro, vaso e TV estão no catálogo **sem uso e fora da mobília**.
- Comida: a despensa de partida (`SeedInitialEconomyBuffer`: 50 por pessoa de comida e de água). Nada de moeda nem comida criada por regra.

## Trocar de corpo (motor)
- `PlayerCommands.Switch`, `POST /players/{playerId}/switch {npcId}` e `{"command":"switch","npcId":N}` no canal. O jogador solta o NPC que conduz
  **sem fazê-lo sumir** (`Logout` faz: largaria casa e emprego e abriria busca) e conduz o outro no mesmo modo de posse (plena ou comum).
- Uma peça só: alvo morto, já conduzido por outro jogador ou vontade que impede (posse comum) recusam e o jogador fica no corpo de antes.
  Entra no registro de sessões (`PlayerCommandKind.Switch`) e o replay o reproduz.

## Cliente
- **G** (ou o botão do topo, a linha "G — passar a ser Rafael" no cartão e "Trocar para …" na pausa): a próxima pessoa da casa do corpo, lida
  do motor (`/entities/household`). A figura de antes segue no mundo como gente do motor; a câmera desliza do corpo velho ao novo (0,8 s).
- Passos de captura: `sandbox` (o botão do título), `switch`, `use:Geladeira` (a oferta do motor), `where`; ver `dev_steps.gd`.

## Tempo real e passo (#256, AD-221)
- **Início rápido entra no corpo**: ao terminar a criação (o job assíncrono), o cliente possui a pessoa de menor id da casa (`App.enter_first_of_family`),
  no modo de posse do jogador (plena com o token de admin, comum sem ele), com a câmera nela e o guia da primeira posse; **G** troca.
- **Todo caminho de posse conta como jogador presente** (`PlayersPresent` = corpo vivo com controlador): comum, plena, troca G e reconexão; com jogador o relógio é
  1:1 (Δ 50 ms, 20 ticks/s). O Δ manual e o acelerado do admin são do mundo que estava aberto: criar ou abrir outro mundo os zera (`ResetRhythmOverrides`).
- **Relógio da tela fiel**: `GET /simulation/status` devolve o ritmo de agora (não o do último ciclo); o cliente o relê ao possuir, trocar e criar personagem.
- **Passada**: `BodyPose.gait_params` (cadência e amplitude pela velocidade real em m/s e pela escala do corpo) e `drop_for_reach` (o quadril desce o que o alcance
  pede): ~2,1 passos/s a 1,1 m/s, pé de apoio parado no chão. Passos de captura: `hudclock:N` (relógio da tela) e `gait:W=S` (m/s, passos/s, deslize).

## S2 (#255): necessidades e objetos como dado
Higiene, diversão e bexiga, usos com duração, o corpo que anda até o móvel, fila e o parceiro que se cuida sozinho: ver [needs-and-objects.md](needs-and-objects.md) (AD-220).
No período: `CatalogExtends: ["home-life"]` (móveis com usos e as três necessidades), `Furnishings["-1"]` com chuveiro, vaso, pia e TV, `InitialPantryPerMember: 400`
(a despensa dura; a reposição vem do mercado do bairro, ver #258 abaixo).

## UX do sandbox (#260, AD-224)
- **Velocidade do tempo** (HUD, canto de cima, e teclas **0** pausa, **1**, **2**, **3**): só sozinho no mundo (`canSkip` do `GET /players/{id}/time`), validada pelo motor
  (`POST /players/{id}/speed {speed}` ou `{"command":"speed","speed":N}`; máximo em `Players.Time.MaxPlayerSpeedValue`, padrão 3). `SimulationHost.PlayerSpeed` multiplica o Δ fino
  (1× = 50 ms, 2× = 100, 3× = 150); a pausa não roda tick e aceita comando; com dois jogadores volta a 1× e os botões somem. Entra no registro (`Speed`) e o Δ vai pelo registro de Δ.
- **Casal**: o segundo morador começa numa célula livre de outro cômodo (`HouseholdStart.Spread`); **luz**: abajur e luminária são objetos do catálogo com `light`, acesos à noite com gente em casa.
- **Câmera**: **V** alterna paredes cortadas (as do lado da câmera baixam), inteiras e baixas; ao entrar o olhar vai para o espaço livre. Passos de captura: `speedinfo`, `lamps`, `walls`.
## 3º passo de UX (#261, AD-227)
- **Velocidades em dado**: `Players.Time.Speeds: [1, 3, 10, 60, 300]` (`TimeRules.SpeedList`; sem a lista vale o `MaxPlayerSpeedValue` de antes, 1..N). 1× é tempo real e o padrão; **0** pausa e
  **1–5** (teclas e botões do HUD) pedem a velocidade pelo valor (`POST .../speed {speed:300}`, validada pela lista). `GET /players/{id}/time` ganha `speeds` e `walkMaxSpeed`. Só sozinho (`CanSkip`).
- **Passo em velocidade alta**: o Δ do tick nunca passa de `MaxStepMsValue` (2000 ms): acima disso o motor roda **mais ticks por segundo** (`TimeRules.SpeedPlan`: 300× = Δ 2 s × 150 ticks/s),
  então andar, usos, interações, sono e eventos agendados seguem corretos e o relógio fecha exato. As necessidades continuam do relógio (nada cai mais rápido além do próprio tempo). Com Δ acima de
  `WalkMaxStepMsValue` (500 ms, até 10×) o corpo **não anda por WASD** (o comando chega a cada ~0,1 s reais = minutos de mundo): só por ordem do motor (usar móvel, interação, "ir até"); o aviso diz
  como voltar (tecla 1). Registro e replay: `Speed` e o Δ pelo registro de Δ, o mesmo hash.
- **Menu social de longe e conversa sentado**: ver [social-interactions.md](social-interactions.md) (`approachM`, `compatibleWithUse`, o par que fica junto). **Duplicata**: com o menu social, o "Conversar" de texto
  sai da roda; o "Bater papo" aceito oferece abrir o texto (T, 20 s). **Câmera após G**: a troca liga o enquadramento de cena (a prumo, parede cortada) até o corpo novo assentar.
- **Rotina com variação**: `NeedStartJitter` (nível de partida de fome/sede/sono/social por pessoa) e, por necessidade, `variation {jitter, traits}` e `seekBelow` ([needs-and-objects.md](needs-and-objects.md)).
  O rótulo "N min" do uso é o que ele leva agora (`untilFull`), não o teto do catálogo; o relógio da tela é relido ao acabar o uso.
## S3 (#259): o casal vive junto
Interações sociais em dados (conversar, elogiar, abraçar, beijar, piada, discussão, desculpa, convites para ver TV, comer e deitar juntos), relação visível e iniciativa dos NPCs: ver [social-interactions.md](social-interactions.md) (AD-223). No período: `CatalogExtends: ["home-life", "social"]`.

## Sobras (#258, AD-226): mercado, refeição, rua, luz
Tudo é dado do período e do catálogo (`home-life.json`); o código só tem os mecanismos genéricos.
- **Mercado do bairro** (comida que se repõe sem nascer): um `Workplaces[]` do tipo 1 (nome "Mercado", `Descriptors.LocationType/BuildingType`) com estoque, preço e caixa, de porta
  para a rua (`Orientation: 180`). Quem o abastece é o trabalho de verdade: **Seu Joaquim** (profissão 2, `Members[].Workplace: 0` = já empregado), pela receita do local
  (`Recipes["1"]`) com `CropYear: {Enabled:false}` (sem safra anual, o lote diário). Comprar = a banca de sempre (`stall`/`buy`): moeda e comida só mudam de mão (carteira → caixa,
  estoque → despensa da casa, que é a geladeira); sem moeda, recusa. A renda do casal veio em #263 (emprego e contas, [careers-and-bills.md](careers-and-bills.md)): o período agora tem também o escritório e a escola como empregadores (`Workplaces[]` 1 e 2).
- **Refeição**: recurso 3 ("Refeição"). O fogão (`cook`, 10 min, `consumes` 2 comida, `produces` 2 refeição) só produz no fim natural (interromper perde o preparo); a mesa (`meal`) gasta
  1 refeição e 1 água e sacia mais (300/h, +diversão); a comida crua do catálogo sacia 60 (`food` `hunger:60`), a refeição 100. Quem ninguém conduz cozinha antes da refeição sem
  urgência e `FoodResolver.ResolveMeal` prefere a refeição pronta.
- **Rua, calçada, quintal** (`CityLayout`): `Streets.Pavement {Street, Sidewalk, SidewalkWidth}` pavimenta o traçado e a calçada (`StreetPavement`, chão de verdade: condição, rota e
  desenho do cliente pela camada `Roads`); `Streets.ClearAroundBuildingsCells` tira as árvores a N células de prédio, rua e calçada (`GET /world/trees` traz `clear`; o cliente esconde
  as da regra de 16% ali); `SidewalkCells` pede calçada entre a porta e a rua. Cenário sem os campos: chão e árvores de antes.
- **Cuidado fora de casa**: o parceiro que ninguém conduz, longe de casa com bexiga ou higiene urgentes, volta (`CareCallsHome`) e usa o objeto lá. Sem instalação pública no catálogo.
- **Dormir**: deitar vale sempre; o sono acelerado só acelera havendo sono a recuperar e acorda `descansado`; descansado, deita em ritmo normal.
- **Abajur**: usos `light:on`/`light:off` (`toggle`), estado em `world.Lights` (`LightLedger`, no snapshot e no hash só se alguma está ligada); o delta do interior traz `lights`;
  o NPC liga a do cômodo ao escurecer e apaga de dia, ao dormir e ao sair (`HomeTending`). **Higiene** pesa no laço também entre NPCs (`HomeLife.WeighBond`).

## O que ainda não funciona
- O emprego e as contas são do catálogo `careers` (#263, [careers-and-bills.md](careers-and-bills.md)): Rafael começa no escritório, Marina procura no jornal da sala; fome e sede de sempre ficam fora do bloco de necessidades em dados (mover muda o hash do mundo grande).
- Sem instalação pública (banheiro de rua); o mercado só vende comida e água; sem entrega por telefone.
