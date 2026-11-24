# Crime, violência, justiça e herança (AD-150)

Furto, briga, homicídio, boato, denúncia, julgamento, punição e partilha da herança. Tudo sai do estado do
mundo (fome, moeda, temperamento, corpo, mágoa, medo) e do RNG semeado; nenhum crime é escrito à mão. A LLM
pode narrar, nunca criar um crime nem um veredito (`rules/llm-boundary.md`).

## Furto (`TheftRules`, ligado por padrão)
| Motivo | Quando | Como decide |
|---|---|---|
| fome | fome ou sede abaixo do limite, sem o que falta em casa (comida ou água) e sem como comprar; com sede e água em casa, bebe | na hora, pela ação Furtar; a fome rebaixa a barreira moral |
| fome da casa | alguém da casa (filho, cônjuge) no limite e nada para dar | uma vez por dia; o adulto vai buscar |
| miséria | pouca moeda e despensa curta | uma vez por dia, ato numa hora sorteada |
| oportunidade | o temperamento passa a barreira moral | idem |

- Barreira moral = peso de personalidade da ação Furtar (consciência, altruísmo, impulsividade, aversão a
  risco) contra o piso do cenário, mais o medo da punição. Corpo: agressividade (testosterona por idade e
  sexo, AD-139) ao quadrado — mais em homens jovens. A miséria multiplica a chance.
- Furto miúdo (valor < 40) quase não vai à corte. Bens: moeda de quem está ao lado (só se há onde comprar o que falta), moeda guardada em casa, comida da despensa de outra casa, comida da banca
  e do celeiro, caixa e estoque de oficina (o ferro é a ferramenta da lavoura). Animal de criação não existe
  no mundo; peça inventada fica fora (a invenção é de outra fatia).
- O que foi levado falta à vítima (sai do estoque ou da carteira). Comida vai à despensa do ladrão; moeda à
  carteira; ferramenta é vendida a um receptador (a oficina que a usa) pela metade do valor, senão fica em casa.
  A moeda nunca é criada nem destruída.
- Guarda: profissão do cenário (nenhuma no padrão); cada guarda tira sucesso e soma medo.

## Violência (`CrimeRules.Violence`)
- Motivos: ofensa (atrito do convívio, mais com raiva), ciúme (o traído contra o amante), traição (furto de
  quem confiava), herança, denúncia, vingança de parente, flagrante (a vítima reage ao ladrão).
- Propensão: agressividade do corpo ao cubo × impulsividade × instabilidade emocional; × (1 − medo).
- Mágoa (`CrimeLedger.Grudges`): inimizade com motivo e intensidade, que decai (meia-vida); agressão por
  mágoa sai da intensidade que resta. Quem bateu desabafa (a mágoa cai à metade).
- Desfecho: dano pela força relativa (músculo); a saúde dinâmica (AD-134) cuida da recuperação e do risco de
  morrer depois; golpe mortal raro = homicídio (`Homicide`, pelo caminho das mortes: luto, herança).
  Parentes do morto (e do muito ferido) guardam mágoa de vingança.
- A briga daqui é fora de casa; violência doméstica, sequestro e violência sexual estão em
  [crime-against-persons.md](crime-against-persons.md) (AD-154). Bebida e dívida em dinheiro não existem no mundo.

## Justiça (`CrimeRules.Justice`)
1. **Testemunha**: quem está no lugar na hora vê (acordado bem mais que dormindo) e às vezes aponta a pessoa
   errada (um conhecido de má fama ou outro passante). Sem ninguém, a vítima desconfia de quem tem má fama.
   Sem acusado: impunidade.
2. **Boato** (achado 26): quem sabe conta a um conhecido por dia, na chance do cenário; cada passagem pode
   trocar o acusado ou aumentar o valor. Cada ouvinte sobe a má fama do acusado e confia menos nele.
3. **Denúncia ou silêncio**: a vítima (morta, quem sabe) denuncia quem acredita culpado; parente e quem gosta
   muito calam; o medo (próprio e a fama violenta do acusado) cala. Sem autoridade, fica a mágoa.
4. **Corte** a cada três semanas (assembleia ou senhor): condena pela prova (testemunhas que confirmam,
   flagrante) e pelo preconceito (má fama); inocente pode ser condenado. O acusado de homicídio às vezes foge.
5. **Punição**: furto — restituição e multa (à cidade); sem pagar ou furto grave, açoite no pelourinho
   (saúde); reincidente grave, banimento. Agressão — compensação à vítima ou castigo. Homicídio — forca ou
   banimento. O condenado guarda mágoa de quem denunciou; o punido lembra (`Punished`, ou `WronglyPunished`).
6. **Dissuasão** (consumidor causal): cada punição sobe o medo da cidade, que decai; quem pensa em furtar ou
   brigar pesa o maior entre esse medo, a própria punição lembrada e os guardas.

## Herança (`CrimeRules.Inheritance`, achado 30)
O cônjuge vivo fica com a terça parte e os filhos repartem o resto; a oficina vai ao filho homem mais velho.
Partilha desigual e temperamento fazem herdeiros adultos brigarem (`InheritanceDisputed`): mágoa, maior em
quem ficou com menos. Mundo sem `CrimeRules` (snapshot antigo): o cônjuge herda tudo, como antes.

## Eventos e cliente
`TheftCommitted`, `Assault`, `Homicide`, `CrimeReported`, `Verdict`, `Executed`, `Banished`,
`InheritanceDisputed` (payloads no `WorldEventKind`). O cliente mostra cada um como aconteceu, com nomes,
motivo, bem, desfecho e punição (`CrimeEventNarrator`), inclusive o furto que ninguém viu e o condenado
inocente: o observador é onisciente.

## Custo
Uma passada diária pelos vivos com um sorteio por pessoa e motivo; a conta (temperamento, corpo, medo) só
para quem o sorteio cai abaixo do teto da chance. Ato, boato, denúncia e corte são por evento e por caso.

## Números
Padrões em `TheftRules`, `ViolenceRules`, `JusticeRules`, `InheritanceRules`; fontes e a marca "precisa
de conferência" no registro AD-150 de `docs/decisions-log.md`.
