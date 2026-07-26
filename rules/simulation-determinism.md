# rules/simulation-determinism.md — carregada em: sistema de simulação / tick / RNG

**Invariante do projeto**: mesma seed + mesmo cenário + mesmo número de ticks
= **mundo byte-idêntico**. Sem isso não há teste de 100 anos, replay, nem bug reproduzível.

## Regras
- **Nunca** use `new Random()`, `Random.Shared`, `Guid.NewGuid()`, `DateTime.Now/UtcNow`
  dentro de `LivingWorld.Domain` ou `LivingWorld.Simulation`. Um teste de arquitetura
  falha o build se aparecer.
- Toda aleatoriedade sai do RNG do mundo, derivado por stream: `ctx.Rng(NpcId)`,
  `ctx.Rng(SystemId)`. Streams separados por sistema/entidade — assim adicionar um
  sistema novo não desloca a sequência dos outros.
- Tempo vem do relógio do mundo (`WorldDate`), nunca do relógio da máquina.
- **Iteração ordenada.** Nunca itere `Dictionary`/`HashSet` para produzir efeito no mundo:
  ordene por ID. Ordem de hash muda entre runs e quebra a reprodutibilidade.
- Sem `Parallel.For` / `async` em sistema de simulação a menos que o resultado seja
  provado independente de ordem (e com teste que prove).
- Ponto flutuante: use `double` de forma consistente; não some em ordem variável.
  Grandezas de dinheiro e estoque são inteiras (centavos / unidades), nunca `float`.
- Todo evento que muda o mundo é registrado no event log com o tick em que ocorreu.

## Teste obrigatório de qualquer sistema novo
```
determinism_same_seed_produces_identical_world:
  arrange  world A = Sim(seed: 42).Run(ticks: 3650)
           world B = Sim(seed: 42).Run(ticks: 3650)
  assert   Hash(A) == Hash(B)
```
E um par com seeds diferentes que **não** deve bater — senão o hash não está medindo nada.

## Canônico vs. volátil (ADR-0014)
**Canônico se alimenta uma decisão. Volátil se é recomputável do canônico ou é cosmético
sem efeito causal.** O teste é um só: alguma decisão de NPC, sistema ou regra lê este campo?
Canônico entra no hash; volátil não. **Na dúvida, canônico** — falso canônico custa hash
instável, falso volátil custa mundo irreprodutível.

## Branch (ADR-0008/0009)
Hash canônico é **por branch**. Seed de um branch novo é derivada, nunca sorteada:
`seed_B = H(seed_A, tick_divergência, id_intervenção)`. Duas ramificações iguais da mesma
linha produzem o mesmo mundo. O salto é um evento **anexado** — nunca `UPDATE` no passado.

## Frequência de tick
Registre o sistema na frequência mais barata que ainda produza o comportamento.
Escala: `Hourly` → `Daily` → `Monthly` → `Yearly`. Na dúvida, `Daily`.
Coisa rara e datável (parto, colheita, morte agendada) **não** vira varredura por tick:
agenda um evento futuro no scheduler. Ver `docs/domain/time-and-ticks.md`.
