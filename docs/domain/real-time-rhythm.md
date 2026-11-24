# Ritmo do relógio, passo fino e pulo de tempo (ADR-0050, AD-197c)

Com jogador conectado o mundo roda em **tempo real** na escala do mundo; sem ninguém, no ritmo do mundo (padrão rápido, o do
admin). Os NPCs decidem, agem e andam dentro da hora quando o Δ é fino. Base: [time-in-milliseconds](time-in-milliseconds.md).

## Ritmo (hospedeiro) — `SimulationHost.Plan` / `Cycle`
Dados de cenário por mundo, `{"Players": {"Time": {...}}}` (`TimeRules`), sem número no motor:

| Campo | Padrão | Papel |
|---|---|---|
| `TimeScale` | 1 | mundo/real com jogador; Δ = `FineTickRealMs` × escala, no divisor da hora mais próximo (1:1 → 50 ms; 1:12 → 600 ms) |
| `IdleRhythm` | `Fast` | sem jogador: `Fast` (1 h por tick, na velocidade do admin) ou `RealTime` |
| `FineTickRealMs` | 50 | período real do tick fino (20 ticks/s) |
| `AcceleratedHoursPerSecond` | 8 | pulo de tempo: 1 h por tick a esta velocidade |
| `FineDecisions` | true | `LW_AD197C=off`: o Δ fino só anda o relógio (AD-197b) |
| `CriticalNeedDeficit`, `BurnTemperatureC`, `CallerAffection` | 80, 50 °C, 30 | o que acorda quem dorme acelerado |

Ordem do plano: Δ fixado pelo admin (`POST /simulation/tick-delta`; `0` volta ao automático) → acelerado (admin
`POST /simulation/accelerate`, ou o pulo de quem joga sozinho) → tempo real (há jogador) → `IdleRhythm`. Cada troca de Δ vai para
o registro do jogador (`TickDelta`), como na AD-197b. `/simulation/status` traz `rhythm`, `loopTicksPerSecond`, `timeScale`,
`playersPresent`, `accelerated`. No tempo real a projeção sai até ~5 vezes por segundo real e sempre na hora cheia.

## Motor no passo fino (`world.FineTime`: Δ < 1 h e `FineDecisions`)
- **Decisão dentro da hora**: `WorldClock` roda os sistemas `IRunsBetweenHours` (a `BehaviorDecisionSystem`) no tick fino para quem
  tem despertar vencido naquele instante (`PrepareWakeBatch` em ms; o balde guarda o piso do instante mais cedo e não é percorrido
  à toa). Os outros sistemas seguem na fronteira do calendário.
- **Ação com fase**: `Npc.ActionPhaseMs` (ms da hora em que começou; 0 e fora do JSON de hora em hora) e `ActionStartedAtMs`. Fim de
  ação e chegada de viagem comparam em ms (`Underway`), o que na hora cheia é a conta de antes.
- **Despertar em ms** (`NpcWakeScheduler.ComputeNextWakeMs`): fim de ação e chegada no instante exato, necessidade no instante em
  que cruza o limiar, virada da rotina na hora cheia, atrasado = +1 h (a cadência de antes).
- **Viagem e ação em ms** (AD-197h): `Mobility.TravelMs` (o caminho sem arredondar para a hora: um trajeto curto leva minutos) e
  `ObjectUse.DurationMs` decidem a conclusão e o despertar; de hora em hora seguem `TravelTicks`/`DurationHours`.
- **Rota e pose** (AD-197h): ao decidir, o viajante grava a perna (`Npc.TravelLegTo`, `TravelLegEndMs`, fora do JSON de hora em
  hora). A cada tick fino o relógio põe a pose de cada viajante (`TravelPose.Refresh`: fração do tempo ao longo da rota real,
  caminho guardado por par) no índice dos vivos, e `AliveNpcIndex.At` o conta na célula do caminho, não na origem: percepção,
  testemunha (`Justice`), encontro (crime, companhia, ensino) e o alcance do golpe o veem lá. `CurrentLocation` segue a origem.
  Quem já viajava quando o Δ passou ao passo fino (sem perna) ganha a perna no primeiro tick fino, do estado canônico (AD-197i).
  AD-197l: só os viajantes (o NPC avisa ao começar ou deixar de viajar, acompanhamento ligado só no passo fino) e só quando a
  pose troca de célula (o próximo instante de troca sai da perna); a pose é a mesma da conta direta, tick a tick.
- **Agendador**: evento que vence dentro da hora fica numa árvore por (instante, Id) da sua hora (O(log k)); o da hora cheia segue na
  lista. A fila de despertar guarda o piso do instante mais cedo fora da hora cheia de cada balde.
- **Tocado pelo jogador acorda já**: `Act` acorda o corpo conduzido e o golpe acorda o alvo no próximo tick fino.

## Pulo de tempo de quem joga sozinho (`PlayerService`, `SkipWatch`)
- `POST /players/{id}/sleep {untilHour?}` (até a hora do dia, ou acelerando até descansar: sono ≥ 95), `.../accelerate` (parado,
  à toa, necessidades caindo), `.../wake`, `GET .../time` (`PlayerTimeView`). Canal: `sleep`, `accelerate`, `wake`.
- Só com um jogador no mundo (`CanSkip`); o segundo que chega encerra o pulo ("outro jogador"). Qualquer comando do jogador
  também encerra. Pausa do admin: 409, como todo comando.
- **O que acorda** (estado real do motor, checado a cada ciclo): golpe (saúde menor e mágoa de agressão desde o começo, o
  rastro de `Violence.Fight`), fogo (temperatura do lugar ≥ 50 °C: o motor não tem incêndio, só clima e poder de temperatura),
  fome ou sede com déficit ≥ 80, alguém da casa ou próximo (afeição ≥ 30) conversando na mesma célula ou ao lado (chamando).

## Registro e replay
Novos tipos no `players.log`: `Advance` (`step` e `advance-year` do admin: `Ticks` × `DeltaMs`, gravado no instante em que
começa), `Sleep` (`UntilMs`, nulo = acelerar), `Accelerate`, `Wake` (`Reason`). `PlayerReplay` reaplica todos; o ciclo do
hospedeiro grava o fim do pulo e a troca de Δ antes de andar o tick.

## Hash
Com Δ = 1 h nada disto roda (`FineTime` falso, fase 0 fora do JSON, despertar em horas): os goldens ficam iguais. Com Δ fino o
mundo diverge por construção (despertar no instante do limiar, ação começando dentro da hora) e é reproduzível pela sequência de Δ.
