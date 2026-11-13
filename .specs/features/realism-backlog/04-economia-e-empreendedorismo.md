# Economia, empreendedorismo e crédito (não começado)

> Detalhe e evidência arquivo:linha em `relatorios/relatorio-empreendedorismo.md`. Achados 17 e 23 da auditoria de realismo.

## O que o dono pediu (palavras dele)

- "Investiga isso de empreendedorismo, o que leva o npc a abrir um lugar dele. Como ele consegue dinheiro para abrir, para pagar as pessoas, etc. Empréstimo? Trabalhar antes para abrir seu negócio, etc."
- "Preciso que olhe todos os sistemas e veja o que neles não é realista e ajuste, igual seria no mundo real (na ponta do lápis)."

## Como é hoje (verificado no código pelo relatório)

- Nenhum NPC abre negócio. A cidade enfileira a obra quando há adulto desempregado sem vaga no ofício (`ConstructionDemandSystem`), paga o material com o próprio tesouro (`InputSupplySystem`), e a mão de obra da construção é de graça (`ConstructionSystem`).
- A oficina nova nasce com caixa zero. As iniciais recebem 500 mil (cenário de código) ou 500/200/200 mil (`scenarios/default.json`), contra 20 mil da cidade. Dinheiro declarado, vindo do nada.
- `Workplace.Owner` nunca é preenchido na prática: o carregador do cenário não lê o campo e só a herança o repassa. Mesmo com dono, não há retirada de lucro.
- O lucro fica preso no caixa da oficina; o salário é tabela fixa por ofício, mensal.
- A oficina nunca fecha (não existe remoção). Depois da AD-128 ela ganha a marca `PayrollFailed` quando não fecha a folha, e os trabalhadores saem.
- Ninguém compra o ferro da ferraria: nenhuma receita pede ferro e não há demanda base. A fazenda/mercado acumula quase toda a moeda (todo mundo compra comida lá e ela só gasta em salário).
- Não existe crédito em moeda. `Relationship.Debt` é só um eixo social; a ajuda mútua é doação.
- Preço de comida preso no piso; o casamento cria comida do nada (achados da auditoria).

## Desenho proposto, em oito fatias (cada uma com consumidor causal e teste)

1. **Dono e retirada.** Ler `Owner` do cenário; o dono retira o lucro acima de uma reserva de alguns meses de folha. A moeda volta a circular.
2. **Oficina sem dono.** O excedente vai para a cidade (senhor, comuna), que gasta em obras e esmola.
3. **Demanda por ferramenta.** A fazenda consome ferramenta como insumo que se desgasta; a ferraria passa a ter comprador. Generalizar: todo bem produzido precisa de quem o use.
4. **Decisão de empreender** (`VentureSystem`). Motivação: ambição, aversão a risco, habilidade no ofício, oportunidade percebida (preço alto, escassez, demanda sem oferta, vaga inexistente). O fundador paga terreno, obra, ferramentas e estoque inicial.
5. **De onde vem o capital.** Poupança de anos de salário ("trabalhar antes para abrir"); herança; sociedade com parente ou amigo de confiança (usa os vínculos); e empréstimo (fatia 6).
6. **Empréstimo.** De outro NPC com moeda, do tesouro da cidade, de um senhor ou guilda; juros, prazo, garantia (casa, terra, a própria oficina), calote com consequência (perda da garantia, vínculo destruído, má fama). Dá uso real ao eixo `Debt`.
7. **Folha, capital de giro e falência.** Atraso, demissão por falta de caixa, fechamento, venda do que sobrou, dívida que fica com o dono. Sócios e herdeiros recebem cotas e dívidas.
8. **Salário e preço que reagem.** Salário ligado à margem e à escassez de gente; preço que sobe na escassez e na entressafra (liga com a fatia de agricultura); demissão quando a oficina encolhe.

## Riscos de calibragem apontados

- Fome em cascata se a retirada do dono drenar a fazenda.
- Desemprego em massa quando uma oficina fecha.
- Espiral de juros e concentração de riqueza sem freio.
- Trocar o caixa inicial das oficinas muda o contrato do cenário: pede ADR e muda todos os goldens.

## Critérios mensuráveis

- Moeda conservada todo dia; distribuição de moeda entre carteiras, oficinas e cidade estável em 100 anos (hoje a fazenda vai de ~520 mil para ~980 mil em 50 anos).
- Negócios abertos por NPC por década, com a origem do capital (poupança, herança, sociedade, empréstimo).
- Taxa de sobrevivência de negócios novos; calotes; falências e o que aconteceu com os empregados.
- Gini e mobilidade (filho de pobre que vira dono) em 20 seeds × 100 anos.
- Demografia dentro da referência; custo por NPC-tick sem regressão.

## Já feito nesta trilha

Trabalhador não fica em quem não paga; morto libera a vaga; troca de emprego não gera evento de demissão; esmola do tesouro da cidade; dinheiro compartilhado dentro da casa (AD-128).

## Estado (2026-11-13): fatias 1 a 3 juntadas (AD-144)

Dono lido do cenário e retirada de lucro (`OwnerDrawSystem`), oficina sem dono rende à cidade, ferramenta que desgasta na
fazenda e ferraria que faz por encomenda, sucessão do oficial quando o mestre morre sem herdeiro. 20 seeds × 100 anos
contra a base: 0 extinções, fome 14,3% → 6,7% das mortes, e0 27,0 → 30,1, ferraria viva 0/20 → 19/20 aos 50 e aos 100.
Custo a 1000 NPCs: CPU +1,3%, alocação +5,2%.

## Ficou de fora das fatias 1 a 3 (nada some)

- E1. Presença na hora da produção: a fazenda é creditada por quem está na fila do mercado (o mercado é a fazenda); contar
  só trabalho feito pede recalibrar a lavoura. O defeito existe em todo o motor; na forja foi contornado pela folha.
- E2. Fila do mercado: as casas vivem abaixo de um dia de comida e passam o turno tentando comprar (liga com `08` item 46).
- E3. Alocação +5,2% a 1000 NPCs sem fonte achada (não perfilada); custo a 5000 NPCs não medido.
- E4. Prejuízo estrutural da fazenda comum: hoje a cidade recompõe.
- E5. Mina ou ferro bruto; consumo de ferro pelas casas; obra pública como destino da moeda da cidade.
- E6. `Tool` e `CraftLocationTypeIds` nos cenários JSON; lista dos bens sem consumidor nesses cenários.
- E7. Caixa inicial das oficinas: igualar à reserva piorou a fome; pede outro desenho. Gera a herança de ~490 mil do
  mestre e o Gini de 0,81 a 0,88 nos anos 10 a 30.
- E8. Mobilidade social não medida. Seed 4 termina com 24 vivos (não extinta): vigiar.
- E9. Números sem fonte firme: 3 kg, 2 kg por ano, 60%, preço 20, reserva de 12 meses.
- E10. Não verificado: determinismo em dois processos, ida e volta de snapshot com dono e ferramenta, compactação do evento
  de retirada (`ProfitDrawn`), hash do mundo antes e depois do refactor de alocação (`5720255e`).
- E11. Preço e salário que reagem, demissão: é a fatia 8, já listada acima. Próximas: fatias 4 a 8.

