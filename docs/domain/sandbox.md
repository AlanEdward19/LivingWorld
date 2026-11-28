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
(a despensa dura; sem mercado ainda, nenhuma comida é criada do nada).

## O que ainda não funciona
- Rua e calçada sem superfície de partida; a porta da casa pode abrir para um caminho até a rua e o quintal tem as árvores da regra de 16% (nenhum parâmetro de cenário as tira).
- Comida: só a despensa de partida (mercado ou entrega como prédio com estoque e preço fica em issue). Sono e comer à mesa seguem as ações do motor (dormir: `action`).
