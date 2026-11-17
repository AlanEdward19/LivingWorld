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
| Jogar | I (ou botão Encarnar) toma o NPC selecionado; a faixa do topo pede a ação (só as que o motor aceita), "Deixar o motor decidir" ou Sair; sair faz o personagem desaparecer, não congelar; se o cliente reiniciou com o personagem ainda ativo, o HUD oferece "Sair do personagem anterior". `playerId` local em `user://player.cfg` (sem login ainda) |
| Console de admin | só com `LW_ADMIN_TOKEN` (o lançador já define): F8 ou botão Console · abas Mundo (pausa com motivo e teto, retomar, velocidade), Busca (abre a ficha) e Auditoria · a barra do topo avisa todos quando o mundo está pausado |
| Economia | botão Tesouro na ficha da cidade (saldo e barras de entrou/saiu por mês) · botão Vendas do local de trabalho na ficha de um NPC empregado (dia, recurso, quantidade, valor) · Esc fecha |
| Tecnologias | botão Tecnologias da cultura na ficha do NPC (só aparece se a cultura já descobriu algo): chave, adoção e eixos ritmo/carga/alcance · Esc fecha |
| Moradores anônimos | a ficha da cidade mostra "Conhecer um dos N moradores anônimos" (pool agregado): abre a ficha anônima com "Ver em detalhe", que materializa o morador |
| Orientação | passe o mouse (dica com ação, alvo e corpo visível) · minimapa (clique/arraste) · avisos de eventos e marcador no mapa para evento com lugar (clicável, some em 30 s) · processos com barra de progresso |
| Crime | evento no mapa e na ficha (autor, vítima, caso, desfecho), sem cena nem animação; `recordOnly` (violência sexual, doméstica e contra criança) mostra **só o registro**, nunca o texto narrado (aviso, linha do tempo, dica e ficha) |
| Som | ambiente procedural com fade ao trocar de período, som da ação do NPC selecionado (catálogo `assets.json`) · M silencia |
| Relógio | pausa, passo, +1 ano e velocidades 1× a 200× na barra (só admin); o céu segue a hora do mundo |

## Aparência pelo estado (ADR-0047 etapa 3, AD-171)

Formas geradas por código (sem pacote de assets), a partir da projeção do observável (AD-170):
- **Pessoas (todas, não só a da ficha)**: escala pela altura real (bebê, criança, adulto, idoso), cabelo pelo sexo, roupa e chapéu do ofício (catálogo `assets.json` ou o nome do ofício), barriga de gravidez, palidez e esteira de quem está de cama, roupa de luto, postura pela emoção dominante, saco ou jarro da carga nas costas. **Perto** (zoom ≤ 7,5): ferramenta do ofício na mão no trabalho, recipiente levado (forma e cor de `/designs/{id}`) e rosto com expressão. Quem conversa com alvo vira para o outro e uma linha discreta liga os dois.
- **Prédios**: enfeite e placa pela função (`buildingStates`): chaminé, hospedaria com lanterna, fazenda com cerca, ferraria, padaria, mercado com toldo, taverna, pedreira, templo; fechado com tábuas na porta.
- **Lavoura**: canteiro por estágio (sem semear, plantado, crescendo, maduro, colhido, pousio), cor e altura pela cultura, amarelado com sede.
- **Estação e clima**: paleta do chão e das copas (primavera, verão/seca, outono, inverno), neve caindo abaixo de 0 °C; estação e temperatura no relógio do topo.
- **Fauna**: animais por espécie na cidade; bandos com o número no mapa-múndi. **Espólio** (saco e trouxa) e **objetos inventados** fora de casa pela forma e material do design.
- **Mapa-múndi vivo**: um ponto por morador na rua, na cor da ação; a placa diz quantos estão na rua, no pool, o que fazem e a temperatura.
- **LOD em três níveis**: mundo (densidade, bandos, eventos) · cidade (corpos, prédios, lavoura, animais, objetos) · perto (ferramenta, objeto em uso, rosto). Lavoura, fauna, itens, densidade e linhas de conversa são MultiMesh por tipo; peças do corpo têm malha e material compartilhados e não projetam sombra.

Delta enxuto (AD-172): num NPC já conhecido, `city: null` = mesma cidade, `appearance` ausente = a anterior, `actionStartedAtTick` ausente = início, alvo e intenção de antes (presente = grupo novo), `observable` ausente = não mudou; `rest`/`food` não reenviados avançam pelo tick do delta (`Store/observable_parse.gd` `advance_processes`). Nomes vêm do escopo (`observable.name`): a paginação periódica de `GET /npcs` saiu; prédio novo chega com a planta no delta (sem reler o retrato da cidade). **Leitura**: prédio que fica na frente do que está sob o mouse, da pessoa selecionada ou do que se segue fica translúcido enquanto cobrir (e o mouse atravessa o translúcido); quem divide uma célula se espalha nela (lugar estável por id) e quem se cruza passa pela direita — o clique pega o corpo onde ele está desenhado; quintal de terra em volta de cada prédio, rua no resto (camada `Roads` do mundo, quando vier, decide a rua). Delta: chave ausente e `null` valem o mesmo. Resposta fora do contrato vira uma faixa no topo com o primeiro campo divergente (antes ficava em "Conectando…").

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

Capturas e demos automáticas: `-- --steps "wait:2;city;wait:1;shot:/tmp/a.png;quit"` (ver `Scenes/dev_steps.gd`); do menu, `--steps "menu;new;step:3;shot:…"` (`Scenes/menu/menu_steps.gd`).

## Desempenho

`-- --steps "wait:2;stress:500;quit"` põe 500 NPCs andando e imprime os quadros por segundo. Medido num M1 Pro: ~77 FPS com a câmera longe (NPCs simplificados) e ~52 FPS perto. Acima de 150 NPCs e zoom longe, o NPC vira tronco e cabeça; fora da vista só interpola a posição.

Zoom LOD (2026-10-13, M1 Pro, `stress:500`, câmera padrão): antes 78,1 / 77,8 / 78,6 FPS; depois 78,4 / 78,9 / 76,9 (teto de 79 pelo vsync, ruído de ±1). No mapa-múndi afastado os NPCs viram bolhas (acima de 60% do zoom máximo).
Arte e áudio (2026-10-14, M1 Pro, `stress:500`, câmera padrão, API local): 75,8 / 77,2 / 75,3 FPS (teto de 77-79 pelo vsync), contra 76,9-78,9 antes do catálogo: dentro de ±3 e bem acima dos 50 FPS exigidos.
Painel Cérebro (2026-10-14, M1 Pro, `stress:500`, API local): sem painel 79,4 / 80,0 / 77,7 FPS; com o painel aberto num NPC 77,2 / 77,5 / 76,4 (média 77,0 contra 79,0: dentro de ±3; antes da mudança 77,3 / 78,3 / 75,6). O painel só busca `/npcs/{id}` quando a hora do mundo muda e fechado não faz requisição.

O rastro de cognição só é gravado para NPC em escopo observado: `Contract/scope_observer.gd` publica o escopo da câmera (`POST /observation/scope`, batimento de 10 s, `null` ao voltar ao menu).
Janela de conversa (2026-10-14, M1 Pro, `stress:500`, API local): sem janela 79,4 / 78,2 / 78,8 FPS; com a janela aberta 78,2 / 77,7 / 77,8 (dentro de ±3).
Painel "E se…?" (2026-10-14, M1 Pro, `stress:500`): sem painel 77,6 / 78,3 / 78,6 FPS; com o painel simulando 74,4 / 76,2 / 76,2 (média -2,6, dentro de ±3).
Narrativas (2026-10-14, M1 Pro com a máquina ocupada, `stress:500`): sem janela 75,0 / 72,4 / 71,8 FPS; com a biografia aberta 73,4 / 72,6 / 73,1 (sem diferença).
HUD do jogador (2026-10-14, M1 Pro com a máquina ocupada, `stress:500`): sem HUD 74,7 / 73,7 / 72,7 FPS; com o HUD ativo 73,1 / 73,6 / 72,6 (sem diferença).
Console de admin (2026-10-14, M1 Pro com a máquina ocupada, `stress:500`): sem console 71,0 / 70,7 / 76,8 FPS; com a busca aberta 72,6 / 73,0 / 72,7 (sem diferença).
Economia (2026-10-14, M1 Pro, `stress:500`): sem janela 74,0 / 75,0 / 76,2 FPS; com as vendas abertas 77,7 / 76,9 / 78,1 (sem regressão).
Tecnologias (2026-10-14, M1 Pro, `stress:500`): sem janela 75,5 / 75,0 / 74,9 FPS; com a janela aberta 77,0 / 75,8 / 74,5 (sem regressão).
Consolidado (2026-10-15, M1 Pro, máquina ociosa, `stress:500`, API local, 3 rodadas): base 75,9 / 77,2 / 77,0 (e 77,1 / 75,8 / 75,7 no fim); cérebro 73,6 / 74,9 / 75,8; "E se…?" 74,3 / 75,5 / 73,0; conversa 75,9 / 76,4 / 75,4; narrativa 75,9 / 75,6 / 76,1; vendas 75,8 / 75,0 / 77,7; tecnologias 76,5 / 75,7 / 76,6; console 74,7 / 75,6 / 75,1; jogador 76,0 / 76,1 / 75,4; ficha com botão de materializar 75,9 / 75,8 / 76,1. Pior caso ("E se…?") 2,1 FPS abaixo da base: dentro de ±3.
Aparência herdada (2026-10-15, M1 Pro, `stress:500`, API local, 3 rodadas): sorteio local de sempre 76,6 / 74,0 / 77,4 FPS; com genes do motor em todos os NPCs (`stress_look`) 75,0 / 75,6 / 75,2 (média -0,7, dentro de ±3; materiais compartilhados por combinação, no máximo 21 de pele e 21 de cabelo).

Mundos salvos e leitos (2026-10-15, M1 Pro, `stress:500`, API local): base 74,4 / 77,6 / 75,4 FPS; entrega 77,3 / 76,2 / 75,0. Medianas 75,4 → 76,2; sem regressão. Repetir com `bash scripts/client-stress.sh`.
Catálogo de objetos (2026-10-15, #47 fatia 1, M1 Pro, `stress:500`, 3 rodadas): antes 66,8 (1ª execução do worktree, importação) / 76,7 / 76,0; depois 75,9 / 76,2 / 76,7 (sem regressão; a mobília só é instanciada ao abrir um interior, com mesh e material compartilhados por id e variação).
Uso de objeto (2026-10-16, #47 fatia 2, M1 Pro com carga 8–11, `stress:500`, 3 rodadas): 77,8 / 72,9 / 74,9 FPS (a posição e a pose de uso só são resolvidas ao abrir ou atualizar um interior; nada novo por quadro na cidade).
Indicadores da cidade (2026-11-09, #112 fatia E, M1 Pro com a máquina dividida com outros agentes, `stress:500`, API local, 3 rodadas): antes 76,1 / 73,6 / 74,6 FPS; depois 72,3 / 71,9 / 73,6 (mediana 74,6 → 72,3; -2,3, dentro de ±3 e do ruído da máquina compartilhada). A mudança só toca a ficha da cidade (`show_city`, 1 linha a mais), que o `stress` nunca abre; nada novo por quadro. `client-test.sh`: 288 passaram, 0 falharam.

### Medida recalibrada (ADR-0047, AD-169)

`bash client/godot/tools/bench.sh` (pela fila pesada) sobe uma API própria (porta 5291, período `cidade-media`, seed 2) e roda 3× `stress:500` (medida antiga, com vsync) e 3× `bench:500`: sem vsync, cidade real aberta, ficha do 1º morador aberta, multidão completada até 500 NPCs andando dentro da cidade; FPS médio e tempo de quadro p95 de 6 s. `LW_BENCH_CLIENT` aponta outra cópia do cliente para comparar.
Antes (2026-11-17, M1 Pro, máquina dividida com outros agentes, base `8f792e50`): `stress:500` 74,0 / 74,5 / 72,7 FPS; `bench:500` 76,5 / 77,7 / 77,2 FPS, p95 14,03 / 13,69 / 13,77 ms (cidade com 5 prédios).
Depois (AD-169, mesma máquina e comando, vsync confirmado desligado `vsync=0`): `stress:500` 73,3 / 71,9 / 71,8 FPS (média 72,3 contra 73,7: −1,4, dentro do ±3 de ruído histórico desta medida); `bench:500` 77,1 / 77,0 / 77,1 FPS (média 77,1 = antes), p95 14,06 / 13,89 / 14,10 ms (+0,2 ms, ruído). Uma rodada intermediária, antes de o inspetor deixar de refazer os links a cada segundo, deu 76,6 / 76,6 / 76,2 e p95 ~14,3 ms. Muito acima do piso de 50 FPS. `client-test.sh`: 291 → 304 testes, 0 falhas.

### Aparência pelo estado (AD-171, ADR-0047 etapa 3)

`bench.sh` ganhou a 3ª medida, `benchbig:500`: a mesma cidade com 40 prédios 2x2 a mais e, no cliente com a projeção do observável, estado de prédio (placas, enfeites), 30 lavouras, 20 animais e corpo pelo estado nos 500 NPCs (a cidade de 5 prédios era pequena para pegar regressão). Mesma máquina (M1 Pro dividida com outros agentes), base `c6df9eac` (cópia do cliente, `LW_BENCH_CLIENT`) e entrega, 3 rodadas cada:
- `stress:500` (com vsync): antes 72,3 / 73,2 / 72,7 FPS; depois 71,9 / 71,5 / 71,9 (média 72,7 → 71,8).
- `bench:500` (sem vsync, cidade real): antes 75,6 / 77,2 / 78,3 FPS, p95 14,22 / 13,99 / 13,88 ms; depois 76,3 / 75,9 / 75,9, p95 14,09 / 14,28 / 14,26 (média 77,0 → 76,0; p95 14,03 → 14,21 ms).
- `benchbig:500`: antes 67,2 / 67,2 / 66,8 FPS, p95 16,00 / 16,06 / 16,17 ms; depois 66,5 / 66,7 / 66,1, p95 16,58 / 16,28 / 16,16 (média 67,1 → 66,4; p95 16,08 → 16,34 ms).
Diferenças dentro do ruído histórico (±3 FPS) e muito acima do piso de 50. Uma rodada antes de fundir chapéu e saco numa malha só deu 75,4 e 65,7. O mundo do `bench.sh` nasce pausado: com o mundo rodando, ver a lacuna das formas do contrato (backlog 09, 1b). `client-test.sh`: 304 → 329 testes, 0 falhas.
Depois dos pedidos do dono (prédio translúcido, NPCs espalhados na célula, delta sem nulos), mesma medida: `stress:500` 71,5 / 70,9 / 72,3 FPS; `bench:500` 75,9 / 75,5 / 76,1, p95 14,12 / 13,76 / 13,82 ms; `benchbig:500` 66,0 / 66,4 / 66,2, p95 16,19 / 16,01 / 16,12 ms (dentro do ruído; o teste de cobertura roda junto com a dica, a cada 70 ms).

### Menu de videogame e pausa (AD-174)

`bench.sh 3` pela fila pesada, mesma máquina (M1 Pro dividida com outros agentes), cópias do cliente fora do repo (`LW_BENCH_CLIENT`), base `9711face` → entrega. O `--steps` pula o menu; mede o jogo com as camadas novas (dicas, ajuda, pausa, cortina, botão do mapa):
- `stress:500`: 71,0 / 71,4 / 71,1 → 73,6 / 72,9 / 72,6 FPS.
- `bench:500`: 74,8 / 76,5 / 76,0 FPS, p95 13,93 / 13,82 / 13,82 ms → 77,0 / 76,3 / 76,1, p95 13,45 / 13,76 / 13,81.
- `benchbig:500`: 64,6 / 66,3 / 65,9 FPS, p95 16,61 / 16,02 / 16,22 ms → 65,8 / 65,6 / 64,9, p95 16,22 / 16,04 / 17,06.
Sem regressão (medianas 71,1 → 72,9; 76,0 → 76,3; 65,9 → 65,6, dentro do ruído). `client-test.sh`: 330 → 346 testes, 0 falhas.
