# Cliente Godot — Living World

Cliente gráfico só de leitura (ADR-0022). O motor (`src/LivingWorld.Api`) é dono do estado, do tempo e do
determinismo; este projeto só desenha o que a API projeta. Godot 4.7 (qualquer build; o .NET não é usado), tudo em GDScript.

## Rodar

**Jogar**: dois cliques em `Jogar.command` (raiz do projeto, macOS) ou `bash scripts/play.sh`. Ele confere .NET 10 e Godot 4
(diz o que instalar se faltar), compila a API só quando o código mudou, liga a API numa porta livre (5214 ou a próxima),
espera ficar pronta e abre o jogo no menu; ao fechar o jogo, desliga só a API que ele ligou (ou herdou de um lançador que
já fechou; outro jogo aberto continua usando). Já ligada, reaproveita. Mundos e banco em
`~/Library/Application Support/LivingWorld/` (`mundo.db`, `worlds/`, `api.log`, `api.state` com porta e token, modo 600):
"Continuar" volta ao último mundo depois de reiniciar. `--api-only` só liga a API. O token de admin é da sessão: pausa,
velocidade e passo funcionam sem o jogador saber dele.

Aberto sozinho (sem lançador), o cliente usa a API do `api.state` se estiver ligada; senão mostra "O mundo ainda não está
ligado" e, se a API já foi compilada, liga ele mesmo `dotnet src/LivingWorld.Api/bin/Release/net10.0/LivingWorld.Api.dll`
(caminho fixo; nunca compila) e a desliga ao sair (`Contract/local_server.gd`). Manualmente:

```bash
ADMIN_TOKEN=dev ConnectionStrings__World="Data Source=/tmp/lw.db" TICK_LOOP_ENABLED=true dotnet run --project src/LivingWorld.Api -c Release --urls http://localhost:5214
LW_ADMIN_TOKEN=dev /Applications/Godot_mono.app/Contents/MacOS/Godot --path client/godot   # LW_API_URL troca o endereço
```

**Menu (AD-174, `Scenes/menu/`)**: título sobre uma ilha 3D girando · Continuar (só com mundo; nome, ano, pessoas) · Novo
mundo em passos (começo → nome e semente → tamanho e pessoas → mapa de `/worlds/preview`, que também vira o fundo 3D → resumo),
com Voltar/Avançar, barra e "Surpreenda-me"; "Personalizar" (mapa, vilas, prédios, famílias, retoques) é desvio opcional ·
carregamento por etapas reais · Meus mundos (jogar, apagar com confirmação pelo nome) · Opções (tamanho da interface,
volume, tela cheia, vsync, idioma; `user://settings.cfg`). Mouse, teclado (setas, Enter, Esc) e controle. No jogo: dicas da
primeira vez (6, puláveis, lembradas), **?**/F1 ajuda, **Esc** menu de pausa (pausa o tempo se o jogador pode).

**O que vem do servidor (AD-175)**: o cartão de cada lugar (`GET /periods` → `card`: nome, época, descrição, pessoas, vilas,
ofícios, tecnologia de partida; os três de hoje são a mesma época em três tamanhos, e o passo diz isso); a lista de mundos
(`GET /worlds`: período, ano, aberto agora, quando foi jogado, origem `player`/`automatic`), que Continuar e Meus mundos usam
sem adivinhar; Renomear (`POST /worlds/{id}/rename`, o erro do servidor aparece no diálogo); as etapas da criação
(`POST /worlds/create/async` e `GET /worlds/create/jobs/{id}`, mostradas na tela de carregamento); "Salvar agora" na pausa
(`POST /world/save`, com "salvo pela última vez há…"); e o mundo criado ou aberto já rodando (`?startRunning=true`, só com o
token da sessão; o cliente não despausa mais por conta própria). Sons de interface (clique, foco, confirmar, voltar, erro)
gerados por código em `Scenes/menu/ui_sounds.gd`, no barramento Master (volume das Opções).

## Controles

| Ação | Tecla / mouse |
| --- | --- |
| Mover / girar / zoom | botão direito arrasta · Shift+arrasta · roda · WASD/setas · Q/E |
| Entrar na cidade | zoom até a escala de cidade, ou duplo clique na cidade · sair: afastar ou ◀ Mapa do mundo (antes: Esc, que agora abre a pausa) |
| Prédio | clique abre a ficha (tipo, andares, preço, quem está dentro) · duplo clique ou botão Entrar entra (andares: PgUp/PgDn) · Esc sai |
| Pessoas | L abre a lista (segue a cidade em foco) · clique num NPC, também dentro do interior |
| Ficha (inspetor) | um painel à direita para pessoa, prédio, cidade, processo, lavoura, animal, bando, espólio, objeto inventado, mobília, trabalho, casa e evento; lê `GET /entities/{kind}/{id}` (nome real, vínculos, história recente); atualiza sozinho; links (mãe, pai, cônjuge, todos os filhos, casa, trabalho e o prédio dele, dono, moradores, trabalhadores, envolvidos) · Ir até lá · ◀ Voltar ou Backspace |
| Direção | **N** pula para o próximo evento (câmera no lugar, ficha aberta, segue quem está envolvido) · **V** modo assistir (acompanha sozinho o evento mais novo ainda não visto; sem evento, alguém na rua fazendo algo) · linha do tempo "Acontecimentos" (embaixo, clicável) |
| Seguir | botão Seguir na ficha (pessoa, casa ou cidade) ou F; continua ao trocar de escopo, entrar num prédio e sair da cidade |
| Janelas | cérebro, narrativa, economia, tecnologias e console convivem: encaixam lado a lado (ou em cascata) · arraste pelo título para soltar · Esc fecha a da frente |
| Cérebro do NPC | B (ou botão Cérebro na ficha) abre/fecha o painel das decisões do NPC selecionado: linha do tempo + fluxo Estímulo → Ponderação → Decisão · Vigiar retém o rastro inteiro · **E se…?** simula a decisão com outros estímulos (nada é gravado) · Esc fecha |
| Conversar | T (ou botão Conversar na ficha) pede a conversa; o NPC pode recusar (ocupado, hostil, indisponível) · Enter envia · Esc encerra |
| Narrativas | botão Biografia na ficha do NPC · botão Crônica na ficha da cidade (mês/ano/década) · R lista os relatos que circulam, com a confiança de cada um · botão O que a cidade acredita (crenças distorcidas e a distância do fato) · Esc fecha |
| Jogar | I (ou Encarnar) toma o NPC selecionado em **3ª pessoa** (C anima 1ª/3ª, Q troca o ombro, + − zoom; o mouse olha só de mexer). **WASD** anda pelo controle local que prevê com a regra de chão do motor (máscara `GET /players/{id}/ground`; só a célula cheia de gente barra) e converge reaplicando o que o motor ainda não viu (#248), **Shift** corre, **Espaço** pula se o corpo aguenta. **E** faz a ação principal sob a mira (anel no alvo); **segurar E** abre a roda (1–9). **Clique** bate (alcance e recarga do motor), **botão direito/F** bloqueia, **Ctrl/Alt** esquiva. **I/Tab** mochila, **J** metas e diário, **B** modo comprar e construir em casa (#264: câmera de cima, catálogo do motor, fantasma verde/vermelho, Q/E giram; pausa o tempo), **O** carreira (#263: emprego, desempenho, contas e vagas, [careers-and-bills.md](../../docs/domain/careers-and-bills.md)), **Esc** menu (Sair do personagem). Dentro de um prédio a casa entra em foco no lugar do prédio, **no mesmo mundo** (#249): a cidade segue desenhada e viva em volta (só o prédio em foco sai), a figura é a mesma da rua, a casa tem assinatura própria (assinada ao chegar na porta) e a parede entre a câmera e o corpo fica translúcida. Recusa vira aviso discreto com o motivo do motor (#228). Andando para fora da cidade (ou de volta) o escopo troca na borda e a câmera segue um ponto de apoio na posição do corpo até o boneco chegar (#237); a dica de fonte/mata some assim que o corpo passa do alcance que o motor diz |
| Sandbox do casal (#252) | botão **Sandbox: casal** no título (período de início rápido que o servidor declara) cria e abre o mundo; **G** (ou "Trocar para …" na pausa) passa a conduzir a outra pessoa da casa: o motor solta a de antes sem ela sumir e a câmera desliza até a nova; ver [docs/domain/sandbox.md](../../docs/domain/sandbox.md) |
| Tempo e paredes (#260) | **0** pausa, **1**, **2**, **3** (ou os botões do canto): velocidade do tempo, só sozinho no mundo, o motor valida · **V** alterna paredes cortadas / inteiras / baixas · luz dos abajures à noite, barras com número, ícone e seta de tendência, aviso "Terminou: …" ao fim de um uso; ver [docs/domain/sandbox.md](../../docs/domain/sandbox.md) |
| Entorno e luz (#258) | a rua pavimentada e a calçada vêm da camada `Roads` (superfície do motor); o quintal limpo, de `clear` em `GET /world/trees` (sem árvore onde o motor não aceita); o abajur acende só se o motor diz que a luz está ligada (índice da peça em `lit` do retrato e `lights` do delta do interior); usos Ligar/Desligar na roda do objeto. Passos de captura: `goto:X,Y`, `skip:S`, `speed:N`, `delta:MS`; ver [docs/domain/sandbox.md](../../docs/domain/sandbox.md) |
| 5º passo de jogo (#268) | ler o jornal abre as vagas na roda com "Candidatar-se" (o corpo vai ao empregador pelo "ir até" e o resultado vem em aviso) · faixa "⏸ Tempo pausado (menu aberto)" quando a roda pausa · **U** liga e desliga o livre-arbítrio do corpo (`career.freeWill`) e o aviso `need-critical` desce o tempo · entrega com a hora do motor ("chega amanhã às 08:12") e "Ir à loja" na roda · "Chamar"/"Esperar terminar" no menu social de quem está num móvel · realce suave no chão do corpo conduzido visto de cima (`npc_actor.set_legible`) e câmera de rua colada em parede sobe até `STREET_VIEW_M` do corpo; ver [sandbox.md](../../docs/domain/sandbox.md) |
| Console de admin | só com `LW_ADMIN_TOKEN` (o lançador já define): F8 ou botão Console · abas Mundo (pausa com motivo e teto, retomar, velocidade), Busca (abre a ficha) e Auditoria · a barra do topo avisa todos quando o mundo está pausado |
| Economia | botão Tesouro na ficha da cidade (saldo e barras de entrou/saiu por mês) · botão Vendas do local de trabalho na ficha de um NPC empregado (dia, recurso, quantidade, valor) · Esc fecha |
| Tecnologias | botão Tecnologias da cultura na ficha do NPC (só aparece se a cultura já descobriu algo): chave, adoção e eixos ritmo/carga/alcance · Esc fecha |
| Moradores anônimos | a ficha da cidade mostra "Conhecer um dos N moradores anônimos" (pool agregado): abre a ficha anônima com "Ver em detalhe", que materializa o morador |
| Ler a cidade (AD-177, AD-181) | **X** raio-x (todos os prédios translúcidos, telhado quase some, continuam clicáveis) · **P** planta baixa (lotes, ruas por superfície e uso, obras com barra, acessos, reserva do traçado como traço, **pessoas como pontos** — amarelo = andando pela rota; clique abre a ficha; P/Esc fecha) · **H** mapa de calor do pisoteio (chão 3D e planta) · **Tab** ou **Alt+roda** alterna entre os prédios empilhados sob o cursor (Shift+Tab volta) · rótulo do prédio de perto (zoom ≤ 11) · clique numa rua abre a ficha dela; no quintal, a do prédio |
| Orientação | passe o mouse (dica com ação, alvo e corpo visível) · minimapa (clique/arraste) · avisos de eventos e marcador no mapa para evento com lugar (clicável, some em 30 s) · processos com barra de progresso |
| Crime | evento no mapa e na ficha (autor, vítima, caso, desfecho), sem cena nem animação; `recordOnly` (violência sexual, doméstica e contra criança) mostra **só o registro**, nunca o texto narrado (aviso, linha do tempo, dica e ficha) |
| Som | ambiente procedural com fade ao trocar de período, som da ação do NPC selecionado (catálogo `assets.json`) · M silencia |
| Relógio | pausa, passo, +1 ano e velocidades 1× a 200× na barra (só admin); o céu segue a hora do mundo |

## Estrutura

| Pasta | Papel |
| --- | --- |
| `Contract/` | Única camada que fala HTTP/WebSocket: snapshot, deltas por `sequence`, `replay`, reconexão, `ApiClient` (todas as demais rotas); valida contra `tests/contracts/shape.*.json` |
| `Store/` | Estado local reconstruível (`WorldStore`) e rótulos de ação vindos do servidor |
| `Contract/world_reader.gd` | Camada única de leitura: todo painel recebe este objeto; ficha do NPC em cache por hora do mundo |
| `Store/entity_registry.gd` | Registro local de entidades {kind, id}, nomes do catálogo e vínculos aprendidos das fichas |
| `Scenes/` | `app.gd` (raiz e ligação) · `app_ui.gd` (montagem) · `session.gd` (escopos) · `selection.gd` (clique e dica) · `inspector.gd` · `window_router.gd` · `follow_director.gd` · `shortcuts.gd` · câmera, interior, ciclo do dia, painéis |
| `Scenes/render/` | Um renderizador por tipo: terreno (+ `season_palette.gd`), cidade (densidade), prédio (+ `building_dress.gd`), NPC, processo, lavoura, fauna, espólio/objeto (MultiMesh), marcador de evento; `world_view.gd` coordena e escolhe o LOD |
| `Scenes/npc_look.gd`, `npc_props.gd` | Corpo pelo estado (função pura, testável) e as peças geradas por código |
| `Scenes/inspector_describe.gd`, `event_timeline.gd` | Texto e links de cada tipo de ficha; linha do tempo com Próximo e Assistir |
| `tests/` | Testes headless (`bash scripts/client-test.sh [--filter arquivo::teste]`), com transporte falso |

Capturas e demos automáticas: `-- --steps "wait:2;city;wait:1;shot:/tmp/a.png;quit"` (ver `Scenes/dev_steps.gd`); do menu, `--steps "menu;new;step:3;shot:…"` (`Scenes/menu/menu_steps.gd`); `menu;resume;…` (#246) Continua o mundo que a API já tem, sem recriar, e segue com passos do jogo (dois dias no mesmo mundo; termine com `unplayer`).

## Desempenho

Piso do ADR-0047 e histórico das medidas: [docs/desempenho.md](docs/desempenho.md) (AD-189: mundo rodando 63 FPS, p95 ≤ 22 ms).
AD-193: o canteiro sobe pela obra do motor (`works`), sem regressão (`benchrun` mediana 60,3 → 60,7 FPS; motor 1.000 NPCs 0,99–1,01×).
AD-194: pilha pelo material no lote da casa e eventos de obra do log; pareado (cliente 2e0852ed → entrega) `bench` 162,7 → 163,1,
`benchbig` 131,1 → 130,6, `benchrun` 58,6 → 58,4 FPS (medianas), sem regressão.
AD-197d (câmera 3ª/1ª pessoa, WASD por intenção, menu de interações do motor), pareado contra `29ba981f` (medianas): `stress` 115,7 → 115,7,
`bench` 170,0 → 170,2, `benchbig` 138,6 → 138,0, `benchrun` 57,3 → 59,1 FPS; `benchrun` em 3ª pessoa (passo `third`) 64,3 FPS. Canal: `move` 0,12 ms (p95 0,14).
AD-197j (passo no tempo do corpo, escada, golpe animado, mira, cursor preso), contra `8ea77bfb` (medianas): `stress` 116,0 → 115,7,
`bench` 169,2 → 169,1, `benchbig` 138,1 → 138,1, `benchrun` 59,2 → 58,5 FPS (ruído); 3ª pessoa 64,2.
AD-197k (passo solto, olhar sem botão), contra `21c793fe` (medianas): `stress` 115,6 → 115,9, `bench` 176,7 → 176,1,
`benchbig` 138,7 → 138,8, `benchrun` 60,2 → 59,8 FPS; 3ª pessoa 67,5. Passo solto: mediana 0,575 m/s; `walk` 0,086 ms no canal.
AD-197r (câmera em passagem estreita, olhos pelo corpo, pessoas sem se sobrepor só no desenho), contra `e7727571` (medianas de 3): `stress` 115,5 → 115,6,
`bench` 171,0 → 169,3, `benchbig` 136,2 → 136,8, `benchrun` 59,2 → 58,8; 3ª pessoa pareada (`tools/bench-third-paired.sh`, mesmo corpo) 118,8 → 118,7. `npc_avoid` 0,10 ms/quadro.
#222 (criador em etapas: janela central, 5 etapas, boneco girando num SubViewport que só desenha com o criador aberto; fechado não custa quadro, sem medição de `stress` nesta rodada), AD-197q/t (criador de personagem; AD-197t monta a pessoa, prévia pelo estado do motor), contra `3eba4fe7`: `stress` 116,0/116,0/115,4 → 115,3/115,5/116,2.
AD-197s (3ª pessoa com o corpo à vista, translucidez que sobrevive ao redesenho, 1ª pessoa fora da parede), contra `c0979602` (medianas de 3): `stress` 115,6 → 115,3,
`bench` 170,1 → 171,1, `benchbig` 136,9 → 137,3, `benchrun` 57,9 → 58,5; 3ª pessoa pareada (`tools/bench-third-paired.sh`) 120,5 → 120,8 (máquina ruidosa: 110 a 148 nas duas pontas).
AD-197s3 (câmera do jogador em metros do motor, histerese cômodo/telhado, ombro antes de subir), contra `70e76523` (medianas de 3): `stress` 115,8 → 116,0,
`bench` 167,9 → 168,1, `benchbig` 135,0 → 135,4, `benchrun` 58,2 → 58,5; 3ª pessoa pareada 110,9 → 110,3 (ruído da máquina: 89 a 116).
AD-197s4 (resto da escala em metros, ombro que mira o corpo), contra `077982d8` (medianas de 3): `stress` 115,8 → 115,8, `bench` 167,5 → 167,3,
`benchbig` 134,5 → 134,6, `benchrun` 58,3 → 58,3; 3ª pessoa pareada (5 rodadas, depois de hoistar a conversão do desvio) 114,9 → 114,0 (ruído 94 a 116).
AD-198 a AD-206 (épico AAA #228: controle local, corpo articulado, câmera, retorno, roda, combate, metas, casa em foco), contra `96b892b1` (medianas de 3): `stress` 115,1 → 115,0; com controle+câmera+roda+corpo 115,7 → 115,3.
#238 (guia da primeira posse, "O que dá para fazer" na ajuda e chão no cartão; só texto e um rótulo, sem custo por quadro), contra `0978b90f` (medianas de 3): `stress` 115,9 → 115,8.
#254 (arte procedural dos NPCs: malhas geradas e compartilhadas, rosto, cabelo, roupa por período, mistura de poses), contra `97cee901` (medianas de 3): `stress` 114,1 → 113,7, `stress_look` 115,7 → 114,4 FPS. Capturas: `tools/portraits.tscn`.
#262 (humor, momentos e desejos no cartão; só rótulos, sem custo por quadro), contra `origin/feat/gameplay-aaa` (3 pares alternados): `stress` 116,6 → 115,8 (ruído: 115,4 a 116,7).
AD-189: chuva e neve inclinam pela direção do vento do motor (`windFromDeg`), a copa enche, vira e cai pela `foliage`, a janela
acende pelo `lit` do prédio, o tracejado vai até o `intentTarget` e a temperatura da hora sai de `hourlyC` (o clima não volta a cada tick). Aparência pelo estado, rua, rota e tempo do
dia: [docs/aparencia-e-cidade.md](docs/aparencia-e-cidade.md).

**Popup (#240)**: toda janela central entra no registro único com UMA chamada, `app.modal.watch(controle)` (ou `app.modal.set_open(chave, on)`); aberto, some mira/anel/dica do E/números, E e clique não disparam e o app pede o hold (pausa do mundo só single player) ao motor.
#241 (ação em andamento: barra, gesto de machado em laço, bloqueio do WASD, Espaço interrompe; árvore como mata), contra `4cf95790` (3 rodadas alternadas): `stress` 116,0/116,7/117,2 → 116,1/116,5/116,3 (mediana 116,7 → 116,3, ruído). Motor: a oferta `nature` olha 25 células só com as mãos livres, sem custo por NPC-tick.
#248 (andar sem voltar: o motor anda o rumo mantido a cada tick, comando numerado, cliente reaplica o que o motor não viu e prevê pela máscara de chão do motor), contra `ec206a8d` (3 rodadas alternadas, mesma API): `stress` 116,1/134,4/141,2 → 116,0/140,6/141,1 (a rodada muda com a taxa do monitor, 120 ou 145 Hz; pares iguais). Motor, `compare-simulation-cost` (3 rodadas): 1,12 → 0,89 µs/NPC-tick (ruído; amostras 0,86–1,22) e alocação 77.276 → 77.250 B/tick. Passo ao vivo (`walkloop:90`, 90 s virando o olhar; `walkto` na parede da casa; `walkstats`): antes, afastamento médio entre a tela e a pose do motor 0,22 m (pior 1,36 m), 4 recuos visíveis (1,48 m ao todo); depois, 0,017 m (pior 0,31 m), 0 recuos (0,08 m ao todo); na parede, 0,45 m (pior 0,52) → 0,03 m (pior 0,07). Puxões > 0,3 m num quadro: 0 antes e depois (o antigo escondia o puxão em correção lenta; a medida honesta é o afastamento e o recuo). Simulado (`test_walk_sync`, 3 min, rede de 5 a 45 ms por perna): 0 puxões, pior afastamento 0,19 m, média 0,02 m.
#226 (parede em aresta, cômodos por catálogo, painéis finos nas arestas do motor, tom de piso por cômodo), contra `4a830fea` (1 rodada): `stress` 116,7 → 116,3; motor `MeasureHarness` 0,884 → 0,845 µs/NPC-tick.
#252 (sandbox do casal: botão de início rápido, troca de corpo por G com a câmera deslizando, nome dos móveis do cenário), contra `3f1ba09d` (2 rodadas): `stress` 115,7 / 115,5 FPS (antes ~116); motor 150 NPCs, 5,56 → 5,53 µs/NPC-tick e 12,50 KB/tick iguais.
#256 (início rápido entra no corpo, relógio relido ao possuir, passada pela velocidade real): `stress` 116,5 / 116,4 FPS (2 rodadas; antes ~116, README #252); motor: só o plano do status e o zerar do Δ manual, sem custo por NPC-tick.
#259 (S3 do sandbox: interações sociais em dados, relação visível, menu "Interagir ▸"): `stress` 117,1 / 117,1 FPS (2 rodadas, monitor 120 Hz; antes ~116). Motor, `profile.sh --pop 300 --ticks 720` contra `4023b328`: 6,056 → 6,120 µs/NPC-tick (ruído), 77 B/NPC-tick iguais; o mundo sem catálogo social só paga uma checagem de nulo.
#266 (móveis com pegada, giro e modelos procedurais do catálogo: caixas chanfradas, materiais, LOD que some o detalhe de longe, malhas e materiais compartilhados), contra `19627aa1` (3 rodadas alternadas, monitor 120 Hz, mesma API): `stress` 116,9/114,7/117,2 → 116,8/116,9/116,5; a casa do sandbox com a mobília inteira, de cima, 117,4/119,2/119,5 → 118,8/118,8/119,3 e de perto 116,7/119,8/119,7 → 119,8/119,8/120,0 (teto do monitor); custo da cena: +400 nós e +189 objetos desenhados (737 → 926) na vista de cima da casa. Motor: planejar a mobília de uma casa, < 25 ms no teste e uma vez por casa (em cache por versão da obra); sem custo por NPC-tick. Passos `buylook` e `fps:S`.
#268 (5º passo de jogo: vagas na roda, faixa de pausa, livre-arbítrio, Chamar, realce do corpo, câmera de rua), contra `f7e13c19` (2 rodadas alternadas, mesma API, monitor 120 Hz): `stress` 116,1/115,1/115,8 e 116,4/115,7/115,5 → 116,0/117,0/116,2 e 116,0/115,6/116,1 (iguais; o realce são 2 malhas só no corpo do jogador). Motor, `compare-simulation-cost` (150 NPCs, 3 rodadas): 5,41 → 5,27 µs/NPC-tick (ruído; amostras 4,02–5,44 e 5,25–5,71) e 16.174 → 16.181 B/tick; o mundo sem `NeedCritical` paga uma checagem de nulo por despertar do corpo conduzido.

## 3º teste de jogo (#267)
Clique = ir até/interagir (pessoa, móvel, prédio; `player_hud.click_target`, `POST /players/{id}/go`); golpe: **R** ou Shift+clique parado. "Ir agora" e "Pular para a volta do turno" sob o relógio; "Os dois estão fora"
(`player_hud.both_away_changed`); relação empilhada acima do cartão; rótulos de prédio sem sobreposição (`building_labels.hidden_ids`); câmera de interior sobre a casa (`interior_camera.keep_in_house`); chapéu pelo traje do motor.
Passos de captura: `clickgo:Rafael|obj=Sofá|place=Mercado`, `relinfo:Rafael`, `gonow`, `skipreturn`, `turnui`. Medidas contra `14a3af4b` (2 rodadas alternadas): `stress:500` 115,9 / 116,1 → 116,2 / 116,0 FPS (monitor 120 Hz); motor `profile.sh --pop 300 --ticks 720` 5,327 / 5,391 → 5,479 / 5,320 µs/NPC-tick (ruído), 78 B/NPC-tick iguais; o humor só custa em mundo com catálogo.
