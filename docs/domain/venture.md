# Quem abre o próprio negócio (AD-152)

Backlog 04, fatias 4 e 5. Antes, nenhum NPC abria nada: a cidade enfileirava a oficina que faltava, pagava o material com o
tesouro e a obra saía de graça; a oficina nascia sem dono e com caixa zero. Agora o adulto que sabe o ofício decide abrir o
dele, com o dinheiro que tem, e a oficina nasce dele. Nada é lista escrita: sai do estado da pessoa e da cidade.

## A decisão (`VentureSystem`, mensal, depois da folha e da retirada do lucro)
- **Quem pode:** adulto com a idade de mestre (`MinAgeYears`, 21) e habilidade no ofício da oficina (`MinSkill`, 30 de 100),
  que não tem oficina trabalhando. A profissão de hoje não importa: importa saber o ofício. O dono de uma oficina do ofício
  parada (folha que não fechou, ninguém nela) reabre a dele: põe só o capital de giro do casal, sem sócio novo.
- **A conta do negócio (`VenturePlan`):** o preço que cobraria (o do concorrente que já trabalha na cidade, menos 10% para
  ter freguesia; sem concorrente, o preço do ofício), quanto venderia no mês (a encomenda de ferramenta da cidade, ou a
  procura-base por pessoa de um bem de mercado, dividida entre ele e quem já vende; nunca mais do que a oficina nova faz) e
  a folha que pagaria. A margem `(venda − folha) ÷ venda`, entre 0 e 1, é a oportunidade percebida. Bem que ninguém compra
  tem margem zero; a vila que só sustenta um ferreiro não chama o segundo enquanto o primeiro trabalha.
- **O caráter:** chance do mês = `MonthlyChance` (0,25) × oportunidade × `Ambição/100` × `(1 − AversãoAoRisco/100)`. Quem
  está sem trabalho pago no ofício (nenhuma oficina dele com vaga que paga e precisa de mão) sente a oportunidade em dobro.
  O sorteio é `StreamFor("venture", npc × 1.000.003 + mês)`. Uma abertura por ofício, por cidade, por mês.

## O capital (`VentureCapital`)
1. **O casal:** carteira do fundador e do cônjuge acima da reserva da casa (`ReserveWageMonths`, 6 meses do salário mais
   baixo × `0,5 + Aversão/100`). É a poupança de anos de salário (e de lucro, para quem já é dono). A parte que veio de
   herança é separada por `Npc.Inheritance`, que o espólio soma na hora da partilha (só com a regra ligada).
2. **Sócios:** parentes crescidos (pais, filhos, irmãos) por id, depois amigos com confiança ≥ 60 nos dois sentidos. Cada um
   entra com até `1 − Aversão/100` do que tem acima da própria reserva, no máximo dois. A cota (`Workplace.Partners`, em
   décimos de milésimo) é a parte que ele pôs do capital.
3. **Empréstimo** (AD-155, [credit](credit.md)): a falta, até metade do capital × `1 − Aversão/100`, de parente, conhecido,
   o rico da vila ou o tesouro, com a oficina em garantia.
Sem fechar a conta, não abre e nada muda. Origem contada: `sociedade` se houve sócio; `emprestimo` se houve credor; `heranca`
se metade ou mais do que o casal pôs era herança; senão `poupanca`. Uma oficina fechada do ofício na cidade é comprada de quem
ficou com ela em vez de levantar outra (AD-155).

## O custo (`VentureFounding`), todo com moeda que existe
| Parte | Para quem vai | Padrão |
|---|---|---|
| Foro do lote | tesouro da cidade (senhor/comuna) | 6 meses do salário do ofício |
| Obra | adultos sem trabalho da cidade (senão os da casa do fundador; sem ninguém, ele levanta só e não paga) | 60 dias-homem ao salário diário mais baixo, até 3 braços |
| Equipamento | quem o faz na cidade (nunca a ferramenta em uso de outra oficina); sai do estoque à venda e vira capital fixo. O que falta pronto vem de fora pelo mercado da cidade, a preço de teto, e a moeda vai ao tesouro | 40 de ferro na forja |
| Estoque inicial | quem vende o insumo na cidade | 3 dias de insumo (a forja não usa) |
| Capital de giro | caixa da oficina nova | 6 meses da folha inicial |

A oficina nasce com `Owner` = fundador, os sócios, o preço da conta, 4 vagas e o fundador trabalhando nela (sai do emprego
de antes sem evento de demissão). Evento `VentureFounded`: `oficina|fundador|origem|capital|doCasal|herdado|sócios[|reaberta]`.

## O lucro e os sócios
`OwnerDrawSystem` tira a cota de cada sócio da retirada do mês antes do dono (evento `ProfitDrawn` por sócio). Cota de
sócio morto passa aos herdeiros dele (`EstateSystem.HeirsOf`, partes iguais); sem herdeiro, ou quando o herdeiro é o
próprio dono, volta ao dono. A oficina de dono não é coberta pela cidade: sem venda, a folha falha e a gente sai (AD-128).

## A cidade
Com a regra ligada, a cidade não ergue oficina de ofício que a gente abre por conta própria (`ConstructionDemandSystem`):
o senhor ou a comuna constrói o que é dele (terra comum, moradia, obra pública), não a forja de alguém. No cenário de
código a cidade não constrói (`CityRules` desligado), então o emprego ali não muda por isso. Cenários JSON sem `Venture`
seguem como antes (a cidade abre tudo, ninguém empreende).

## Medir
`DemographyReport` imprime `negocios abertos por NPC` (total, seeds com algum, capital médio, origem), sobrevivência aos
5 e aos 10 anos (gente trabalhando e folha paga) e a contagem por década (`VentureTally`, só lê eventos e o fim do mês).

## Limites conhecidos (backlog 04, itens E12 a E21)
Empréstimo e falência em [credit](credit.md) (AD-155); obra instantânea (o prédio sai no mês da decisão); preço fixo depois de aberto (só o recém-chegado
baixa); a terra não se compra nem se arrenda; a obra da cidade continua de graça; `Venture` não está nos cenários JSON;
nenhum número tem fonte firme (`08`, itens 68 a 72).

## Ver também
[economy.md](economy.md) · [genetics-and-family.md](genetics-and-family.md) · [cities.md](cities.md)
