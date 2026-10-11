# Snapshot binário posicional e delta real — Specification

> **Spec, não gate. Sem implementação.** Origem: Fase 9 (T6, PERF-12) e Fase 28 (T2). AD-070 deixou o
> hasher incremental como stub; a auditoria achou `BinarySnapshotWriter` sem consumidor em produção.

## Problem Statement

`BinarySnapshotWriter` é binário só no nome: magic mais JSON UTF-8 com prefixo de tamanho, e `WriteDelta`
ainda serializa o mundo inteiro (`BuildFieldDiffJson`). A persistência (`PersistentWorldRunner`) usa
`WorldSnapshot.Serialize` em JSON; `IncrementalHasher.CombineIncremental` não tem chamador. Medido
(`scale-sensor.json`): 131.737 B/NPC/ano com 1.000 NPCs e 149.661 com 5.000; snapshot de cerca de 10.978 B por
NPC vivo. A meta da Fase 9 é no máximo 50 B/NPC/ano em disco.

## Goals

- [ ] **Codificação posicional**: esquema versionado por tipo (campos na ordem declarada, inteiros varint, strings
      internadas por tabela), gerado por reflexão sobre `[Canonical]`, o mesmo mecanismo que já cobre o hash —
      campo novo não fica de fora.
- [ ] **Delta real**: só NPCs/cidades sujos (dirty set por `TouchCanonical`) vão para o diff; o resto é referência
      ao snapshot-base. `ReadAndApply` reconstrói byte a byte.
- [ ] **Hash incremental real**: combina hashes por entidade e recalcula só os sujos; igual ao hash do zero.
- [ ] **Ligado na persistência**: `PersistentWorldRunner` grava base mais diffs; `LoadLatestSnapshot` reaplica.
- [ ] **Migração**: snapshots JSON antigos continuam legíveis (o leitor lê os dois; a escrita nova é só binária).

## Out of Scope

Postgres (ADR futuro); compressão do log de eventos (Fase 28-T1, spec própria); mudar o hash canônico (o formato
de arquivo muda, o hash do mundo não — golden idêntico é critério).

## Requisitos

| ID | Requisito |
| --- | --- |
| SNB-01 | Round-trip binário→mundo→binário byte-idêntico em 10 anos, 20 seeds. |
| SNB-02 | `CanonicalHash` do mundo restaurado igual ao do original (golden inalterado). |
| SNB-03 | Hash incremental igual ao hash do zero, a cada N ticks, em 10 anos. |
| SNB-04 | Campo `[Canonical]` novo sem codificação reprova (teste por reflexão). |
| SNB-05 | Bytes/NPC/ano até 50% do baseline atual na 1ª etapa; meta final de 50 B (Fase 9). |
| SNB-06 | Zero round-trips ao banco durante o tick (`ZeroRoundTripsTests` segue verde). |
| SNB-07 | Dois processos: bytes idênticos para a mesma seed e os mesmos ticks. |

## Etapas sugeridas (cada uma entrega e mede)

1. Esquema posicional mais leitura dos dois formatos, sem delta. Meta: −50% de bytes.
2. Dirty tracking e delta; `WriteDelta` deixa de serializar o mundo todo.
3. Hash incremental com cache por entidade (inclui as `HotProperties` hoje não cacheadas: Relationships, Households).
4. Ligar na persistência e remover o caminho JSON de escrita.

## Medição obrigatória

Antes e depois de cada etapa: bytes/NPC/ano e bytes/NPC vivo (sensor), tempo de salvar/carregar 1.000 e 10.000 NPCs,
µs/NPC-tick (não pode subir: o salvamento é fora do tick, mas o dirty tracking toca `Npc` no caminho quente, então
medir `TouchCanonical`), alocação/tick e `LongRunScaleTests`. Mudar o formato persistido exige **ADR**.
