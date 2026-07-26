# Níveis de Detalhe da Simulação (LOD)

Como o mundo decide quanto detalhe gastar em cada lugar, para sustentar milhares de
habitantes sem simular todos com a mesma profundidade.

## Princípio

**A escala determina o detalhe.** Uma aldeia a três reinos de distância não precisa de
horários individuais; a taverna onde o jogador está agora precisa.

## Três resoluções

| Resolução | Quando | O que existe |
|---|---|---|
| **Agregado** | Região longe de qualquer jogador | Só estatística: totais, taxas, contagens |
| **Detalhado** | Jogador na cidade | Pessoas presentes, edifícios, horários, atividades, transações |
| **Máximo** | Conversa direta com um NPC | Personalidade, emoção, memória, objetivos, conhecimento, relação com o jogador, LLM |

Um tick agregado produz algo como: *"a cidade produziu 3.200 de alimento, população +1,8%,
42 famílias formadas, 17 mortes, 12 migrações, desemprego subiu"*. Ninguém em particular
morreu — dezessete pessoas morreram. Basta enquanto ninguém está olhando.

## Níveis espaciais

```
global    -> continentes, biomas, clima, rotas, guerras, epidemias
regional  -> reinos, províncias, facções, economias
cidade    -> bairros, empregos, comércio, saúde, educação
local     -> casa, taverna, oficina, mercado
indivíduo -> um NPC
```

O nível espacial é *onde*; a resolução é *com quanto detalhe*.

## Materialização de NPC

Uma cidade se descreve assim: `População: 12.430 / detalhados: 320 / agregada: 12.110`.
Um NPC agregado vira entidade completa quando:
- o jogador interage com ele;
- ele ocupa posição importante (prefeito, mestre de guilda, capitão);
- ele participa de evento relevante;
- ele entra na proximidade do jogador;
- ele tem impacto histórico.

Ao materializar, o NPC recebe identidade, família, atributos, profissão, relações e
histórico **coerentes com as estatísticas agregadas de onde veio**. Se a cidade passou por
uma fome há dez anos, o ferreiro recém-materializado provavelmente perdeu alguém nela.
Materialização não é sorteio livre: é amostragem condicionada pelo agregado.

## Desmaterialização

Um NPC detalhado que perdeu relevância volta a ser estatística. O que é historicamente
significativo é preservado — feitos, cargos, laços com personagens ainda detalhados,
relação com o jogador. O resto é descartado sem culpa.

## Conservação

Agregado e detalhado **precisam bater**. Materializar 10 NPCs não cria 10 pessoas do nada:
os 10 saem do pool agregado (`12.110 → 12.100`). Vale igual para comida, moeda, empregos e
casas — o que o NPC detalhado consome ou ocupa é debitado do agregado.

Violar isso produz inflação silenciosa: a cidade "cresce" toda vez que o jogador entra
nela. **Materializar move, nunca cria**; desmaterializar devolve.

## Ver também
- [npc.md](npc.md) — o que compõe um NPC materializado
- [time-and-ticks.md](time-and-ticks.md) — frequência de tick por resolução
- [world-map.md](world-map.md) — particionamento espacial e proximidade de jogador
- [cities.md](cities.md) — estatísticas agregadas de um assentamento
- [llm-contract.md](llm-contract.md) — contexto enviado no nível máximo
