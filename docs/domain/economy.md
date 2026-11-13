# Economia

Como riqueza, escassez e preço **emergem** da atividade da população — em vez de saírem de
uma tabela de preços escrita à mão.

Não existe "preço do pão" configurado. Existe trigo plantado, moinho funcionando, padeiro
empregado e família com fome. Se ninguém planta, o pão encarece sozinho — e ninguém
precisou programar essa consequência.

## Elementos

| Elemento | Papel |
|---|---|
| Recursos naturais | Estoque bruto do território; finito e localizado |
| Produção | Trabalho + recurso + local → bem |
| Trabalho e salário | NPC ocupa vaga em um local; recebe mensalmente |
| Consumo | Diário, puxado pelas necessidades do NPC |
| Oferta e demanda | Estoque disponível vs. intenção de compra no mercado local |
| Preço | Derivado de oferta/demanda, por mercado, não global |
| Comércio | Move bens entre mercados quando o diferencial de preço paga a rota |
| Imposto | Transferência de moeda do NPC para o governo da cidade |
| Poupança e dívida | Reserva contra crise; dívida é pressão que gera comportamento |
| Propriedade | Todo bem e todo local têm dono; dono muda por venda, herança ou tomada |

## A cadeia causal

O valor da economia está em ela ser um **transmissor de choque** entre sistemas:

```
colheita ruim de trigo
  → oferta de alimento cai
  → preço do pão sobe
  → famílias pobres não conseguem comer
  → fome aumenta criminalidade
  → quem pode, migra
  → cidade perde mão de obra e ganha instabilidade política
```

Nenhum passo é roteiro. Cada seta é um sistema reagindo ao estado que o anterior deixou.

## Profissões

Dependem do período e do nível tecnológico da sociedade — são conteúdo, não motor.

- **Medieval:** agricultor, lenhador, ferreiro, comerciante, guarda, curandeiro, professor,
  desempregado.
- **Moderno:** operário, médico, burocrata, cientista, programador, prestador de serviço.

Desemprego é um estado legítimo e comum, não um erro: vagas são finitas.

Ninguém nasce com ofício (AD-136): ele se escolhe na juventude pelo gosto próprio (herdado em parte), pelo
temperamento e corpo, pelo que se aprendeu em casa, pela vaga que paga e pela pressão de quem criou — forte numa
vila medieval, fraca hoje, e só no tanto em que o filho é obediente. Quem trabalha no que combina rende e aprende
mais e valoriza mais o emprego; quem foi empurrado troca quando surge vaga.

## Mercado, estoque e moeda

Cada cidade tem seu mercado local, com formação de preço própria. Estoque não é abstrato —
mora em um lugar: casa, loja, celeiro. Bem sem local não existe, e é isso que torna roubo,
seca, incêndio e bloqueio de estrada economicamente relevantes.

Moeda é **inteiro** e a soma total do mundo só muda por cunhagem explícita. Compra move
moeda de A para B; nada evapora, nada nasce em arredondamento. É invariante testável do
motor, e é ela que impede a inflação-fantasma que quebra economias emergentes.

**Cenário MVP (medieval):** trigo, madeira, ferro, água, moedas. Suficiente para fechar a
cadeia produção → consumo → preço → migração sem inventar um mundo inteiro antes da hora.

## Ano agrícola, celeiro e água (AD-135)
Uma safra de trigo de inverno por ano (`AgricultureRules`): semeadura no outono pelos lavradores presentes, crescimento
por grau-dia da célula (inverno quase parado), colheita à mão numa janela curta de verão, com perda do que fica em pé.
A colheita repõe primeiro a semente do ano seguinte; o resto vai ao celeiro do campo, que perde ~15%/ano e abastece a
venda da fazenda até a próxima safra. Rendimento = semente × grãos por semente × habilidade de quem semeou × ano de
clima (AR(1) regional, `ClimateRules.Default`). Fome sazonal e de quebra de safra saem disso, não de regra escrita.
Casa a até 1 h de rio, lago ou fonte bebe dali (`NaturalWaterSystem`); a água do mercado é só para quem mora longe.
O celeiro solta por dia o que dura até a colheita esperada; a lavoura ainda em pé antes do mês esperado conta até ele.

## Dono, lucro e ferramenta (AD-144)
**Dono.** A oficina de ofício (ferraria) é do mestre: o adulto do ofício mais hábil na cidade (`WorkplaceOwnership`). A
terra da fazenda, a obra da cidade e o espólio sem herdeiro ficam sem dono. JSON: `Owner` e `CraftLocationTypeIds`.
**Lucro.** Todo mês, depois da folha, o caixa acima de `WorkingCapitalPayrollMonths` (12) meses da folha possível (a
atual ou a dos adultos da cidade até as vagas, o que for maior) sai na fração
`SurplusDrawShare` (1 no cenário de código; 0 = desligado, padrão de JSON e snapshot antigo; nome serializado antigo
`SurplusLevyShare`): à carteira do dono, ou ao tesouro da cidade, que paga a esmola (`OwnerDrawSystem`, evento
`ProfitDrawn`). Absorve o dízimo provisório da AD-140. Sem dono, a cidade também cobre a falta até a reserva (a fazenda
comum dá prejuízo no salário fixo). A moeda só muda de dono; a reserva protege a folha seguinte.
**Ferramenta.** A receita pode declarar `Tool`: cada trabalhador precisa de um jogo (3 kg de ferro no lavrador), cada dia
trabalhado gasta (2 kg/ano) e sem ferramenta o trabalho rende 60% (lavrar, ceifar, tirar água). A fazenda compra o jogo
da ferraria; a ferraria trabalha por encomenda (não faz além do que falta aos clientes mais um mês de desgaste), vende a
preço de encomenda fixo (20; preço que reage fica para a fatia 8). Mais uma mão ali vale só a encomenda que a lotação
atual não cobre (produto marginal): o mestre não toma quem não precisa, e o ferreiro ocioso vai à lavoura.

## Ver também
- [cities.md](cities.md) — onde a produção e o estoque ficam
- [npc.md](npc.md) — necessidades que viram consumo
- [behavior.md](behavior.md) — como o NPC escolhe trabalhar, comprar ou roubar
- [society.md](society.md) — tecnologia e cultura definindo o que é produzível
- [simulation-lod.md](simulation-lod.md) — economia agregada fora do detalhe
- [time-and-ticks.md](time-and-ticks.md) — salário mensal, consumo diário
- [history.md](history.md) — crises econômicas viram registro
