# Tempo em segundos e duração de tick variável (ADR-0050, AD-197b)

O tempo do mundo é guardado em **segundos inteiros**. Cada tick avança **Δ** segundos. Sem jogador (e hoje em produção) Δ = 3.600
(uma hora); o modo fino com jogador presente é a AD-197c. Ver também [time-and-ticks.md](time-and-ticks.md).

## Regras do relógio
- `WorldDate.TotalSeconds` é o estado; `TotalHours` (piso, também antes da época), `Hour`, `Day`, `Month`, `Year` são derivados.
  A hora tem sempre 3.600 s; o cenário só escolhe quantas horas tem o dia, o mês e o ano.
- `WorldClock.Tick(world, Δ)`: Δ **divide a hora** (1 a 3.600 s; `WorldSeconds.IsValidTickDelta`). O tick **nunca atravessa a hora
  cheia**: o que passaria dela para nela. Assim toda hora cheia é um tick, qualquer que seja Δ.
- Sistemas `Hourly`, `Daily`, `Midshift`, `Monthly`, `Yearly` rodam no tick que **chega** à fronteira (hora cheia, e dentro dela
  dia, meio de turno, mês, ano), uma vez por fronteira, nunca "a cada tick". Fora da hora cheia o tick só anda o relógio e despacha
  os eventos vencidos.
- `Run(world, n)` continua sendo n horas; `Run(world, n, Δ)` roda n ticks de Δ.

## O que já é segundo
| Peça | Estado em segundos | API em horas (sistemas de fronteira) | API em segundos |
|---|---|---|---|
| Relógio | `WorldDate.TotalSeconds` | `TotalHours`, `ctx.CurrentTick` | `ctx.CurrentSecond`, `AddSeconds` |
| Agendador | `ScheduledEvent.TargetSecond` | `ctx.ScheduleEvent(hora, …)`, `TargetTick` | `ctx.ScheduleEventAtSecond` |
| Despertar | `PendingNpcWake.TargetSecond` | `ReplaceNpcWake(npc, hora)` | `ReplaceNpcWakeAtSecond` |
| Necessidade | `LazyNeed.SecondOfLastEvent`, taxa `DecayRatePerHour` | `ValueAt(hora)`, `TicksAtZero(hora)` | `ValueAtSecond`, `WithValueAtSecond` |
| Registro do jogador | `PlayerCommandRecord.Second`, kind `TickDelta` | — | `PlayerService.SetTickDelta` |

- **Agendador e despertar**: baldes pela hora de fecho (`(h-1)·3600 < s ≤ h·3600`); um tick toma tudo da sua hora com instante ≤
  agora, em ordem de (instante, id) nos eventos e de sequência decrescente nos despertares. Quem vence mais tarde na mesma hora
  fica na fila. Com Δ = 1 h o balde é o tick inteiro, como antes.
- **Necessidade**: horas decorridas = segundos / 3.600, com a conta inteira quando cai na hora cheia (o número de antes, byte a byte).
  `TicksAtZero` conta horas inteiras depois da âncora, como sempre.

## Save, hash e contrato
- **Save e hash canônico**: os campos acima mantêm nome e valor em horas quando caem na hora cheia (`WorldDateJsonConverter`,
  `HourCompatibleSecondsJsonConverter`); dentro da hora a data ganha `SecondOfHour` e os instantes viram o texto `"<s>s"`. Mundo
  que só andou de hora em hora tem os mesmos bytes e o mesmo hash de antes; **save antigo carrega convertido** (horas × 3.600).
  O arquivo frio do arquivo de mortos passou ao formato 2 (segundos) e ainda lê o 1 (horas). `players.log` antigo carrega igual.
- **Contrato v2** (`/openapi/v2.json`; o v1 não é mantido): todo campo em tick sai em segundos, com o nome trocado (`tick` →
  `second`, `…Tick` → `…Second`, `…Ticks` → `…Seconds`), sem campo irmão (`WireSeconds`, HTTP, WS e canal do jogador). O que o
  motor ainda guarda em horas sai como hora × 3.600. Parâmetros `fromSecond`/`sinceSecond`; entrada em segundos arredonda para a
  hora de cima. `/simulation/status` traz `second` (o relógio em segundos) e `tickDeltaSeconds`. `POST /simulation/tick-delta`
  (admin) troca Δ pela fila e pelo registro do jogador. `ticksPerSecond` segue sendo a velocidade do laço.
- **Cliente Godot**: lê os segundos na borda (`Contract/world_time.gd`) e conta em horas do mundo como antes (relógio do dia,
  rota, processos).

## Determinismo e replay
Mesma seed + mesmas entradas + **mesma sequência de Δ** = mesmo mundo. A troca de Δ vai para o `PlayerSessionLog` (`TickDelta`, no
instante) e o `players.log`; base nova com Δ ≠ 1 h começa com ele (sequência -1). `PlayerReplay` roda cada trecho com o Δ em vigor,
parando nas horas cheias como o hospedeiro. Avanço do admin (`step`, `advance-year`) ainda não entra no registro: com o motor desta
fatia isso não muda o mundo (ver abaixo), na AD-197c tem de entrar.

## Δ menor: onde o mundo é o mesmo e onde deixará de ser
Provado (`TickDeltaEquivalenceTests`): o mesmo mundo sem entradas, 48 h em ticks de 1 h ou de 1 min/15 min, chega ao **mesmo hash
canônico**. Não é truque: tudo que decide ainda conta em horas e só roda na hora cheia (sistemas de fronteira, decisão por
despertar, eventos agendados em hora cheia), e o que já é segundo dá a mesma conta na hora cheia. Deixará de ser igual quando a
decisão ficar fina (AD-197c), por construção e não por erro:
1. **Despertar com granulação diferente**: a necessidade cruzando o limiar às 3h24 acorda o NPC às 3h24 com Δ = 1 min e às 4h com
   Δ = 1 h; a escolha é feita com outras necessidades e outro estado de quem está em volta.
2. **RNG por chave de tempo**: sorteios com chave de tick/hora (`OneShot`, `StreamFor`) mudam de chave se a decisão acontece em
   outro instante; a sequência consumida muda.
3. **Ordem entre evento e sistema horário**: evento agendado dentro da hora dispara antes dos sistemas daquela hora.
4. **Ação e rota em horas inteiras**: hoje `ActionStartedAtTick`, durações (`DurationHours`) e pernas de rota (`TravelTicks`) são
   horas; enquanto forem, o fim de ação e a chegada caem na hora cheia com qualquer Δ (igual por construção, mas grosso).

## Ainda em horas (fatia seguinte)
Decisão (`BehaviorDecisionSystem` é `Hourly`), `ActionStartedAtTick` e durações de ação, pernas de rota e `routeArrivalTick` do
motor, campos de tick dos sistemas de cidade, obra, lavoura, saúde, crime, história (`WorldEvent.Tick`), `ctx.CurrentTick` em
geral. São da escala de hora e só rodam na hora cheia, então estão certos com qualquer Δ; passam a segundos quando um consumidor
fino precisar (AD-197c).
