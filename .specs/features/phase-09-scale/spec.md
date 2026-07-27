# Fase 9 — Escala e armazenamento — Specification

## Problem Statement

O motor escala linearmente, mas paga por **NPC que já existiu**, não por NPC vivo, e aloca
centenas de bytes por NPC-tick — o que faz o GC pagar proporcional ao heap. Medido em Release,
cenário default (economia ligada, sistemas das Fases 4/5), 1 ano-sim = 8640 ticks, seed 42:

| pop inicial | vivos ao fim | µs/tick | alloc/ano | GC0 | snapshot | hash | sweep |
|---|---|---|---|---|---|---|---|
| 100 | 38 | 34,8 | 160 MB | 14 | 0,1 MB | 9 ms | 16 ms |
| 1.000 | 121 | 151,5 | 913 MB | 79 | 1,2 MB | 53 ms | 32 ms |
| 5.000 | 135 | 613,9 | 2,6 GB | 225 | 5,8 MB | 240 ms | 119 ms |

Ajustando as duas linhas maiores a `custo = a·entidades + b·vivos`: **a ≈ 0,12 µs por entidade
por tick** e b ≈ 0,3 µs por vivo. Ou seja: 5.000 NPCs dos quais 135 vivos custam 4× mais que
1.000 dos quais 121 — o morto não decide nada e continua sendo iterado, serializado e mantido no
heap. Custo por sistema (pop 1.000, 1 ano): `behavior-decision` 85,6 µs/tick e **1.257 MB** de
alloc, `needs-decay` 26,8 µs/tick, `mortality`+`natality` 1,1 µs/tick, contadores 0,1 µs/tick.

Armazenamento e memória: snapshot JSON ~900 B por NPC (mortos são >50% das entradas em 2 anos-sim),
RAM residente ~5,6 KB por entidade, event log 350–900 B por NPC vivo por ano, e `RngStreams` com
~2 streams por NPC já nascido — todos no snapshot **canônico**, nunca liberados.

Extrapolando para 10k vivos por 100 anos (população estável, expectativa ~40 anos ⇒ ~25k mortos
acumulados): **~1,4 h de CPU** e, com snapshot mensal de mundo inteiro, ~1.200 × 30 MB ≈ **35 GB**
em disco. O objetivo #1 (100 NPCs) passa folgado; escala não.

## Goals

- [ ] Custo do tick proporcional a **decisão tomada**, não a NPC existente: ≤ 500 µs/tick com 10k
      vivos (100 anos-sim em ~7 min de CPU, contra ~1,4 h hoje).
- [ ] Alocação ≈ 0 por NPC-tick em regime permanente (só nascimento/morte alocam).
- [ ] Snapshot ≤ 100 B por NPC vivo, gravando só o que sujou; **≤ 1 GB** para 10k NPCs × 100 anos
      somando snapshot + log, contra ~35 GB hoje.
- [ ] Bytes por NPC vivo por ano **independentes da idade do mundo** — ano 100 custa o mesmo que
      o ano 1.
- [ ] Todo ganho é medido pelo mesmo sensor antes e depois; nada entra por intuição.

## Out of Scope

| Item | Motivo |
|---|---|
| LOD de materialização (agregado vs indivíduo) | Fase 8, task 6 — esta fase abarata o NPC materializado, não decide quem é materializado |
| LOD por observação (simular menos onde ninguém olha) | Depende de "quem observa": Fase 25 + ADR-0012 |
| Postgres, sharding por branch, catch-up de branch dormente | Persistência não é o gargalo medido (zero round-trip no tick já é invariante da Fase 3); branch é Fase 20 |
| Reescrever `Npc` como struct-of-arrays (ECS) | Só entra se PERF-02..11 não fecharem o teto — decisão medida (PERF-17), não antecipada |
| Paralelizar `behavior-decision` | Lê e escreve estoque de `Household`/`Workplace`: não é independente de ordem (ver PERF-15) |
| Otimizar carga de cenário / geração de mapa | Setup custa 273 ms a 5.000 NPCs, uma vez por run |
| Rebalancear demografia/economia do cenário default | Risco já registrado no `STATE.md`; aqui só entra o cenário de escala do sensor (PERF-01) |

---

## Requirements

### Bloco A — sensor (pré-requisito de tudo)

- **PERF-01**: cenário de escala **com demografia estável** (o default colapsa para ~130 vivos
  saindo de 1.000 ou 5.000 — medir nele é medir NPC morto). Sem população estável o sensor mede
  a coisa errada.
- **PERF-02**: sensor no gate: 1 mês-sim em duas populações do cenário, reprovando se
  µs/NPC-vivo-tick, bytes alocados/NPC-tick ou bytes de disco/NPC-vivo/ano passarem do teto
  declarado. Segundos de execução — 100 anos segue manual.
- **PERF-03**: tetos de tempo e de bytes são **dado de cenário** (R3), não constante de teste.

### Bloco B — constante por NPC-tick (hash-idêntico, golden intactos)

- **PERF-04**: nenhuma closure/delegate por NPC-tick na seleção de ação
  (`BehaviorDecisionSystem.ResolveWithStepCap`, hoje ~145 B/NPC-tick ⇒ 1,2 GB/ano a 1.000 NPCs).
- **PERF-05**: `NearestMarket` sem cadeia LINQ por NPC-tick (chamada até 3× por NPC-tick) —
  índice de mercado por célula/região calculado uma vez por tick.
- **PERF-06**: `MarketPricingSystem` calcula população por região uma vez por tick (hoje
  O(mercados × NPCs)/dia); `EmploymentSystem` não materializa `Npcs.OrderBy(...)` duas vezes por
  dia nem varre `Workplaces` por NPC; `ProductionSystem` não copia `Stock` por workplace por dia.
- **PERF-07**: sistemas `Hourly` iteram índice de **vivos**, não `world.Npcs` — é o termo
  `a ≈ 0,12 µs/entidade/tick` medido. Índice derivado, reconstruído na rehidratação, fora do hash.

### Bloco C — estrutural (muda o mundo; exige AD + golden regenerado)

- **PERF-08**: decisão por evento agendado: o NPC acorda no tick em que a ação termina ou em que
  a próxima necessidade cruza o limiar — decaimento linear determinístico ⇒ esse tick é **fórmula
  fechada**. Alvo: O(decisões)/tick.
- **PERF-09**: decaimento preguiçoso — necessidade derivada de `(valor, tick da última mudança,
  taxa)` em vez de 4 escritas por NPC por hora. Conjunto com PERF-08. Morte por fome continua no
  mesmo tick de hoje.
- **PERF-10**: NPC morto há mais de N anos-sim (N do cenário) sai do estado quente para tier-2
  frio; event log vira resumo periódico. Mesmo compromisso do ADR-0007 aplicado a NPC: custo por
  ano independente do tempo decorrido. Id referenciado por vivo nunca é arquivado.

### Bloco D — armazenamento

- **PERF-11**: snapshot binário posicional (hoje JSON ~900 B/NPC) com **delta por entidade suja**
  desde o snapshot anterior, e full periódico declarado no cenário.
- **PERF-12**: hash canônico incremental — hash por entidade com marca de sujo, combinado em
  ordem de id. Resultado **byte-idêntico** ao hash recomputado do zero (teste compara os dois
  caminhos no mesmo mundo).
- **PERF-13**: `WorldRngRegistry` não retém stream de rolagem única (`mortality-*`,
  `personality-*`, `profession-*`): derivado de `(seed raiz, propósito, id)` e descartado. Mesma
  sequência de números, snapshot O(vivos).
- **PERF-14**: `EventScheduler.Schedule` não reordena o bucket por inserção e `Cancel` não varre
  todos os buckets — índice por id (a fila cresce com PERF-08, que agenda por NPC).

### Bloco E — paralelismo (só com prova)

- **PERF-15**: decaimento pode paralelizar sobre partição estável de id, com teste provando hash
  idêntico ao sequencial em 3 tamanhos de partição. Outro sistema exige duas fases (pontuar em
  paralelo, aplicar em sequência por id) e prova equivalente.

### Bloco F — disciplina

- **PERF-16**: sensor roda antes e depois de cada task; teto do cenário é apertado junto.
- **PERF-17**: task cujo ganho medido não justifica o diff é revertida. ECS/SoA só entra na fila
  se A–E não fecharem o teto.

---

## Verification Criteria

1. Golden hashes **inalterados** nos Blocos B, D e E; alterados com AD e regenerados na mesma
   task no Bloco C, com par ligado/desligado provando que a mudança entrou na conta.
2. Determinismo entre dois processos verde após **cada** task (índice novo é a forma mais fácil de
   reintroduzir ordem de dicionário no caminho quente).
3. Round-trip binário → mundo → binário byte-idêntico; hash incremental == hash do zero.
4. Sweep referencial verde a 10k NPCs após PERF-07/PERF-10.
5. 100 anos com população estável mantendo bytes/NPC-vivo/ano dentro do teto (`Category=Scenario`,
   manual) — a curva de disco não cresce com a idade do mundo.
6. Orçamento provado uma vez: 10k NPCs × 10 anos dentro do teto de tempo e disco, execução manual
   registrada no `STATE.md`.

## Assumptions & Open Questions

| Questão | Default proposto | Confirmado? |
|---|---|---|
| Alvo de escala | 10k vivos a ≤ 500 µs/tick e ≤ 1 GB para 100 anos | n |
| Ordem de execução | A → B → D → C → E (sensor primeiro; C muda hash, então depois de B/D estabilizarem) | n |
| Formato binário | Posicional próprio (sem dependência nova); JSON sobrevive como export de debug | n |
| Onde vive o tier-2 frio | Mesma base, tabela separada (não arquivo à parte) | n |
| Teto de alloc/NPC-tick | 0 B em regime permanente | n |
