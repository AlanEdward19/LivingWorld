# Comportamento

Como um NPC decide sem LLM: utilidade, rotina, inércia e aprendizado, tudo no motor.

## A mistura

| Técnica | Para quê |
|---|---|
| Máquina de estados | Estado grosso: dormindo, trabalhando, viajando, lutando |
| **Utility AI** | Escolher a próxima ação — o núcleo da decisão |
| Árvore de comportamento | Executar uma ação composta em ordem |
| Planejamento simples | Encadear passos até um objetivo |
| Regras e probabilidade | Tudo que é barato demais para merecer cálculo |

## Utility AI: o núcleo

Cada ação candidata recebe nota de três entradas: **necessidades**, **contexto** (horário,
distância, disponibilidade, dinheiro) e **personalidade**.

| Ação | Situação | Nota |
|---|---|---|
| Comer | fome 90, comida perto | **95** |
| Trabalhar | precisa de dinheiro 60, horário de trabalho | 72 |
| Dormir | sono 40 | 35 |

Vence a maior nota. A personalidade **pesa** essa nota, não decide sozinha:

```
nota = utilidadeBase(necessidade, contexto) * pesoPersonalidade
disciplinado alto -> peso maior em Trabalhar
impulsivo alto    -> peso maior em Diversão
altruísta alto    -> peso maior em Ajudar
```

A vida interior entra como termos somados à nota (AD-138, [inner-life.md](inner-life.md)): luto,
medo, vergonha, alegria, solidão, lembrança de fome e falta de renda; o rastro mostra cada termo.

Peso não é trava: com fome 95, até o mais disciplinado come. Daí sai objetivo e plano —
raso de propósito, replanejado no tick seguinte se um passo falhar:

```
Fome alta -> objetivo "procurar alimento" -> checar estoque de casa
          -> comprar no mercado -> sem dinheiro? trabalhar ou pedir ajuda
```

## Rotina, sobreposição e inércia

Cada profissão e estágio de vida tem rotina diária — camponês no campo ao amanhecer,
criança na escola, idoso na praça. **A rotina é o padrão**; a utility só a sobrepõe diante
de algo urgente (fome, perigo, doença, oportunidade rara), e assim a maior parte da
população segue um script barato e previsível. Sem freio, porém, o NPC trocaria de ação a
cada tick quando duas notas ficam próximas — *thrashing*. A ação em curso ganha **bônus de
continuidade** até concluir, e trocar exige margem sobre a nota atual: gente que termina o
que começou.

**O corpo antes do lazer** (AD-147; AD-182, 08c 149): com fome ou sede crítica, Conversar e Ficar à toa param no déficit crítico
quando há saída que sirva o corpo — a despensa (Comer) ou, desde o AD-182, a compra (moeda, comida à venda e o marco de comprar,
`PsycheRules.CriticalBodyCountsPurchase`). O jovem que ficou só em casa, com 85–154 moedas e a despensa vazia, conversava sozinho
(solidão no zero e peso de personalidade, ~190) contra a compra (160) por 25 dias até morrer de fome.

## Aprendizado

Repetição, treinamento, escola, pais, observação, ofício, leitura, experiência, falha e
trauma alimentam habilidade, com retornos decrescentes. Espiral típica de um ferreiro:

```
trabalha -> metalurgia sobe -> produz melhor -> ganha mais
         -> melhora a oficina -> contrata aprendiz -> transmite conhecimento
```

A LLM pode **narrar** essa trajetória; o cálculo é do motor e a narração nunca o altera.

## Ver também
- [npc.md](npc.md) — necessidades, personalidade e habilidades que alimentam a nota
- [time-and-ticks.md](time-and-ticks.md) — em que escala a decisão roda
- [memory.md](memory.md) — crenças que enviesam a nota
- [economy.md](economy.md) — trabalho e preço como contexto
- [household-provisioning.md](household-provisioning.md) — quem da casa vai comprar, quando e quanto (AD-148)
- [meal-routine.md](meal-routine.md) — refeição em hora certa com a despensa em casa e a medida da aflição (AD-157b)
- [llm-contract.md](llm-contract.md) — por que a LLM não escreve decisão
