# ADR-0050: mundo em tempo real com duração de tick variável

- **Status**: aceito (dono, 2026-11-23: opção (b); padrão sem jogador rápido no ritmo do admin; contrato em versão nova).
  AD-197b fez a unidade em milissegundos, o Δ variável no relógio, o contrato v2 e o cliente ([time-in-milliseconds](../domain/time-in-milliseconds.md));
  AD-197c fez o modo fino com jogador, o ritmo por mundo e o pulo de tempo ([real-time-rhythm](../domain/real-time-rhythm.md)).
- **Data**: 2026-11-23
- **Decisores**: dono do projeto; rascunho por agente

## Contexto
Hoje 1 tick = 1 hora de mundo (`WorldClock.Tick` faz `AddHours(1)`; `WorldDate` é `TotalHours`). O dono decidiu: com jogador o
mundo inteiro roda em tempo real ("1 tick não será mais 1 hora"); avançar horas só o admin, ou o jogador quando é o único no
mundo (ocioso, vendo o mundo e perdendo necessidades, ou ao dormir: até um horário ou só acelerando). Respostas de 2026-11-23:
a proporção tempo de mundo / tempo real é **configurável por mundo, padrão 1:1**; o ritmo sem nenhum jogador também é
configurável. Rejeitado: o passo fino só do jogador dentro do tick de 1 h.

**Inventário medido** (busca em `src/` sobre `48315796`, 2026-11-23): 238 usos de `TotalHours` como tick, 260 de
`CurrentTick`, 279 de `HoursPerDay/Month/Year` (conversão central pelo calendário, a parte boa), 117 taxas `PerHour/PerTick`
(23 em `Domain/Population`, 20 em `Behavior`), 482 nomes em horas (`DurationHours`, `MaxDurationHours`, pernas de rota), 74
sistemas registrados por frequência (Hourly 19, Daily 20, Monthly 20, Yearly 7, Midshift), cerca de 15 literais `24` em código
de motor (`CrimeSystem` 17h/24, `CropSystem` 24 ticks, `WorldClock` meio-turno, relatórios de `Observation`). Contrato: 18
campos com tick (`tick` ×25, `actionStartedAtTick`, `routeArrivalTick`...). Cliente: 197 linhas com tick (interpolação por
`routeArrivalTick`, relógio). 19 goldens e baselines. As necessidades já são preguiçosas (`LazyNeed`: valor = taxa × tempo
decorrido) e a decisão é por despertar agendado (`NpcWakeScheduler`), não varredura por tick: o motor já pensa em tempo.

## Opções
- **(a) Tick fixo mais curto para todo o mundo** (ex.: 1 minuto). Simples de explicar; mas 100 anos sem jogador viram 52,6
  milhões de ticks (60×). Sistemas horários a cada minuto multiplicam o custo por 60; mantidos na virada da hora, cada tick
  vazio ainda paga despertar, agendador e projeção. Todos os goldens mudam. Rejeitada como padrão.
- **(b) Duração de tick variável por modo, recomendada.** O tempo de mundo passa a ser inteiro em **segundos**
  (`WorldDate.TotalSeconds`; `TotalHours` derivado). Cada tick avança Δ segundos: 3.600 sem jogador ou ao avançar o tempo;
  fino (ex.: Δ = 50 ms de mundo × proporção) com jogador presente. Sistemas horários, diários e mensais rodam **ao cruzar** a
  fronteira do calendário (como hoje com Δ = 1 h); taxas escritas por tempo de mundo (a `LazyNeed` já é); ações e rotas com
  duração em segundos; agendador e despertares em segundos. Com Δ = 3.600 a conta é a de hoje, byte a byte: os goldens não
  mudam se a migração for feita por etapas, cada uma provada por "Δ = 1 h reproduz o hash anterior".
- **(c) Duas camadas** (mundo em horas e uma "cena" fina em volta do jogador). É o passo fino rejeitado com outro nome.

## Decisão: (b), aprovada pelo dono
1. **Determinismo**: mesma seed + mesmas entradas + mesma sequência de Δ = mesmo mundo. O `PlayerSessionLog` (ADR-0048) passa a
   gravar também as trocas de modo (Δ a partir do tick T) e cada aceleração; o replay reaplica tudo.
2. **NPC em tempo fino**: a decisão segue por despertar (agora em segundos), não por subtick; movimento progride pela rota com
   instante de chegada (a pose entre células é calculada pelo tempo, não guardada); ação conclui no instante certo. Quem é
   tocado pelo jogador (golpe, conversa, comércio) é despertado na hora pelo evento.
3. **Ritmo do relógio por mundo**: `TimeScale` (mundo/real, padrão 1:1) com jogador; `IdleRhythm` sem jogador (padrão
   aprovado: rápido, no ritmo do admin, como hoje; configurável por mundo, inclusive tempo real). Admin sempre avança e acelera. Jogador só avança
   quando é o único conectado: ocioso (acelera vendo o mundo, as necessidades caem) ou dormindo (até um horário ou acelerar),
   interrompido por evento que o acorda (golpe, fogo, fome crítica). Com dois ou mais jogadores ninguém pula o tempo.
4. **Custo**: com jogador a 1:1, uma hora de mundo leva uma hora real; os sistemas horários rodam uma vez por hora real (muito
   mais barato por segundo real que hoje, 1 h por segundo). O tick fino só paga despertares vencidos, entradas do jogador e o
   passo do jogador; meta ≤1,02× por NPC-tick em Δ = 1 h e FPS sem regressão.
5. **Escala em números (1:1)**: um dia de jogo = 24 h reais; dormir 8 h reais (ou pular, se sozinho); ir à cidade vizinha
   horas reais; uma casa leva semanas reais. Mundo com 1:12 (dia de 2 h reais): sono de 40 min, obra de dias. O dono escolhe
   por mundo.

## Consequências
- **Positivas**: o mundo do jogador é contínuo e realista; o mundo sem jogador segue rápido e barato; o replay continua
  determinístico; a migração em etapas preserva os goldens.
- **Negativas**: migração ampla (unidade de tempo em ~1.400 pontos, contrato em **versão nova**: os campos em tick mudam de unidade
  para segundos, sem campos irmãos; o cliente migra junto), cliente reinterpreta relógio e interpolação; saves antigos convertidos (horas → segundos).
- **Follow-ups**: AD-197b (unidade de tempo em segundos, etapa a etapa com prova de hash), AD-197c (modo jogador com Δ fino e
  ritmo por mundo), ADR-0049 (controle direto sobre este relógio).

## Decisões do dono (2026-11-23)
1. Aprovada a opção (b): tempo de mundo em segundos inteiros, Δ variável por modo.
2. Sem jogador: rápido, no ritmo do admin; configurável por mundo.
3. Contrato: versão nova (`openapi.v2`), campos em tick passam a segundos na versão nova; sem campos irmãos. O cliente Godot
   migra na mesma fatia; a versão antiga não é mantida em paralelo.
4. **Milissegundos em vez de segundos** (2026-11-23, durante a AD-197b): o tempo de mundo é guardado em **milissegundos inteiros**,
   para o tick fino de ~50 ms com jogador caber no motor (escolhido contra "segundo inteiro como mínimo"). Onde este ADR diz
   segundos (`TotalSeconds`, Δ em segundos, campos do contrato em segundos), vale ms: `WorldDate.TotalMilliseconds`, Δ em ms
   (divide 3.600.000), contrato v2 com `atMs`, `…AtMs`, `…Ms`, `tickDeltaMs`.
