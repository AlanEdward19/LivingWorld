# Cliente Godot: desempenho (ADR-0047)

> Parte do [README](../README.md). Piso: ≥ 50 FPS no M1 Pro com 500 NPCs, sem vsync e com a cidade aberta.

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

### Cidade que o motor diz (AD-177)

`bench.sh 3` pela fila pesada, M1 Pro dividida com outros agentes, base `01b35a38` (cópia do cliente, `LW_BENCH_CLIENT`) →
entrega; a cidade do `bench.sh` agora tem 22 células de rua e 6 quintais do motor desenhados (ruas e lotes em 2 MultiMesh):
- `stress:500`: 89,0 / 87,5 / 88,5 → 87,0 / 88,4 / 88,4 FPS.
- `bench:500`: 70,0 / 70,2 / 70,1 FPS, p95 15,43 / 15,15 / 15,46 ms → 71,1 / 70,7 / 71,1, p95 15,31 / 15,38 / 15,21.
- `benchbig:500`: 61,4 / 61,1 / 61,0 FPS, p95 17,57 / 18,04 / 17,57 ms → 61,4 / 61,1 / 59,9, p95 17,75 / 17,88 / 18,29.
Medianas 88,5 → 88,4; 70,1 → 71,1; 61,1 → 61,1 (dentro do ruído, acima de 50). `client-test.sh`: 359 → 369, 0 falhas.
Mundo rodando (40 ticks/s, `cidade-media` seed 2): Mundo 1.352 ticks e Cidade ~1.480 ticks, 0 divergência de contrato,
0 erro de script; a releitura da camada `Roads` trocou as ruas uma vez na Cidade sem recarregar.

### Rua de verdade e gente andando (AD-181)

`bench.sh 3` pela fila pesada, M1 Pro dividida com outros agentes, base `87c56bbd` (cópia do cliente, `LW_BENCH_CLIENT`) →
entrega, duas rodadas (a 1ª com uma hora entre base e entrega; a 2ª pareada, base e entrega no mesmo comando):
- 1ª: `stress:500` 92,8 / 93,4 / 93,5 → 90,0 / 90,2 / 87,9; `bench:500` 71,9 / 71,1 / 70,3 (p95 15,08 / 14,92 / 15,08 ms) →
  70,5 / 70,2 / 69,8 (p95 15,06 / 15,11 / 15,12); `benchbig:500` 61,5 / 61,6 / 61,9 (p95 17,58 / 17,61 / 17,13) → 60,4 / 60,9 / 60,7.
- 2ª (pareada): `stress:500` 86,3 / 88,4 / 89,0 → 88,2 / 87,2 / 87,8; `bench:500` 70,6 / 69,9 / 70,4 (p95 14,97 / 15,37 / 14,98) →
  69,7 / 70,1 / 70,0 (p95 15,03 / 15,08 / 14,98); `benchbig:500` 58,0 / 60,7 / 60,6 (p95 18,98 / 17,56 / 17,57) → 60,4 / 60,2 / 60,7.
Medianas pareadas 88,4 → 87,8; 70,4 → 70,0; 60,6 → 60,4 (ruído; acima de 50). Por quadro só entrou: relógio da rota, um teste
`walk != null` por boneco, linha da rota 5×/s e transição do chão só enquanto há rua mudando. O mundo do `bench.sh` está pausado
(sem rota nem delta de rua); quem anda custa O(1) amortizado por boneco (cursor que só avança). `client-test.sh` 369 → 381.
Mundo rodando (40 ticks/s, `cidade-media` seed 2): Mundo 2.041 deltas e Cidade 2.020, 0 divergência de contrato, 0 erro de
script; na Cidade 30 células de trilha nasceram por delta (0 → 30) e 14–31 pessoas andavam pela rota ao mesmo tempo.
