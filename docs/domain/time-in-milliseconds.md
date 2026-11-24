# Tempo em milissegundos e duração de tick variável (ADR-0050, AD-197b)

O tempo do mundo é guardado em **milissegundos inteiros** (decisão do dono, 2026-11-23: o tick fino de ~50 ms com jogador tem de
caber no motor). Cada tick avança **Δ** ms. Sem jogador (e hoje em produção) Δ = 3.600.000 (uma hora); o modo fino com jogador é a
AD-197c. Ver também [time-and-ticks.md](time-and-ticks.md).

## Regras do relógio
- `WorldDate.TotalMilliseconds` é o estado; `TotalHours` (piso, também antes da época), `Hour`, `Day`, `Month`, `Year` são
  derivados. A hora tem sempre 3.600.000 ms; o cenário só escolhe quantas horas tem o dia, o mês e o ano.
- `WorldClock.Tick(world, Δ)`: Δ **divide a hora** (1 a 3.600.000 ms; 50 ms divide; `WorldMillis.IsValidTickDelta`). O tick
  **nunca atravessa a hora cheia**: o que passaria dela para nela. Assim toda hora cheia é um tick, qualquer que seja Δ.
- Sistemas `Hourly`, `Daily`, `Midshift`, `Monthly`, `Yearly` rodam no tick que **chega** à fronteira (hora cheia, e dentro dela
  dia, meio de turno, mês, ano), uma vez por fronteira, nunca "a cada tick". Fora da hora cheia o tick só anda o relógio e despacha
  os eventos vencidos.
- `Run(world, n)` continua sendo n horas; `Run(world, n, Δ)` roda n ticks de Δ ms.

## O que já é milissegundo
| Peça | Estado em ms | API em horas (sistemas de fronteira) | API em ms |
|---|---|---|---|
| Relógio | `WorldDate.TotalMilliseconds` | `TotalHours`, `ctx.CurrentTick` | `ctx.CurrentMs`, `AddMilliseconds` |
| Agendador | `ScheduledEvent.TargetMs` | `ctx.ScheduleEvent(hora, …)`, `TargetTick` | `ctx.ScheduleEventAtMs` |
| Despertar | `PendingNpcWake.TargetMs` | `ReplaceNpcWake(npc, hora)` | `ReplaceNpcWakeAtMs` |
| Necessidade | `LazyNeed.MsOfLastEvent`, taxa `DecayRatePerHour` | `ValueAt(hora)`, `TicksAtZero(hora)` | `ValueAtMs`, `WithValueAtMs` |
| Registro do jogador | `PlayerCommandRecord.AtMs`, kind `TickDelta` (`DeltaMs`) | — | `PlayerService.SetTickDelta` |

- **Agendador e despertar**: baldes pela hora de fecho (`(h-1)·hora < ms ≤ h·hora`); um tick toma tudo da sua hora com instante ≤
  agora, em ordem de (instante, id) nos eventos e de sequência decrescente nos despertares. Quem vence mais tarde na mesma hora
  fica na fila. Com Δ = 1 h o balde é o tick inteiro, como antes.
- **Necessidade**: horas decorridas = ms / 3.600.000; com âncora na hora cheia a conta é a de antes, em horas, byte a byte.
  `TicksAtZero` conta horas inteiras depois da âncora, como sempre.

## Save, hash e contrato
- **Save e hash canônico**: os campos acima mantêm nome e valor em horas quando caem na hora cheia (`WorldDateJsonConverter`,
  `HourCompatibleMillisJsonConverter`); dentro da hora a data ganha `MillisecondOfHour` e os instantes viram o texto `"<ms>ms"`.
  Mundo que só andou de hora em hora tem os mesmos bytes e o mesmo hash de antes. **Save antigo carrega convertido**: horas, e a
  forma em segundos que existiu antes da troca para ms (`SecondOfHour`, `"<s>s"`). Arquivo frio: formato 3 (ms), lê 1 (horas) e
  2 (segundos). `players.log` antigo (horas, `"<s>s"`, `DeltaSeconds`) carrega convertido.
- **Contrato v2** (`/openapi/v2.json`; o v1 não é mantido): todo campo em tick sai em ms com o nome trocado, sem campo irmão
  (`WireMillis`, HTTP, WS e canal do jogador): `tick` → `atMs`; instante `…AtTick`/`…Tick` → `…AtMs` (`actionStartedAtMs`,
  `routeArrivalAtMs`, `startedAtMs`, `distortedAtMs`); duração `…Ticks` → `…Ms` (`maxMs`, `pauseMaxMs`). O que o motor ainda
  guarda em horas sai como hora × 3.600.000. Parâmetros `fromMs`/`sinceMs`; entrada em ms arredonda para a hora de cima.
  `/simulation/status` traz `atMs` (o relógio em ms) e `tickDeltaMs`. `POST /simulation/tick-delta {deltaMs}` (admin) troca Δ
  pela fila e pelo registro do jogador. `ticksPerSecond` segue sendo a velocidade do laço.
- **Cliente Godot**: lê os ms na borda (`Contract/world_time.gd`) e conta em horas do mundo como antes.

## Determinismo e replay
Mesma seed + mesmas entradas + **mesma sequência de Δ** = mesmo mundo. A troca de Δ vai para o `PlayerSessionLog` (`TickDelta`, no
instante) e o `players.log`; base nova com Δ ≠ 1 h começa com ele (sequência -1). `PlayerReplay` roda cada trecho com o Δ em vigor,
parando nas horas cheias como o hospedeiro. Avanço do admin (`step`, `advance-year`) ainda não entra no registro: com o motor desta
fatia isso não muda o mundo (ver abaixo); na AD-197c tem de entrar.

## Δ menor: onde o mundo é o mesmo e onde deixará de ser
Provado (`TickDeltaEquivalenceTests`): o mesmo mundo sem entradas, em ticks de 1 h ou de 1 min, 15 min e 50 ms, chega ao **mesmo
hash canônico**. Não é truque: tudo que decide ainda conta em horas e só roda na hora cheia, e o que já é ms dá a mesma conta na hora
cheia. Deixará de ser igual quando a decisão ficar fina (AD-197c), por construção:
1. **Despertar com granulação diferente**: a necessidade cruzando o limiar às 3h24 acorda o NPC às 3h24 com Δ fino e às 4h com
   Δ = 1 h; a escolha é feita com outras necessidades e outro estado de quem está em volta.
2. **RNG por chave de tempo**: sorteios com chave de tick/hora (`OneShot`, `StreamFor`) mudam de chave se a decisão acontece em
   outro instante.
3. **Ordem entre evento e sistema horário**: evento agendado dentro da hora dispara antes dos sistemas daquela hora.
4. **Ação e rota em horas inteiras**: `ActionStartedAtTick`, `DurationHours`, `TravelTicks` ainda são horas; o fim de ação e a
   chegada caem na hora cheia com qualquer Δ (igual por construção, mas grosso).

## Ainda em horas (fatia seguinte)
Decisão (`BehaviorDecisionSystem` é `Hourly`), início e duração de ação, pernas de rota e chegada do motor, campos de tick dos
sistemas de cidade, obra, lavoura, saúde, crime, história (`WorldEvent.Tick`), `ctx.CurrentTick` em geral. São da escala de hora e só
rodam na hora cheia, então estão certos com qualquer Δ; passam a ms quando um consumidor fino precisar (AD-197c).
