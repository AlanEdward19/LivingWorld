# Cérebro neural — ligado por padrão (AD-160b, 2026-11-16)

> Continuação de [verdict-ad160.md](verdict-ad160.md) (o "não ligar" e o porquê). O dono pediu insistir: "não pode sair
> pela metade". Base: `lote/cerebro-ad160` + `perf/decision-scan-costs` (2c3a41ca). Mecanismo em [brain](../../../docs/domain/brain.md).

## O que mudou no cérebro (cada item com teste)
1. **Recompensa = variação do valor do estado** (`BrainValue`): corpo (4 necessidades) + despensa (20 pontos por dia de
   comida e água por morador, até 4 dias) + moeda (10 por dia de comida que ela compra ao preço de referência) + humor
   (100 × valência). Trabalho soma o salário que a jornada rende (salário ÷ 270 h × horas). Medida uma vez, no fim.
2. **O impulso do corpo não aprende** (grupo Base preso em 0): o que se aprende é quanto pesam temperamento, contexto,
   humor, pressão, antevisão e hábito. Com fome ou sede urgente (≤ 30) o corpo decide pela utilidade pura.
3. **Expectativa**: o filho herda a média do que os pais esperam; a 1ª vez só forma a expectativa; anda em degraus de 16.
4. **Rotina pelo cérebro** (tempo livre): na faixa de conversar/ficar à toa, escolhe entre conversar, deitar mais cedo
   (2 h antes da cama) e ficar além do turno, com a forma da utilidade (necessidade × temperamento + hábito, cada grupo
   com o ganho do NPC). Só no começo da faixa; com fome ou sede < 50 é a rotina. Não troca conversar por ficar à toa.
5. **Custo e bytes**: lente pulada quando os ganhos não pesariam; só a vencedora é recontada; escolha guardada num número
   (uma folha); ganhos em degraus de 20 com sorteio sem viés (stream do mundo por NPC e tick); sem ruído de herança.
6. **Leitura**: `GET /npcs/{id}/brain` (sinapses termo → grupo → ação, notas, valor do estado por parcela) e a linha
   "Vale para ele agora… / Aprendeu a pesar…" no painel Cérebro do cliente.

## Medidas finais (contra o mesmo commit desligado)
| Critério | Medido | Passa? |
|---|---|---|
| Fome/sede crítica por pessoa-ano (40 × 15, pareado) | fome −0,8 ± 0,5; sede −7,6 ± 2,5 h (melhor) | sim |
| Furto por fome / horas de compra | 4,12 → 3,75 por seed-ano / 0,39 → 0,40 h | sim |
| População, extinções | +1,8% (40 × 15), +1,3% (40 × 20); 0 extinções | sim (curto) |
| Variedade de dias no mesmo ofício (40 × 20) | 0,754 → 0,783 (+3,9% ± 1,1%, 3,5 EP) | sim |
| Bytes por NPC-ano | +2,1% ± 1,2 entre mundos; **3,11% ± 0,04 no mesmo mundo** | no limite |
| Custo a 1.000 NPCs (30 pares) | **+5,5%** (IQR 0,0% a +6,4%); alocação −0,4% | **não** |
| `brain-trace` | 0 decisões críticas trocadas; 1–2% trocadas, sem ação dominante | sim |
| Leitura soma a nota / desligado = mundo igual | testes passam | sim |

20 seeds × 100 anos (topo final): não rodado nesta fatia.

## Trocas encontradas (para o dono decidir)
- **Variedade × sede longe de casa**: escolher entre conversar e ficar à toa sobe a variedade, mas ficar à toa anda um
  passo a cada 2 h (conversar, a cada 3 h) e leva para longe da água: +34 h de sede crítica longe de casa por pessoa-ano.
  Piso de 85 zerava a sede e a variedade (+0,6%); proibir a troca por "à toa" mantém variedade (+3,9%) e a sede melhora.
- **Aprendizado × bytes**: sem aprender, o cérebro custa 1,75% de bytes; aprendendo a cada ação, 4,1%. Degraus maiores e
  aprendizado 0,05 chegam a 3,1%; menos que isso pede aprender ainda menos.
- **Custo**: a medida de relógio na máquina compartilhada oscila ±5%; a mediana de 30 pares diz +5,5%. Parte é o mundo
  diferente (mais decisões no tempo livre), parte o mecanismo (lente, valor do estado). Não fechou em 2%.

## Pins que quebram com o cérebro ligado
`DataOnlyPowerTests.Default_scenario_hash_with_the_power_system_off_matches_the_committed_golden`,
`OptimizationEquivalenceTests.Default_scenario_ninety_days_keeps_the_recorded_hashes` (seeds 1 e 2) e o contrato
`openapi.v1.json` (rota nova). Regravar na junção (`refresh-world-hashes.sh`, `LW_RECORD_CONTRACT=1`).
