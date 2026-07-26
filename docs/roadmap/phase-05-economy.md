# Fase 5 — Economia

**Objetivo**: a vila produz, estoca, consome e negocia. Escassez vira preço, preço vira
fome, fome vira pressão social — a primeira cadeia causal emergente do projeto. Dinheiro e
recursos são inteiros e **conservados**.

## Tasks
1. **Recursos** do cenário medieval: trigo, madeira, ferro, água, pedra. Unidades inteiras,
   nunca ponto flutuante.
2. **Produção por local de trabalho**: cada local converte entrada + trabalho de NPC em
   saída por tick, com capacidade finita. Sem trabalhador, sem produção.
3. **Estoque por local** (casa, loja, celeiro) com capacidade máxima e perda/deterioração
   declarada. Excedente acima da capacidade é perda **registrada**, não sumiço silencioso.
4. **Consumo diário**: o NPC retira do estoque acessível para saciar fome e sede da Fase 4.
   Sem estoque, a necessidade não é saciada — e a Fase 4 já sabe o que acontece.
5. **Emprego**: NPC ↔ local de trabalho com **vagas finitas**. Contratação, demissão e
   desemprego são eventos. Profissões: agricultor, lenhador, ferreiro, comerciante, guarda,
   curandeiro, professor, desempregado.
6. **Salário mensal** pago pelo empregador ao empregado; empregador sem dinheiro não paga
   e isso é um evento, não uma exceção engolida.
7. **Mercado local e formação de preço** por oferta/demanda: preço sobe quando o estoque
   ofertado cai frente à demanda, e cai quando sobra. Faixa de preço declarada no cenário.
8. **Compra e venda**: transferência atômica de dinheiro contra recurso, implementada como
   **lista ordenada de passos enumerável** (1..N). Ou todos aplicam, ou nenhum. Toda
   transação vai para o event log.
9. **Hook de injeção de falha por índice de passo**: aborta a transação no passo `i`. Existe
   para que o teste de atomicidade cubra passos futuros sem precisar ser reescrito.
10. **Cunhagem e destruição de dinheiro** explícitas e raras (imposto, tesouro, saque) —
    qualquer variação da massa monetária tem origem nomeada.
11. **Cenário base/tratamento como dado** (Fase 3): o mesmo cenário com um multiplicador de
    produção declarado, para que os testes causais rodem par a par na mesma seed.

## Critérios de verificação
Os dois primeiros são **os critérios mais importantes da fase**. Se algum deles cair, nada
mais nesta fase importa.

- **Conservação de dinheiro**: `soma de todas as moedas do mundo == inicial + cunhado -
  destruído`, exato, até a última unidade. Gate: 10 anos com o assert rodando **a cada
  tick**. Nightly: 100 anos, mesmo assert, `Category=Scenario`.
- **Conservação de recursos**: para cada recurso, `produzido == consumido + estocado +
  perdido`, exato. Mesmo regime — a cada tick em 10 anos no gate, 100 anos em nightly.
- **Escassez empurra o preço, com braço de controle**: par (base, tratamento) na **mesma
  seed**, tratamento = produção de trigo cortada pela metade a partir de `t0`. Assert
  `preçoTrat[t] > preçoBase[t]` em **todo** tick de `[t0, t0+30]`. Repetido em 10 seeds,
  exigindo **10/10**. Direção, sem magnitude e sem prazo inventado.
- **Débito além do saldo é rejeitado sem efeito colateral**: a operação retorna `Failure`
  **e** o saldo do comprador e o estoque do vendedor ficam **byte-idênticos** ao estado
  anterior. (Não se testa "saldo não fica negativo" — `Money` já é não-negativo na Fase 0.)
- **Atomicidade por injeção de falha em cada passo**: para cada `i` em `1..N` dos passos da
  transação, abortar no passo `i` e exigir `Hash(world)` idêntico ao de antes da tentativa.
  Os passos são enumerados por reflexão — passo novo entra no teste sozinho, e o teste
  falha se algum passo declarado ficar sem caso.
- **Salário sem caixa é evento, não silêncio**: empregador sem saldo emite o evento de
  salário não pago **e** deixa os dois saldos byte-idênticos.
- Todo NPC empregado aponta para um local de trabalho que **existe**; nenhum local tem mais
  empregados que vagas. Checado a cada tick em 10 anos.
- **Cadeia completa com controle**: par base/tratamento na mesma seed, tratamento = quebra
  de safra. A contagem de NPCs com fome acima do limiar do cenário é **maior** no
  tratamento em **10/10** seeds.
- **A economia entrou na conta**: desligar produção e mercado por flag de teste muda
  `Hash(world)` após 10 anos. Se o hash não muda, esta fase não está simulando nada.

## Fora do escopo
Habilidade que aumenta produtividade (Fase 6), rotas comerciais entre cidades e migração
econômica (Fase 8). Aqui o mercado é local e o comerciante não viaja.

## Ver também
[economy.md](../domain/economy.md) ·
[behavior.md](../domain/behavior.md) ·
[society.md](../domain/society.md) ·
[cities.md](../domain/cities.md) ·
[ADR-0006](../adr/ADR-0006-snapshot-mais-event-log.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md) ·
[rules/simulation-determinism.md](../../rules/simulation-determinism.md) ·
[rules/tests.md](../../rules/tests.md)
