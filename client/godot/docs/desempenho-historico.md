# Cliente Godot: histórico de desempenho até 2026-11-09

> Parte de [desempenho.md](desempenho.md). Uma linha por fatia, `stress:500` (com vsync), M1 Pro.

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
