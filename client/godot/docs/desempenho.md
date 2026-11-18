# Cliente Godot: desempenho (ADR-0047)

> Parte do [README](../README.md). Piso: ≥ 50 FPS no M1 Pro com 500 NPCs, sem vsync e com a cidade aberta.

`-- --steps "wait:2;stress:500;quit"` põe 500 NPCs andando e imprime os quadros por segundo. Medido num M1 Pro: ~77 FPS com a câmera longe (NPCs simplificados) e ~52 FPS perto. Acima de 150 NPCs e zoom longe, o NPC vira tronco e cabeça; fora da vista só interpola a posição.

Medidas de 2026-10-13 a 2026-11-09 (`stress:500`, uma linha por fatia): [desempenho-historico.md](desempenho-historico.md).

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

### Mundo rodando, tempo do dia e o que o motor já mandava (AD-188)

`bench.sh` ganhou `benchrain` (o `bench:500` com chuva forte sintética no cliente, mundo pausado) e `benchrun` (depois de
`/simulation/resume` a 40 ticks/s: rota, delta de rua, tempo e clima chegando; `LW_BENCH_TPS`, `LW_BENCH_ONLY=run` e
`LW_BENCH_RUN_STEPS` para diagnóstico); cada `bench` imprime também `bench-cost:` (ms somados em 6 s de ler o JSON, conferir o
contrato, aplicar o delta, NPCs e chão; draw calls). Pareado, M1 Pro dividida com outros agentes, base `b0439b99` → entrega:
- `stress:500`: 70,9 / 72,0 / 72,3 → 115,5 / 115,2 / 116,4 FPS.
- `bench:500`: 71,0 / 71,4 / 71,0 (p95 14,96 / 15,04 / 15,16 ms) → 161,0 / 158,6 / 159,4 (p95 7,06 / 7,97 / 8,02).
- `benchbig:500`: 60,8 / 60,8 / 60,8 (p95 18,23 / 17,54 / 18,02) → 130,6 / 130,9 / 130,5 (p95 8,76 / 8,57 / 8,74).
- `benchrain:500` (só a entrega): 159,0 / 159,5 / 159,5 (p95 7,36 / 7,14 / 7,21): a chuva forte não custa nada medível.
- `benchrun:500` (mundo rodando): **24,1 / 25,5 / 24,7** (p95 54,07 / 50,10 / 53,73) → 54,8 / 51,2 / 50,5 (p95 30,30 / 25,04 /
  26,51); depois de a rua só redesenhar quando o chão muda: 54,5 / 50,9 / **48,6** (p95 29,71 / 25,47 / 27,47). No limite do piso.
Rodada final da entrega sozinha (mesmo comando): `stress` 113,2 / 113,5 / 111,5; `bench` 153,5 / 153,5 / 154,1; `benchbig` 127,9 /
127,7 / 127,4; `benchrain` 150,1 / 150,8 / 152,9. `client-test.sh` 381 → 394, 0 falhas.
O mundo pausado escondia duas coisas: o **minimapa refazia os 500 pontos a cada quadro** (a câmera "mudava" todo quadro; agora
só redesenha quando a câmera anda e os pontos no máximo 4×/s) e, com o mundo rodando, a **pose de 500 bonecos** (pausado, os
bonecos ficam congelados). A multidão agora refaz a pose a cada 2 quadros perto do centro da vista e a cada 4 na borda (o
passo é o mesmo; a posição continua todo quadro). O tempo só reconfigura as partículas quando o que cai ou o vento muda (o clima
chega a cada tick) e a malha de ruas não é refeita quando só a umidade (`wetPm`) da célula mudou (vinha a cada tick). Custo que
sobra com o mundo rodando, por quadro (`bench-cost`): aplicar o delta ~2–3,5 ms (NPCs ~1, clima/observável ~0,5), pose e passo dos
NPCs ~2,6, contrato ~0,9; o resto é o servidor a 40 ticks/s na mesma máquina. Abaixo de 50 numa rodada: issue (09b item 27).

### O motor diz, e o mundo rodando acima do piso (AD-189)

M1 Pro dividida com outros agentes, `bench.sh 3` pela fila pesada, API desta entrega (o clima já não volta a cada tick):
- `benchrun:500` base `09a321b7` (cópia do cliente): 52,6 / 50,7 / 48,7 FPS, p95 31,32 / 26,21 / 27,66 ms → entrega **63,6 / 63,4 /
  63,5**, p95 **21,22 / 19,91 / 21,85** (por etapa: contrato amostrado 54,8 / 52,0 / 51,6; + linha do tempo 54,0 / 55,2; + pose 58,1 / 60,7 / 60,7).
- Pausado, sem regressão (AD-188 → entrega): `stress` 113,2 / 113,5 / 111,5 → 111,0 / 113,2 / 113,7; `bench` 153,5 / 153,5 / 154,1 →
  156,9 / 157,3 / 157,3; `benchbig` 127,9 / 127,7 / 127,4 → 128,2 / 128,1 / 127,8; `benchrain` 150,1 / 150,8 / 152,9 → 153,5 / 156,5 / 155,4.
O que saiu do quadro: conferência do contrato só nos 20 primeiros deltas e depois 1 a cada 30 (0,9 → ~0,02 ms/quadro); a linha do
tempo reaproveita os 10 botões (eram recriados a cada delta com evento); a pose do parado a cada 8 quadros e a do passo a cada 3
(centro) ou 6 (borda) — com o mundo a 40 ticks/s quase todo boneco está no passo; os setters de LOD só quando mudam; a altura da rota
só é procurada quando a rota muda; a malha de processos não realoca; a transição das ruas a 20/s. `bench-cost` ganhou `npc_pose`,
`npc_links`, `events` e `processes`. `client-test.sh` 394 → 399.

