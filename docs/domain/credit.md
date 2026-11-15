# Empréstimo, capital de giro e falência (AD-155)

Backlog 04, fatias 6 e 7, em cima de [venture](venture.md). Antes, ninguém emprestava (a ajuda mútua é doação), a oficina que
não fechava a folha só parava e nunca fechava (sobra E19), e o dono largava a própria oficina por um emprego 25% melhor (E20).
Agora há crédito em moeda que existe: a moeda sai da carteira de quem empresta e volta em parcelas; nada é criado.
Regras em `CreditRules` (`EconomyRules.Credit`; nulo = desligado; ligado no cenário de código, fora dos cenários JSON).

## Quem empresta (`Lending.Find`), pela ordem da vida
1. **Parentes** crescidos (pais, filhos, irmãos; o cônjuge já é do casal), por id, sem acréscimo.
2. **Conhecidos** do tomador na cidade, por id.
3. **O rico da cidade** (maior carteira primeiro) e 4. **o tesouro da cidade** (senhor, comuna; até 25% do que tem), estes
   dois só com a oficina em garantia.
Cada pessoa empresta até `1 − Aversão/100` do que tem acima da própria reserva (a mesma de quem abre). Empresta se a confiança
dela no tomador, mais o parentesco (+40), mais a garantia (+40), mais o favor que ela deve a ele (eixo `Debt`, a
reciprocidade), passa de 40; se a parcela cabe em metade do ganho que a conta do negócio promete (venda − folha); se o tomador
não deu calote nos últimos 10 anos (o parente perdoa o calote que não foi com ele) nem tem má fama na justiça acima de 30.

## A norma da usura (vila cristã dos séculos XIII–XIV)
O juro aberto era pecado e crime; ele vinha disfarçado. Adotado: **acréscimo fixo no contrato** (10% ao ano do prazo, sobre o
principal: o que se devolve a mais), **multa pelo atraso** (5% da parcela não paga, a "pena" que o direito tolerava) e um
**teto** (acréscimo e multas nunca passam de 20% ao ano do prazo): é o que trava a espiral de juros. Parente não cobra.
Fontes que lembro, todas **precisam de conferência**: Briggs, *Credit and Village Society in Fourteenth-Century England* (2009),
crédito de aldeia pequeno, curto e com juro escondido; Concílio de Latrão III (1179) e Lyon II (1274) contra a usura; o teto
dos judeus ingleses (2 dinheiros por libra por semana, ~43% ao ano) como o caro de fora; os montes de piedade (~5–10%) do XV.

## Quem toma e para quê
- **Abrir** (`VentureCapital.Raise`): quem financia escolhe ser sócio ou credor (sócio se `1 − Aversão` + 0,25 se sabe o
  ofício − Ambição do fundador/200 ≥ 0,5); os que preferem sociedade entram primeiro, depois o empréstimo, depois os outros
  como sócios. O fundador ambicioso e pouco medroso guarda `(Ambição − Aversão)/100` da poupança (até a metade) e pede essa
  parte. O empréstimo não passa de metade do capital × `1,5 − Aversão/100`. Prazo 36 meses; a oficina nova é a garantia.
  Origem `emprestimo` (`sociedade` se também houve sócio).
- **Reabrir** a oficina parada, ou **comprar uma fechada** (`VentureFounding.TryBuyClosed`, pelo foro + obra + equipamento,
  a quem ficou com ela) em vez de levantar outra. Prazo 12 meses.
- **Capital de giro** (`WorkingCapitalSystem`, mensal, antes da folha): na oficina de dono cujo caixa não cobre a folha, o dono
  põe primeiro o que o casal tem acima da reserva; depois, se a venda que a conta promete passa da folha, pede 3 meses de folha
  (um empréstimo de giro por vez); o que ainda falta ele corta: **demite os últimos contratados**, nunca ele mesmo.
- **Comida no aperto** (`FoodCredit`, no mesmo sistema): a casa com menos de 7 dias de comida por pessoa e sem dinheiro para
  15 dias pede a falta a parente ou conhecido (sem garantia), em nome de quem tem salário; a parcela (12 meses) cabe na metade dele.

## Parcela, atraso e calote (`CreditSystem`, mensal, depois da folha e antes da retirada do lucro)
- A parcela sai do caixa da oficina dada em garantia e, faltando, da carteira do devedor acima de 1 mês do menor salário (o
  que a penhora deixa). Parcela incompleta: atraso, multa até o teto. Pagou tudo: a palavra cumprida aumenta a confiança.
- **6 meses seguidos** sem a parcela inteira: **calote**. Com a oficina em garantia: falência (abaixo). Sem garantia: o credor
  penhora a carteira acima do mínimo e perde o resto. O devedor fica com a fama de mau pagador (`CreditLedger.Defaults`, 10
  anos); o credor perde a confiança (a zero) e o afeto (traição) e, com a justiça ligada, guarda mágoa "divida", que pode
  virar briga (sobra V2 do backlog 06, ligada aqui).
- `Relationship.Debt` segue sendo **favor**, não moeda: o empréstimo é ajuda (o tomador fica grato e em dívida de favor com o
  credor) e o favor que o credor deve conta como confiança na hora de emprestar. A dívida em moeda mora só no livro.

## Falência (`Bankruptcy`, sobra E19)
A oficina de dono cuja folha não fecha **6 meses seguidos** (ou cujo empréstimo com garantia caloteou) fecha de verdade:
1. a gente fica livre (evento `Fired`) e procura trabalho pelo caminho de sempre;
2. o estoque é vendido, ao piso, a quem usa ou vende o bem na cidade, até o caixa de cada um;
3. os credores recebem na ordem: **salário atrasado** (rateio; o atraso da oficina de dono fica devido e é pago quando o caixa
   volta), depois os **empréstimos com a oficina em garantia**, por id;
4. a sobra vai ao dono e aos sócios pela cota;
5. o primeiro credor com saldo **fica com o prédio** (o valor dele abate a dívida); sem credor, o prédio segue do dono;
6. o que ainda se deve vira dívida pessoal do dono e dos sócios **na proporção das cotas**.
A oficina fica `Workplace.Closed`: não contrata, não paga folha, a cidade não a cobre. Quem ficou com ela reabre (se sabe o
ofício) ou a vende a quem quer abrir.

## Morte e partida
O espólio paga a dívida pessoal do morto **antes** da partilha (`CreditEstate`, uma linha em `EstateSystem`); o que a carteira
não cobre se perde. A dívida com garantia segue a oficina: quem a herda passa a dever. O crédito do morto passa aos herdeiros
dele pela norma do espólio (partes iguais); sem herdeiro, à cidade. Quem deixa o mundo devendo dá calote (o credor fica com a
garantia, se havia).

## O dono e a própria oficina (E20)
Enquanto a oficina dele paga, o dono não a troca por emprego melhor (`BehaviorDecisionSystem`, só com `Venture` ligado).

## Medir
`DemographyReport` imprime `credito`: empréstimos por finalidade e por década e tipo de credor, quitados, calotes (taxa, quanto
os credores perderam, como), falências, liberados e demitidos por falta de caixa e onde estavam um ano depois, e o maior desvio
da moeda total (0 = conservada). O Gini segue na linha `moeda por decada` (`CreditTally`, `EconomyTally`; só leem).

## Limites (backlog 04b, itens E22 a E29)
Empréstimo de abertura não acontece na prática (o parente entra de sócio); calote baixo; os demitidos ficam sem trabalho;
casa e terra não servem de garantia; o credor não leva o devedor à corte; números sem fonte firme (`08b`, itens 90 a 96).
