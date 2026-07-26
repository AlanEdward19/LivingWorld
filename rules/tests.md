# rules/tests.md — carregada em: escrever/ajustar testes

O agente **não se autoavalia**. Quem decide "pronto" é `bash scripts/verify.sh`.

## Regras
- Todo comportamento novo/alterado tem teste antes de concluir a tarefa.
- Nomeie por comportamento: `<unidade>_<condição>_<resultado esperado>`.
- Um assert lógico por teste. Sem rede, disco real ou `DateTime.Now` — o mundo tem
  relógio próprio, use-o. Sem `Thread.Sleep`.
- Bug corrigido entra com teste de regressão que **falha sem o fix**. Prove que falha.
- Rode por `bash scripts/test.sh` (`--watch` no desenvolvimento). Nunca monte o runner.

## Camadas de teste deste projeto
| Camada | O que cobre | Custo |
|---|---|---|
| Unit | Regra isolada: hereditariedade, utility score, preço, invariante de entidade | barato |
| Determinismo | Mesma seed → mesmo hash de mundo. Obrigatório por sistema novo | barato |
| Arquitetura | `Domain`/`Simulation` sem `Random`/`DateTime.Now`/referência a `AI` | barato |
| Cenário | Roda N anos e afere propriedades agregadas do mundo | caro — poucos |
| Fumaça de LLM | Contrato de saída parseia e é rejeitado quando inválido. Provider **fake** | barato |

## Testes de cenário (property-based, não valor exato)
Não asserte "população == 137". Asserte propriedades que precisam valer sempre:
```
after 100 years with seed 42:
  population > 0                     // a vila não colapsou por bug
  no npc has age > maxLongevity      // ninguém imortal
  sum(coins) == initial + minted - burned   // dinheiro não vaza
  every child has two known parents
  no npc employed at a workplace that no longer exists
```
Valor exato muda a cada ajuste de regra e gera teste que só sabe reclamar.

## Nunca
- Testar contra LLM real no gate. Provider real fica atrás de flag, fora do `verify.sh`.
- `Assert.True(true)` disfarçado: teste que passaria com o código deletado.
