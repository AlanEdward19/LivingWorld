# Economia: empréstimo, capital de giro e falência (fatias 6 e 7, AD-155)

> Parte de [04](04-economia-e-empreendedorismo.md). Fatias anteriores e sobras E1 a E21 em [04a](04a-economia-fatias-1-a-5.md).
> Domínio: [credit](../../../docs/domain/credit.md). Números sem fonte: [08b](08b-a-investigar.md), itens 90 a 96.

## Estado (2026-11-15): fatias 6 e 7 feitas, ligadas no cenário de código

Crédito em moeda que existe (`CreditRules`, `Lending`): parente, conhecido, o rico da vila e o tesouro (estes dois só com a
oficina em garantia), decidindo pela moeda acima da reserva, confiança, parentesco, favor devido (eixo `Debt`), garantia,
calote recente e má fama. Usura disfarçada (10% ao ano do prazo, multa de 5%, teto de 20%; parente a 0%). Finalidades:
abrir, reabrir ou comprar a oficina fechada, capital de giro e comida no aperto. Parcela, atraso, calote com penhora, fama de
mau pagador, confiança destruída e mágoa "divida" (V2 do backlog 06). Oficina de dono que não fecha a folha: o dono põe do
bolso, toma giro ou demite os últimos; 6 meses sem fechar, **falência de verdade** (E19): estoque vendido, credores na ordem,
prédio ao credor, dívida que fica com dono e sócios pelas cotas. Dívida e crédito no espólio. E20: o dono não larga a oficina.

20 seeds × 100 anos contra a base `3a469e0d` (a mesma medida do AD-154, `demography-report 1 20 100 10`):

| Medida | Base | AD-155 |
|---|---|---|
| Extintas; pop final (mín) | 0; 286,4 (201) | 0; 288,7 (209) |
| Fome; sede (mortes) | 309 (2,4%); 5 | 294 (2,3%); 2 |
| e0; 1q0 coorte | 31,2; 203‰ | 31,3; 206‰ |
| Gini adultos / casas, ano 100 | 0,866 / 0,811 | 0,887 / 0,838 |
| Moeda na cidade, ano 100 | 58,7% | 58,7% |
| Negócios novos (reabertos); vivos aos 5 anos | 47 (32); 26/45 | 24 (0); 20/22 |
| Empréstimos (seeds com algum) | — | 16.606 (20/20): comida 16.562, giro 44, abertura 0 |
| Calotes / encerrados; perdido | — | 228 (1,3%); 13.000 (espólio 109, partiu 103, penhora 16) |
| Falências; demitidos por caixa | — | 5 (4 liberados); 252: um ano depois 56 trabalhando, 166 sem, 20 partiram/morreram |
| Furtos; homicídios por seed-ano | 11,5; 0,07 | 13,0; 0,06 |
| Moeda total | — | conservada (desvio 0) |

Custo a 1.000 NPCs, pares alternados (`profile.sh`): base 2,07/2,66 µs, 190/204 B; AD-155 2,55/2,06 µs, 203/191 B por
NPC-tick: dentro do ruído (médias 2,36 → 2,31 µs; 197 → 197 B). Os sistemas novos são mensais e ficam abaixo de 7 ms.

## Ficou de fora das fatias 6 e 7 (nada some)

- E22. **Empréstimo de abertura não acontece** (0 em 20 seeds): o parente com dinheiro entra antes como sócio, e a falta que
  sobra raramente cabe na metade do capital com parcela dentro do ganho prometido. Conferir com a prática (*commenda*, crédito
  de mestre a oficial) antes de afrouxar.
- E23. **Calote baixo (1,3%)** e quase todo por morte ou partida; a penhora é rara porque a parcela de comida cabe no salário.
  Sem número de referência firme (`08b` item 90).
- E24. **Quem é demitido por falta de caixa fica sem trabalho** (166 de 242 um ano depois): o desemprego estrutural da vila
  (jovens sem trabalho, `08` itens 48 a 61) não absorve. Medido, não tratado.
- E25. **E20 reduziu as aberturas** (47 → 24) e acabou com as reaberturas (32 → 0): antes o mestre largava a forja por
  emprego melhor e outro abria. Mais realista; a vila segue com ferraria viva em 20/20.
- E26. **Gini sobe ~0,02** e a moeda segue indo ao tesouro da cidade (58,7% no ano 100): o empréstimo de comida faz a moeda
  dos que emprestam ir à fazenda comum e daí à cidade. Destino da moeda da cidade é E5 (`04a`).
- E27. **Garantia de casa e terra** (não há posse de casa nem terra comprável, E15); **o credor não leva o devedor à corte**
  (a justiça do AD-150 não tem dívida como caso); **reabertura do próprio dono com sócio novo e cota no espólio** (E18).
- E28. Não verificado: goldens, pins de 90 dias e baselines LongRunning (na junção); `verify --full`; determinismo em 2
  processos; custo a 5.000 NPCs; seeds 21–40 (a fome não piorou: 294 contra 309).
- E29. `Credit` e `Venture` fora dos cenários JSON (como E16): lá ninguém empresta nem fecha.
