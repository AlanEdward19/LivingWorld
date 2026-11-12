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
