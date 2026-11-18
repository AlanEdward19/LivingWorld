# Cliente Godot: aparência pelo estado e a cidade que o motor diz

> Parte do [README](../README.md). Desempenho e medidas: [desempenho](desempenho.md).

## Aparência pelo estado (ADR-0047 etapa 3, AD-171)

Formas geradas por código (sem pacote de assets), a partir da projeção do observável (AD-170):
- **Pessoas (todas, não só a da ficha)**: escala pela altura real (bebê, criança, adulto, idoso), cabelo pelo sexo, roupa e chapéu do ofício (catálogo `assets.json` ou o nome do ofício), barriga de gravidez, palidez e esteira de quem está de cama, roupa de luto, postura pela emoção dominante, saco ou jarro da carga nas costas. **Perto** (zoom ≤ 7,5): ferramenta do ofício na mão no trabalho, recipiente levado (forma e cor de `/designs/{id}`) e rosto com expressão. Quem conversa com alvo vira para o outro e uma linha discreta liga os dois.
- **Prédios**: enfeite e placa pela função (`buildingStates`): chaminé, hospedaria com lanterna, fazenda com cerca, ferraria, padaria, mercado com toldo, taverna, pedreira, templo; fechado com tábuas na porta.
- **Lavoura**: canteiro por estágio (sem semear, plantado, crescendo, maduro, colhido, pousio), cor e altura pela cultura, amarelado com sede.
- **Estação e clima**: paleta do chão e das copas (primavera, verão/seca, outono, inverno), estação e temperatura da hora no relógio do topo; chuva, neve e chão pelo tempo do motor (AD-188, abaixo).
- **Fauna**: animais por espécie na cidade; bandos com o número no mapa-múndi. **Espólio** (saco e trouxa) e **objetos inventados** fora de casa pela forma e material do design.
- **Mapa-múndi vivo**: um ponto por morador na rua, na cor da ação; a placa diz quantos estão na rua, no pool, o que fazem e a temperatura.
- **LOD em três níveis**: mundo (densidade, bandos, eventos) · cidade (corpos, prédios, lavoura, animais, objetos) · perto (ferramenta, objeto em uso, rosto). Lavoura, fauna, itens, densidade e linhas de conversa são MultiMesh por tipo; peças do corpo têm malha e material compartilhados e não projetam sombra.

Delta enxuto (AD-172): num NPC já conhecido, `city: null` = mesma cidade, `appearance` ausente = a anterior, `actionStartedAtTick` ausente = início, alvo e intenção de antes (presente = grupo novo), `observable` ausente = não mudou; `rest`/`food` não reenviados avançam pelo tick do delta (`Store/observable_parse.gd` `advance_processes`). Nomes vêm do escopo (`observable.name`): a paginação periódica de `GET /npcs` saiu; prédio novo chega com a planta no delta (sem reler o retrato da cidade). **Leitura**: prédio que fica na frente do que está sob o mouse, da pessoa selecionada ou do que se segue fica translúcido enquanto cobrir (e o mouse atravessa o translúcido); quem divide uma célula se espalha nela (lugar estável por id) e quem se cruza passa pela direita — o clique pega o corpo onde ele está desenhado. (O quintal em volta de todo prédio e a "cidade inteira = rua" da AD-171 saíram na AD-177: ver abaixo.) Delta: chave ausente e `null` valem o mesmo. Resposta fora do contrato vira uma faixa no topo com o primeiro campo divergente (antes ficava em "Conectando…").

## Cidade que o motor diz (AD-177)

O cliente não inventa chão: fora de rua e de lote, a cidade é o terreno do mapa. **Rua** = cada célula da camada `Roads`
(`Store/city_ground.gd`), desenhada por `Scenes/render/ground_renderer.gd` pela superfície: trilha estreita e irregular de
capim pisado, terra batida, cascalho (pedrinhas), pedra (lajes); largo ocupa a célula e é mais claro; rua principal mais
larga com marco no meio; mais gente (`users`) alarga. **Lote** (`lot` do prédio): quintal só onde vem `croft`, com chão
próprio e cerca de varas (porteira para o caminho); `frontPath` vira caminho da porta até a rua; `lot` nulo, listas vazias e
`street` nulo = nada desenhado; casa colada fica colada (a ficha diz "parede-meia"). Duas MultiMesh (ruas, lotes), uma caixa
e um material compartilhados; redesenho só quando muda (próximo quadro). Servidor sem rua no escopo (antes da AD-176):
`Scenes/road_sync.gd` relê a camada pela rota sem estado a cada 30 s e logo depois de um lote apontar uma rua desconhecida,
sem recarregar a cena (com delta de rua, ver AD-181 abaixo, não roda). **Ponto único** para a AD-176 (tipo de acesso, obra de rua) e superfície nova:
`Scenes/render/street_style.gd` (`ACCESS_STYLES`, `WORK_STYLE`, `access_of`, `works_of`); superfície desconhecida cai em
`NEUTRAL`, campo desconhecido fica em `road.extra`. Ficha da rua (`street`, id "x,y"): superfície, quantas pessoas passam,
principal/largo, prédios cuja porta dá ali; ficha do prédio: quintal (tamanho), rua da frente e caminho, parede-meia.

## Rua de verdade e gente andando por ela (AD-181)

**Rua por delta**: o escopo (Cidade e Mundo) manda `livingState.roads` no retrato e `roadUpserts`/`roadRemoved` a cada tick
(`Store/road_book.gd`, aplicado por `Store/scope_delta.gd`); a releitura de 30 s (`road_sync.gd`) só roda com servidor antigo
(sem o campo). Rua que nasce, alarga, muda de superfície ou some faz isso em 0,8 s (`ground_renderer.gd`: só nesses quadros a
malha de ruas é refeita). **Chão**: largura pela faixa de uso (`useBand`), cor e capim na beira pelo pisoteio (`wear`), lajes
faltando e terra à mostra com `condition` < 600; `planned` sem uso não é rua (traço na planta). **Obra** (`work`): trecho já
assentado cresce com o %, pilha de pedra/cascalho baixa, estacas; ficha diz o que vira, %, conserto ou obra nova e quem paga
(e quem trabalha nela, `work.workerIds`); `StreetWorkStarted`/`StreetWorkDone` viram marcador na obra e
entram na linha do tempo. **Acesso** (`lot.access`): viela (corredor escuro), escada encostada, passarela por cima; a ficha diz
como se chega e `none` é alerta. Peças em `Scenes/render/street_pieces.gd`. **Rota**: quem viaja anda célula a célula pela
`npc.route` (`Scenes/route_walk.gd`), chegando no `routeArrivalTick` pelo relógio do mundo (`Scenes/route_clock.gd`: tick do
delta + velocidade do jogo, nunca mais de 1 tick à frente; pausa congela; 200× só chega antes); anda pela direita (desvio só
visual), sai do espalhamento da célula e entra no da chegada; o clique e o F pegam o boneco onde está. Selecionado ou seguido:
a rota restante aparece como linha no chão (`Scenes/render/route_line.gd`). Dev: passos `roads`, `walker`, `heat`.

## O que o motor já mandava e agora aparece (AD-188)

- **Tempo do dia** (`climate.weather` do escopo; no Mundo, a região de centro mais perto da câmera pela camada `Climate`):
  chuva, neve e granizo caindo (`Scenes/render/weather_renderer.gd`: duas GPUParticles3D com teto fixo, quantidade pela
  `intensity`, inclinadas pelo `windMs`; câmera longe metade, mapa-múndi afastado nada); sol pelo `sunriseHour`/`sunsetHour`
  (sem tempo, 5 h–19 h); céu encoberto com chuva/tempestade; luz mais quente/fria nas ondas de calor/frio; indicador no topo
  (estado, sensação, vento, chão, seca, ondas, geada; dica com mínima, máxima, chuva do dia, sol e dias perdidos no campo).
  Neve no chão só com `ground: snow`/`snowCm` (o "abaixo de 0 °C neva" do cliente saiu); lama, molhado, congelado e geada no
  terreno, quintal e caminho; **cada rua pelo `ground`/`snowCm` da própria célula** (lama e lamaçal com poças; cascalho e pedra
  só molham porque o motor diz). Ponto único: `Scenes/render/weather_look.gd`. Folha caindo: o motor não diz, não é desenhada.
- **Rota** (AD-185): saída pela `routeDepartedTick`; quem entra no escopo no meio da viagem já aparece no ponto da rota;
  `routeOver` sobe o boneco ao telhado do prédio da célula (o trecho entre o chão e a célula alta é a escada).
- **Acesso do lote**: `accessCells` (laje na altura do vizinho `accessViaBuildingId`, escada subindo até ela, corredor da viela
  ou caminho) e `accessState` (chão pisado, caminho claro, ou só traço e estacas da reserva); ficha com o vizinho como link.
- **Motivos e pessoas**: `involved.motive` no aviso, na linha do tempo e na ficha do evento (nunca em `recordOnly`); fundador
  na ficha da cidade (`state.founderId/founderName`, link); `motives` do `/npcs/{id}/brain` (xará, procurar companhia com
  quem ou "vai ao largo", criança acolhida); tracejado da pessoa selecionada até o alvo (`intentTarget` "tipo:id" ou `actionTarget`).
- **Prédio**: parede, telhado e inclinação pela função (`buildingStates`); à noite acendem 2 janelas por morador (casa) ou
  trabalhador (oficina), vazio fica escuro. Obra de rua: ficha com o nº e os links de quem trabalha (`work.workerIds`).
- **Animais** deslizam da célula de antes à nova em 0,9 s, virados para onde andam; salto de mais de 3 células vai direto.

