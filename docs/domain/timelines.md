# Linhas temporais — ramificação

Viagem no tempo neste mundo **não altera o passado**. Ela cria uma linha nova a partir
dele. Nada é apagado, nada é reescrito, nenhum paradoxo é possível — e o preço é que você
não volta para casa.

## Por que ramificação, e não reescrita

O motor é determinístico, o log é append-only e o hash é o gate de tudo. Reescrever o
passado atacaria os três. Ramificar não ataca nenhum: **o salto é ele próprio um evento
anexado**, e a linha nova é uma reidratação do snapshot de T com esse evento inserido.

Isso já existe. Snapshot + replay determinístico é a Fase 1; reaplicar o log a partir de um
ponto intermediário é critério da Fase 3. Ramificar é usar o que o motor já sabe fazer.

## Anatomia de um branch

```
linha A  ──●───────●───────●───────●──▶   segue existindo, intocada
           │ T=ano 54
           └──────────●───────●──▶ linha B (viajante chega aqui)
```

| Propriedade | Como funciona |
|---|---|
| Origem | snapshot da linha-mãe no tick de divergência |
| Armazenamento | copy-on-write: o branch custa só a divergência, não o mundo inteiro |
| Seed | `seed_B = H(seed_A, tick_divergência, id_intervenção)` — determinismo preservado |
| Identidade | toda entidade e toda linha de log carregam `BranchId` desde a Fase 3 |
| Mãe | imutável e indiferente. O que você faz em B nunca toca A |

## Nada garantido

O salto não é um botão. É uma rolagem, e a **inércia histórica** do destino é a
dificuldade. Todos os números já são calculados pelo modelo de história degradável:

```
resistência(alvo) = f(significância, nº de testemunhas,
                      densidade de registro escrito,
                      grau causal no grafo de eventos posteriores)
```

Matar um camponês anônimo do ano 12 é barato. Impedir a fundação de Valen — 400 anos de
livros, canções e uma dinastia pendurada nela — é quase impossível.

Modos de falha, todos com consequência real: chegada no tick errado, chegada em pedaços,
máquina consumida no processo, branch natimorto, viajante preso sem retorno, e a pior —
o salto funciona e ninguém do outro lado acredita em quem você diz que é.

## Colapso de branch

Branch é barato, mas não é grátis, e nada impede mil deles. A viabilidade resolve isso
dentro da ficção: um branch precisa de **âncora** — um habitante, um viajante, um artefato,
uma consequência pendente. Sem âncora, ele deixa de persistir e é coletado.

Com teto de coleta, o custo estabiliza: ~200 branches vivos de 20 anos numa cidade de 12
mil ficam em ~370 MB. Sem coleta, cresce sem limite.

## O que isso desbloqueia

O viajante carrega conhecimento e objetos entre linhas, e é a única testemunha de um mundo
que ninguém mais viveu. Dá para **visitar** a linha que se abandonou e ver no que deu. A
perda é permanente: sem fusão de branches, o que ficou para trás ficou.

E o passado continua não sendo confiável — dentro de cada linha, a história segue se
degradando em relato. Um viajante que volta a uma linha 50 anos depois encontra livros que
contradizem a própria memória, e não tem como provar nada.

Conquistadores que operam entre linhas, artefatos que ramificam ao serem usados e cultos
que veneram uma linha perdida saem tudo do mesmo mecanismo.

## Ver também
- [historical-memory.md](historical-memory.md) — inércia histórica e degradação do passado
- [powers.md](powers.md) — custo, rolagem e falha valem para qualquer potência
- [time-and-ticks.md](time-and-ticks.md) — relógio, snapshot e agendamento
- [cosmos.md](cosmos.md) — raças e artefatos capazes de manipular tempo
- [simulation-lod.md](simulation-lod.md) — branch inativo cai para resolução agregada
