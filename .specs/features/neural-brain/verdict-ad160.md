# Cérebro neural — situação e veredito (AD-160, 2026-11-16)

> Irmão de [design.md](design.md) (a proposta) e [cost-estimates.md](cost-estimates.md) (contas antes de medir).
> Pedido: `.specs/features/realism-backlog/03-cognicao-e-cerebro.md` (parte 2). Medições em 6 seeds × 50 anos (regra de
> "medição curta"), base `3a878f5c`. Veredito: **não ligar**; o trabalho que falta virou texto de issue (relatório do AD-160).

## Os três números que barraram em 2026-11-13 (AD-142) e os de hoje
| | AD-142 (2026-11-13) | Hoje, como juntado | Hoje, depois do AD-160 |
|---|---|---|---|
| CPU por NPC-tick, ligado × desligado | +0,6% (1.000), **+7,0% (5.000)** | +4,4% e +5,2% (1.000), +21% (5.000)¹ | +7,0% (1.000); mecanismo só: +4,5%¹ |
| Bytes: snapshot JSON por vivo / positional por NPC-ano | **+3,3% / +6,8%** | +3,2% / **+12,7%** | +3,1% / **+9,1%** |
| Demografia / variedade de dias | **pop 149,3 → 140,9; sede 29 → 103**; 0,796 → 0,773 | pop 222,7 → 207,7 (−6,7%); sede 1 → 8 mortes; 0,733 → 0,716 | pop 227,2 (+2,0%); sede 0; fome 12 → 38 mortes; 0,707² |

¹ Mediana de 5 rodadas pareadas a 1.000 NPCs (a 5.000 o ruído da máquina compartilhada passou de ±10%: o mesmo código
desligado mediu 0,889 e 0,998 µs). "Mecanismo só" = ganhos presos em zero (`frozen`): mesmo mundo, só o custo de ter cérebro. ² Variedade de dias com 6 seeds oscila ±0,03 entre corridas quase
iguais (0,740 e 0,707 para código que só difere no arredondamento da participação): não sobe, não cai de forma detectável.

## O que impede ligar, item a item
1. **Cérebro em si (avaliação por decisão).** Utilidade pura 226 ns; com ganhos 262 ns (+16%); com ganhos e participação
   guardada 311 ns (+38%) (`BrainDecisionCostHarness`, 3 corridas). Depois do AD-160 (só a vencedora recontada): 229 → 252 ns (+10%) e 297 ns (+30%). Só decisões
   urgentes passam pelo cérebro (~0,24 por NPC-hora na vila): a conta explica ~+1 a +2% do motor (0,85 µs/NPC-tick).
2. **Mecanismo por NPC (fora a conta).** Com ganhos presos em zero o mundo é o mesmo e o motor custa **+4,5%** (1.000 NPCs):
   o resto é guardar a participação a cada decisão e sentir o estado duas vezes (necessidades, humor, despensa) a cada ação
   concluída para medir o alívio. Não fechado.
3. **Estado por NPC.** 80 ganhos + 10 expectativas + a participação guardada. Mecanismo ligado com ganhos presos em zero
   (`frozen`, mundo idêntico em 4 de 6 seeds): **+1,9% bytes por NPC-ano**, +2,0% JSON por vivo. O que passa disso (+7%) é o
   ganho mudando a cada ação concluída: cada aprendizado reescreve folhas do snapshot.
4. **Efeito no mundo** (sonda `brain-trace`: escolha com ganhos × utilidade pura no mesmo estado, 2 seeds × 30 anos):
   - O cérebro troca 8% das decisões urgentes na 1ª década e 15–17% na 3ª.
   - **Como juntado (AD-142)**: troca 10–20% das decisões com fome ou sede crítica, 4.000 a 25.000 por década para longe de
     comer ou comprar → sede 1 → 8 mortes. E come mais: Idle→Eat, Work→Eat, Socialize→Eat são as trocas mais comuns; consumo
     por pessoa 300 → 318/ano; furto de comida por fome 1,18 → 3,19 por seed-ano.
   - Causa do comer a mais: a expectativa de cada ação começava em zero, então toda ação que alivia (comer, dormir) parecia
     melhor do que o esperado nas primeiras vezes e o ganho de Comer subia em todo NPC (de +12 a +258‰ no trace).
   - **Depois do AD-160**: 0 decisões críticas trocadas, consumo 297/ano, sede 0. Fica: o NPC **compra menos** (0,38 → 0,36
     h/dia por adulto), **furta mais por fome** (1,18 → 3,0 por seed-ano) e **morre mais de fome** (12 → 38 mortes em 6 seeds × 50
     anos, 15 por década nas duas últimas: cresce com o tempo, como os ganhos). A expectativa nasce da 1ª vez, que é atípica (a 1ª
     compra é com a despensa vazia): comprar decepciona depois. O trace mostra o viés invertido: agora as trocas mais comuns
     saem de Comer (Eat→Idle, Eat→Socialize, Eat→Travel, Eat→Work), 6–7% das decisões urgentes na 1ª década e 13–16% na 3ª. E a recompensa é só o alívio imediato: o valor adiado de comprar
     (comida para dias) e de trabalhar (salário) não chega ao cérebro.
   - 3ª tentativa (variante `body40`, corpo decide com fome ou sede ≤ 40): fome 10 mortes, 1q0 199‰, e0 33,2 (= base), mas
     compra ainda menos (0,30 h/dia) e furta 6,1 por seed-ano por fome (×5). Não adotada: esconde o erro sem corrigi-lo.

## O que o AD-160 fechou (cada um com teste)
- **O corpo decide na necessidade crítica** (`BrainRules.CriticalNeed` = 20, o mesmo déficit 80 que já põe o corpo à frente do
  lazer): fome ou sede ≤ 20 decidem pela utilidade pura; o cérebro pesa o resto e continua aprendendo.
- **A 1ª experiência só forma a expectativa** (`FirstExperienceSetsExpectation`): sem expectativa, não há surpresa.
- **Participação guardada num número** (um byte com sinal por grupo) e zerada ao esquecer: −4 pontos em bytes por NPC-ano.
- **Fator do grupo só lido quando há rastro**, sem `Array.Clear/Copy` por ação, e só a vencedora é recontada com os grupos
  (a escolha e o mundo não mudam: a conta é a mesma função).
- Tentativas que não fecharam: ganhos em base64 no snapshot (+0,7% pior: ganhos pequenos são curtos em lista); lente que soma
  por grupo e aplica o ganho uma vez (+35% por decisão contra +16%: oito multiplicações por ação custam mais que o que poupam).
- Sonda `brain-trace` (Workers) e variantes `frozen`/`ad142` em `MeasureHarness` e `BrainExperimentHarness`.

## Por que não ligar
Critério do dono: custo ≤ ~2%, 0 extinções, população ±10%, variedade igual ou maior. Passa: 0 extinções, população (+2%),
variedade (igual dentro do ruído). Não passa: custo (+4,5% só o mecanismo, +7% ligado, a 1.000 NPCs), bytes por NPC-ano +9,1%,
e a vila piora onde o cérebro mexe (compra menos, furto por fome ×2,5, mortes de fome ×3). E o motivo de fundo: a recompensa é só o alívio
imediato, então o cérebro não aprende o valor adiado de trabalhar ou comprar; e ele só pesa decisões urgentes (a rotina, o grosso
do dia, não passa por ele), então não pode aumentar a variedade dos dias. Como "modo de cenário" também não: não é bom e caro, é
caro e ainda erra.

## Como ligar para ver
`{"Brain":{"Enabled":true}}` no cenário. `LW_BRAIN=1` no `demography-report`; `LW_BRAIN_VARIANT=frozen|ad142|body`;
`dotnet <dll> brain-trace <seed> <seeds> <anos>`; `LW_MEASURE_BRAIN=1|frozen|ad142` no `MeasureHarness`.
