# Fase 17 — Linhas temporais

**Objetivo**: viagem no tempo não reescreve nada — ela **ramifica**. Um salto ao tick T é um
evento anexado que abre uma linha nova a partir do snapshot de T, com seed derivada e
armazenamento copy-on-write. A linha-mãe segue existindo, intocada e indiferente — e é
exatamente isso que o gate desta fase protege acima de qualquer outra coisa.

> **Spec, não gate.** Os critérios abaixo são a intenção; os critérios finais são escritos
> sob `rules/eval-criteria.md` quando a fase for ativada. Não comece esta fase antes da
> Fase 9 fechar.

## Tasks
1. **Salto como evento anexado** ao log da linha de origem (ADR-0006): nenhum `UPDATE`,
   nenhuma reescrita. O evento carrega tick alvo, intervenção pretendida e resultado da
   rolagem — inclusive quando o salto falha.
2. **Branch por copy-on-write sobre o snapshot da mãe**: o filho referencia o estado de T e
   grava só o que diverge. `BranchId` já está no esquema desde a Fase 3 (ADR-0009); esta
   fase o usa, não o introduz.
3. **Linhagem de seed**: `seed_B = H(seed_A, tick_divergência, id_intervenção)`, função pura
   e estável entre processos e versões. Sem ela não há determinismo entre linhas.
4. **Rolagem contra a inércia histórica** pelo primitivo único (ADR-0011), perfil
   `Dramático`. A dificuldade é `f(significância, testemunhas, densidade de registro
   escrito, grau causal)` — tudo já calculado pelo modelo da Fase 9. Nenhuma fórmula nova.
5. **Modos de falha com consequência**, nunca no-op: chegada em tick errado, chegada
   danificada, máquina consumida no processo, branch natimorto, viajante preso sem retorno.
   Sucesso parcial é resultado de primeira classe — chegou, mas alguém viu.
6. **Âncora e coleta**: um branch persiste enquanto tiver habitante, viajante, artefato ou
   consequência pendente. Sem âncora, é coletado. Teto de branches vivos declarado no
   cenário; a coleta é determinística e ordenada, nunca varredura oportunista.
7. **Árvore de branches consultável** por API e CLI, somente leitura: origem, tick de
   divergência, intervenção, âncoras e estado de cada linha. É ela que torna coleta e custo
   mensuráveis sem instrumentar o motor por dentro.
8. **Cenário `test-branching` pareado**: o mesmo mundo com e sem salto, mais um conjunto de
   alvos que varia **um** fator de inércia por vez, para servir de braço de controle.

## Critérios de verificação
- **A mãe fica byte-idêntica — o critério que sustenta a fase**: capturado o hash canônico
  de A no tick de divergência, nada feito em B (ticks, mortes, guerra, coleta do próprio B)
  o altera. Assert a cada tick em 10 anos no gate; 100 anos em nightly. Uma única
  divergência reprova a fase inteira.
- **`UPDATE` na mãe é transição rejeitada**: a escrita retroativa real no log de A retorna
  `Failure` **e** deixa `Hash(A)` inalterado. Par de mutação: desligar a proteção por flag
  de teste tem de **fazer este critério falhar** — senão ele não media nada.
- **Ramificação reprodutível entre processos**: mesma origem, mesmo tick, mesma intervenção
  → hash canônico do branch idêntico, comparado em **dois processos** separados. Repetir no
  mesmo processo não prova nada sobre estado global escondido.
- **Custo proporcional à divergência, não ao mundo**: com o número de mutações fixo pelo
  cenário, o armazenamento do branch fica dentro do baseline de 20 seeds em
  `tests/baselines/`, enquanto a população da mãe varia pelos tamanhos declarados. Custo
  que acompanha o tamanho do mundo reprova.
- **Sem âncora, sem branch**: removida a última âncora, o branch é coletado em `≤ K` ticks
  (`K` do cenário) e o total de branches vivos fica no teto do cenário, assert a cada tick
  em 10 anos no gate. Nightly roda 50, 100 e 200 anos e reprova se a regressão do total de
  branches vivos contra o tempo tiver **inclinação positiva**.
- **Inércia resiste, fator a fator**: quatro pares base/tratamento na mesma seed, cada um
  elevando **um** fator — significância, testemunhas, registro escrito, grau causal. Em
  cada par, a taxa de sucesso do salto é menor no braço tratado, 10/10 seeds. Direção, não
  magnitude; variar dois fatores juntos não distingue qual pesou.
- **Ramificação entrou na conta**: desligar o subsistema temporal por flag muda o hash
  canônico em 10 anos.

## Fora do escopo
Voltar à linha de origem, catch-up e relógio por branch: Fase 19. Fusão de branches não
existe, por decisão (ADR-0008). Contato e degrau cósmico: Fase 18. A potência que habilita
o salto: Fase 15. Culto que venera uma linha perdida: Fase 16. Prosa sobre o viajante e
sobre a linha abandonada: Fase 11.

## Questões em aberto
- Existe teto de profundidade? Nada impede ramificar de um branch que já é filho, e a
  linhagem de seed aguenta — mas a árvore, o orçamento de coleta e o debug talvez não.
- Coleta de branch é evento no log da mãe ou fato administrativo fora do mundo? Se é evento,
  entra no hash canônico e todo teste de coleta vira também teste de determinismo.
- A rolagem do salto consome o stream de quem — do viajante, do alvo, ou um stream próprio
  da linha? A resposta mexe no hash de A no tick do salto, e a mãe precisa ficar íntegra.
- Salto falho que abre branch natimorto e salto falho que não abre nada são o mesmo
  resultado do primitivo, ou `falha` e `falhaCrítica` distintas?
- O viajante que chega é materializado à força pela Fase 8, ou pode existir em agregado num
  branch que ninguém está olhando? Se pode, o que sobra da identidade dele.

## Ver também
[timelines.md](../domain/timelines.md) ·
[historical-memory.md](../domain/historical-memory.md) ·
[time-and-ticks.md](../domain/time-and-ticks.md) ·
[simulation-lod.md](../domain/simulation-lod.md) ·
[ADR-0008](../adr/ADR-0008-ramificacao-como-modelo-temporal.md) ·
[ADR-0009](../adr/ADR-0009-branchid-no-esquema-desde-a-fase-3.md) ·
[ADR-0011](../adr/ADR-0011-primitivo-unico-de-resolucao.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md)
