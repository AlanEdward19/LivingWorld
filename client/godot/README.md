# Cliente Godot — Living World

Cliente gráfico só de leitura (ADR-0022). O motor (`src/LivingWorld.Api`) é dono do estado, do tempo e do
determinismo; este projeto só desenha o que a API projeta. Godot 4.7 (qualquer build; o .NET não é usado), tudo em GDScript.

## Rodar

Atalho: `bash scripts/play.sh` sobe a API (mundo vivo) e abre o cliente no **menu principal** (Continuar · Novo mundo · F10 volta ao menu; Novo mundo tem "Personalizar" (mapa, assentamentos, prédios, famílias e pessoas) e, depois de criar, "Retoques"). Manualmente:

```bash
# 1. API (outro terminal) — porta 5214
ADMIN_TOKEN=dev ConnectionStrings__World="Data Source=/tmp/lw.db" TICK_LOOP_ENABLED=true dotnet run --project src/LivingWorld.Api -c Release --urls http://localhost:5214
# 2. Cliente
LW_ADMIN_TOKEN=dev /Applications/Godot_mono.app/Contents/MacOS/Godot --path client/godot
```

Pausa, velocidade, passo e +1 ano são do admin (ADR-0015): a API exige `X-Admin-Token` igual a `ADMIN_TOKEN` e o cliente manda o valor de `LW_ADMIN_TOKEN` (o `play.sh` gera um por sessão). Sem o token esses botões recebem 403 e nada muda.

## Controles

| Ação | Tecla / mouse |
| --- | --- |
| Mover / girar / zoom | botão direito arrasta · Shift+arrasta · roda · WASD/setas · Q/E |
| Entrar na cidade | zoom até a escala de cidade, ou duplo clique na cidade |
| Entrar numa casa | clique no prédio (andares: PgUp/PgDn) · Esc sai |
| Pessoas | L abre a lista (segue a cidade em foco) · clique num NPC · botão Seguir ou F |
| Cérebro do NPC | B (ou botão Cérebro na ficha) abre/fecha o painel das decisões do NPC selecionado: linha do tempo + fluxo Estímulo → Ponderação → Decisão · Vigiar retém o rastro inteiro · **E se…?** simula a decisão com outros estímulos (nada é gravado) · Esc fecha |
| Conversar | T (ou botão Conversar na ficha) pede a conversa; o NPC pode recusar (ocupado, hostil, indisponível) · Enter envia · Esc encerra |
| Narrativas | botão Biografia na ficha do NPC · botão Crônica na ficha da cidade (mês/ano/década) · R lista os relatos que circulam, com a confiança de cada um · botão O que a cidade acredita (crenças distorcidas e a distância do fato) · Esc fecha |
| Jogar | I (ou botão Encarnar) toma o NPC selecionado; a faixa do topo pede a ação (só as que o motor aceita), "Deixar o motor decidir" ou Sair; sair faz o personagem desaparecer, não congelar; se o cliente reiniciou com o personagem ainda ativo, o HUD oferece "Sair do personagem anterior". `playerId` local em `user://player.cfg` (sem login ainda) |
| Console de admin | só com `LW_ADMIN_TOKEN` (o `play.sh` já define): F8 ou botão Console · abas Mundo (pausa com motivo e teto, retomar, velocidade), Busca (abre a ficha) e Auditoria · a barra do topo avisa todos quando o mundo está pausado |
| Economia | botão Tesouro na ficha da cidade (saldo e barras de entrou/saiu por mês) · botão Vendas do local de trabalho na ficha de um NPC empregado (dia, recurso, quantidade, valor) · Esc fecha |
| Tecnologias | botão Tecnologias da cultura na ficha do NPC (só aparece se a cultura já descobriu algo): chave, adoção e eixos ritmo/carga/alcance · Esc fecha |
| Moradores anônimos | a ficha da cidade mostra "Conhecer um dos N moradores anônimos" (pool agregado): abre a ficha anônima com "Ver em detalhe", que materializa o morador |
| Orientação | passe o mouse (dica) · minimapa (clique/arraste) · avisos de eventos |
| Som | ambiente procedural com fade ao trocar de período, som da ação do NPC selecionado (catálogo `assets.json`) · M silencia |
| Relógio | pausa, passo, +1 ano e velocidades 1× a 200× na barra; o céu segue a hora do mundo |

## Estrutura

| Pasta | Papel |
| --- | --- |
| `Contract/` | Única camada que fala HTTP/WebSocket: snapshot, deltas por `sequence`, `replay`, reconexão, `ApiClient` (todas as demais rotas); valida contra `tests/contracts/shape.*.json` |
| `Store/` | Estado local reconstruível (`WorldStore`) e rótulos de ação vindos do servidor |
| `Scenes/` | Mundo 3D (terreno, água, árvores, prédios com telhado e janelas), câmera (`CameraRig`, trocável), NPCs articulados por pose, interior multi-andar, ciclo dia/noite, minimapa, avisos, painéis |
| `tests/` | Testes headless (`bash scripts/client-test.sh [--filter arquivo::teste]`), com transporte falso |

Capturas e demos automáticas: `-- --steps "wait:2;city;wait:1;shot:/tmp/a.png;quit"` (ver `Scenes/dev_steps.gd`).

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
Catálogo de objetos (2026-10-15, #47 fatia 1, M1 Pro, `stress:500`, 3 rodadas): antes 66,8 (1ª execução do worktree, importação) / 76,7 / 76,0; depois 75,9 / 76,2 / 76,7 (sem regressão; a mobília só é instanciada ao abrir um interior, com mesh e material compartilhados por id e variação).
