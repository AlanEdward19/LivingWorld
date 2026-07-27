# Fase 9 — Escala e armazenamento

**Objetivo**: o mesmo mundo, muito mais barato — **tempo por NPC-tick** e **bytes por NPC
vivo** viram grandezas com teto declarado e sensor no gate. Não fecha objetivo novo; fixa o
custo que as fases 10+ vão gastar. Alvo: **10k NPCs vivos por 100 anos** em minutos de CPU e
menos de 1 GB em disco.

## Linha de base medida (Release, cenário default, 1 ano-sim = 8640 ticks, seed 42)

| Grandeza | Hoje | Alvo da fase |
|---|---|---|
| Tempo por NPC vivo por tick | 0,6–0,9 µs | ≤ 0,05 µs (≤ 500 µs/tick a 10k vivos) |
| Alocação por NPC-tick | 150–320 B (8,4 GB/ano a 5.000 NPCs) | ~0 B em regime permanente |
| RAM residente por entidade NPC | ~5,6 KB | ≤ 500 B |
| Snapshot por NPC | ~900 B (JSON), mundo inteiro a cada snapshot | ≤ 100 B/NPC vivo, só o que sujou |
| Event log | 350–900 B por NPC vivo por ano | ≤ 50 B/NPC vivo/ano após compactação |
| Custo de snapshot a 5.000 NPCs | serializar 90 ms + hash 81 ms | ≤ 10 ms combinados |

Extrapolação do problema (10k vivos, 100 anos): hoje ~6 ms/tick × 864k ticks ≈ **1,4 h de CPU**
e, com snapshot mensal, ~1.200 × 30 MB ≈ **35 GB em disco**. As duas contas crescem com todo NPC
que **já existiu**, não com o vivo — em 2 anos-sim os mortos já são metade das entradas.

## Tasks
1. **Sensor de escala no gate** (primeira task, pré-requisito das outras): 1 mês-sim em duas
   populações declaradas no cenário; reprova se µs/NPC-vivo-tick, bytes alocados/NPC-tick ou
   bytes de disco/NPC-vivo/ano passarem do teto. Roda em segundos — 100 anos segue manual.
2. **Zero alocação no laço quente**: nenhuma closure, delegate, cadeia LINQ ou array por NPC por
   tick em nenhum sistema. Hoje o pior ofensor é a seleção de ação (~145 B/NPC-tick).
3. **Índices em vez de varredura**: índice de NPCs **vivos** para os sistemas `Hourly`; índice de
   mercado por célula, de vaga por `LocationType` e de população por região recomputados uma vez
   por tick, não por NPC. Todos derivados, reconstruídos na rehidratação, fora do hash.
4. **Decisão por evento em vez de varredura horária**: o NPC é acordado no tick em que a ação
   corrente termina ou em que a próxima necessidade cruza o limiar — o decaimento é linear e
   determinístico, então esse tick é **fórmula fechada**, não busca. Custo do tick passa a ser
   O(decisões), não O(NPCs).
5. **Decaimento preguiçoso**: necessidade deixa de ser 4 escritas por NPC por hora e passa a ser
   derivada de `(valor, tick da última mudança, taxa)`, materializada quando lida. Sem isso a
   task 4 não elimina a varredura.
6. **Snapshot binário + delta + hash incremental**: formato posicional em vez de JSON; snapshot
   grava só entidade suja desde o anterior; hash canônico combina hash por entidade (ordem de id)
   e não reprocessa quem não mudou. O hash resultante segue byte-idêntico ao de hoje.
7. **Arquivo frio de morto e compactação de log**: NPC morto há mais de N anos-sim (N do cenário)
   sai do estado quente para tier-2, e o log vira resumo periódico — o custo por ano fica
   **independente do tempo decorrido** (é o mesmo compromisso do ADR-0007 aplicado a NPC).
8. **RNG streams sob demanda**: rolagem de uso único (mortalidade, personalidade, profissão —
   ~2 streams por NPC já nascido, todos canônicos hoje) é derivada de `(seed raiz, propósito, id)`
   e descartada; só stream consumido repetidamente persiste. Mesma sequência, snapshot O(vivos).
9. **Paralelismo só onde é provado**: decaimento (aritmética pura por NPC) pode paralelizar por
   partição estável de id; qualquer outro sistema exige o padrão duas fases (pontuar em paralelo,
   aplicar em sequência por id). Sem prova de igualdade de hash, não entra.
10. **Orçamento no cenário**: tetos de tempo e de bytes viram dado do cenário (R3), não constante
    de teste — cenário sci-fi e cenário medieval podem ter orçamentos diferentes.

## Critérios de verificação
- **Ganho medido, não alegado**: o sensor da task 1 roda antes e depois de cada task e o teto do
  cenário é apertado junto; task cujo ganho medido não justifica o diff é revertida.
- **Hash-idêntico onde tem de ser**: tasks 2, 3, 6, 8 e 9 mantêm os golden hashes **inalterados**.
  Tasks 4, 5 e 7 mudam o mundo: exigem AD registrado e golden regenerado na mesma task, mais o
  par ligado/desligado provando que a mudança entrou na conta.
- **Determinismo entre dois processos** verde após cada task — índice novo é a forma mais fácil
  de reintroduzir ordem de dicionário no caminho quente.
- **Round-trip de snapshot** verde: binário → mundo → binário byte-idêntico, e hash incremental
  igual ao hash recomputado do zero (teste que compara os dois caminhos no mesmo mundo).
- **Custo independente do tempo**: 100 anos com população estável mantêm bytes/NPC-vivo/ano
  dentro do teto — a curva de disco não pode crescer com a idade do mundo (`Category=Scenario`,
  manual).
- **Sweep referencial verde a 10k NPCs** depois das tasks 3 e 7: índice de vivos e arquivo frio
  não podem deixar id referenciado por vivo fora do mundo.
- **Orçamento de escala provado uma vez**: 10k NPCs por 10 anos dentro do teto de tempo e disco,
  em execução manual registrada no `STATE.md` (o gate de rotina fica no sensor barato).

## Fora do escopo
LOD de materialização (agregado vs indivíduo) é **Fase 8**, task 6 — esta fase abarata o NPC
materializado, não redefine quem é materializado. LOD por observação (simular menos onde ninguém
olha) depende de "quem observa": Fase 25. Trocar SQLite por Postgres, sharding por branch e
catch-up de branch dormente: Fase 20 / ADR-0012. Reescrever `Npc` como struct-of-arrays (ECS) só
entra se as tasks 2–8 não fecharem o teto — decisão medida, não antecipada. GPU/compute: fora.

## Ver também
[simulation-lod.md](../domain/simulation-lod.md) ·
[time-and-ticks.md](../domain/time-and-ticks.md) ·
[ADR-0006](../adr/ADR-0006-snapshot-mais-event-log.md) ·
[ADR-0007](../adr/ADR-0007-memoria-historica-degradavel.md) ·
[ADR-0014](../adr/ADR-0014-canonico-vs-volatil.md) ·
[rules/simulation-determinism.md](../../rules/simulation-determinism.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md)
