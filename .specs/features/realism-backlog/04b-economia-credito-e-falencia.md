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

Topo final (código de `36591769`, antes do merge do AD-154), 20 seeds × 100 anos contra a base `3a469e0d`:

| Medida | Base | AD-155 |
|---|---|---|
| Extintas; pop final (mín) | 0; 286,4 (201) | 0; 286,5 (185) |
| Fome; sede (mortes) | 309 (2,4%); 5 | 321 (2,5%); 5 (dentro de ±20%) |
| e0; 1q0 coorte | 31,2; 203‰ | 31,1; 205‰ |
| Gini adultos / casas, ano 100 | 0,866 / 0,811 | 0,884 / 0,831 |
| Moeda na cidade, ano 100 | 58,7% | 59,8% |
| Aberturas: poupança/herança/sociedade/empréstimo | 47 (sem empréstimo) | 25: 6/10/6/3 (+4 compradas fechadas) |
| Reaberturas; vivos aos 5 anos | 32; 26/45 | 0; 22/23 |
| Empréstimos (seeds com algum) | — | 16.847 (20/20): comida 16.790, giro 54, abertura 3 |
| Calotes / encerrados; perdido | — | 213 (1,2%); 12.678 (espólio 107, partiu 91, penhora 15) |
| Falências; demitidos por caixa | — | 9; 188: um ano depois 42 trabalhando, 129 sem, 19 partiram/morreram |
| Moeda total | — | conservada (desvio 0) |

Oficinas paradas (E25), 6 seeds × 100 anos, meses-forja de dono parada por década, sem crédito (`LW_AG=nocredit`, que é a
base mais as contagens) × com crédito: 0:0 10:0 20:19 30:112 40:87 50:278 60:332 70:546 80:709 90:964 (soma 3.047) ×
0:0 10:0 20:0 30:0 40:0 50:0 60:2 70:5 80:19 90:10 (soma 36, mais 236 meses fechadas por falência). Aberturas 14 × 6 e
reaberturas 3 × 0 nas mesmas seeds. **A queda vem do giro**: o dono põe do bolso, toma giro ou demite e a forja deixa de parar;
sem forja parada não há vaga de mercado para outro abrir nem oficina para reabrir. E20 não pesa: com ele desligado
(`LW_AG=noownerkeeps`) o resultado de 6 seeds é idêntico byte a byte (o dono não tinha emprego 25% melhor).

Comida: 16.790 empréstimos de 2.136 tomadores (média 7,9 por pessoa na vida, mediana 5, p90 18, máximo 70), ~8,4 por seed-ano,
fome concentrada antes da colheita. É crédito sazonal repetido de quem vive no fio, um contrato por vez, quitado em 99% dos
casos: não é rolagem (nunca dois abertos; o novo só depois de quitar o anterior). Não limitado.

Custo a 1.000 NPCs, pares alternados (`profile.sh`): base 2,07/2,66 µs, 190/204 B; AD-155 2,55/2,06 µs, 203/191 B por
NPC-tick: dentro do ruído (médias 2,36 → 2,31 µs; 197 → 197 B). Os sistemas novos são mensais e ficam abaixo de 7 ms.

## Ficou de fora das fatias 6 e 7 (nada some)

- E22. **Empréstimo de abertura raro** (3 em 20 seeds, era 0). Feito: quem financia escolhe entre sócio e credor (aversão ao
  risco, se sabe o ofício, ambição do fundador) e o fundador ambicioso e pouco medroso guarda parte da poupança e pede essa
  parte. Ainda raro porque as aberturas são poucas (25 em 20 seeds, E25) e a parcela tem de caber na metade do ganho que a
  conta promete. Meta "maioria das seeds" **não fechou**. Não medido: quantas decisões de abrir falham por capital (sonda não rodou).
- E23. **Calote baixo (1,2%)** e quase todo por morte ou partida; a penhora é rara porque a parcela de comida cabe no salário.
  Sem número de referência firme (`08b` item 90).
- E24. **Quem é demitido por falta de caixa fica sem trabalho** (129 de 190 um ano depois): o desemprego estrutural da vila
  (jovens sem trabalho, `08` itens 48 a 61) não absorve. Medido, não tratado.
- E25. **Aberturas 47 → 25 e reaberturas 32 → 0**: causa medida acima (o giro segura a forja; E20 não pesa). Mais realista,
  mas a vila tem menos rotação de mestres. Ferraria viva em 19/20 no ano 100.
- E26. **Gini sobe ~0,02** e a moeda segue indo ao tesouro da cidade (59,8% no ano 100): o empréstimo de comida faz a moeda
  dos que emprestam ir à fazenda comum e daí à cidade. Destino da moeda da cidade é E5 (`04a`).
- E27. **Garantia de casa e terra** (não há posse de casa nem terra comprável, E15); **o credor não leva o devedor à corte**
  (a justiça do AD-150 não tem dívida como caso); **reabertura do próprio dono com sócio novo e cota no espólio** (E18).
- E28. Não verificado: goldens, pins de 90 dias e baselines LongRunning (na junção); `verify --full`; custo a 5.000 NPCs e
  custo depois da tentativa de E22; seeds 21–40.
- E29. `Credit` e `Venture` fora dos cenários JSON (como E16): lá ninguém empresta nem fecha.
