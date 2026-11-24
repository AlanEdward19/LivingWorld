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
| Jogar | I (ou botão Encarnar) toma o NPC selecionado e a câmera passa à **3ª pessoa** (C alterna 1ª/3ª; o cursor entra preso e o **mouse olha só de mexer**; **Z** solta para os menus e prende de novo, o menu do E, a conversa e o Esc soltam sozinhos; a mira é o centro da tela); **WASD/setas** andam soltos relativos ao olhar, em frações de célula à velocidade do corpo (rumo `walk` pelo canal a cada 0,1 s; o motor anda o corpo e valida parede, porta e gente na grade; o cliente prevê e corrige pela posição fina); o passo leva o tempo que o motor diz (corpo, carga, cansaço); **E** abre só o que o motor oferece ao alcance (porta, escada, despensa, banca, lavoura, fonte, espólio, cama, pessoa, a própria casa; furto onde vale); **clique** bate no NPC sob a mira; a faixa do topo pede a ação, "Deixar o motor decidir" ou Sair (volta à isométrica). Recusa da pessoa possuída aparece com o motivo do motor |
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

Capturas e demos automáticas: `-- --steps "wait:2;city;wait:1;shot:/tmp/a.png;quit"` (ver `Scenes/dev_steps.gd`); do menu, `--steps "menu;new;step:3;shot:…"` (`Scenes/menu/menu_steps.gd`).

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
AD-197q (criador de personagem "Pessoa nova", fechado por padrão), contra `024c2748`: `stress` 116,8/116,0/116,7 → 116,7/116,5/116,9.
AD-197s (3ª pessoa com o corpo à vista, translucidez que sobrevive ao redesenho, 1ª pessoa fora da parede), contra `c0979602` (medianas de 3): `stress` 115,6 → 116,1,
`bench` 170,1 → 170,5, `benchbig` 136,9 → 136,9, `benchrun` 57,9 → 58,2; 3ª pessoa pareada (`tools/bench-third-paired.sh`) 120,5 → 120,8 (máquina ruidosa: 110 a 148 nas duas pontas).
AD-189: chuva e neve inclinam pela direção do vento do motor (`windFromDeg`), a copa enche, vira e cai pela `foliage`, a janela
acende pelo `lit` do prédio, o tracejado vai até o `intentTarget` e a temperatura da hora sai de `hourlyC` (o clima não volta a cada tick). Aparência pelo estado, rua, rota e tempo do
dia: [docs/aparencia-e-cidade.md](docs/aparencia-e-cidade.md).
