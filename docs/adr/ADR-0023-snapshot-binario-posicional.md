# ADR-0023: Snapshot binário posicional, base completa mais diffs

- **Status**: aceito
- **Data**: 2026-10-12
- **Decisores**: Alan (dono do projeto), com o agente de código

## Contexto
O snapshot era JSON na coluna `Json` (cerca de 130 KB por NPC vivo por ano no fixture de 1.000 NPCs, meta da Fase 9: 50 B). O
`BinarySnapshotWriter` anterior era só um envelope em torno do mesmo JSON, e `WriteDelta` serializava o mundo todo. Guardar
anos de mundo vivo custava armazenamento demais e a carga de um mundo de 10.000 NPCs levava cerca de 3 s.

## Decisão
O formato v2 (`LWSNAP2\0`, um byte de tipo, corpo) codifica a árvore canônica do mundo (as mesmas propriedades públicas de
`WorldState` que o JSON persiste) com tabela de strings única, inteiros varint zigzag, doubles em 8 bytes e arrays de objetos
de mesmas chaves como tabela (chaves uma vez). O arquivo é determinístico e a recodificação não muda um byte. O hash canônico
continua sendo calculado sobre o mundo, nunca sobre os bytes, então o golden não muda.

A persistência grava uma base completa e até 12 diffs sobre ela (`BaseTick` aponta a base). O diff é campo a campo por
entidade (`Npcs`, `Cities`...), por chave nos dicionários (`Relationships`) e por propriedade no resto, e guarda ids removidos
e a ordem quando ela muda. A base fica em memória só como impressões digitais de 64 bits (não como árvore: mais de 1 GB com 10.000
NPCs), válida só para o mesmo mundo e ticks posteriores; qualquer falha de gravação descarta a base.

Duas portas de mão única: o formato do arquivo (versão no magic) e as colunas novas `Binary` (BLOB nulo) e `BaseTick`
(INTEGER nulo) em `Snapshots`, pela migração `AddSnapshotBinary`. Linhas antigas só com JSON continuam legíveis.

## Alternativas consideradas
- Codec por reflexão campo a campo sobre `[Canonical]` (o plano original) — duplicaria o mapa que o serializador JSON já
  mantém e ficaria sem cobertura automática de campo novo; a árvore posicional herda o conjunto de propriedades do JSON.
- JSON comprimido com gzip — não dá diff por entidade.
- Guardar a árvore da base em memória — mais de 1 GB com 10.000 NPCs.

## Consequências
- **Positivas**: 34% dos bytes por NPC por ano (1.000 NPCs) e 37% (5.000), carga cerca de 20% mais rápida que o JSON e gravação no mesmo patamar dele.
- **Negativas / trade-offs**: ler um diff reconstrói a árvore da base e do diff em memória (só ao reabrir um mundo); a meta de
  50 B/NPC/ano da Fase 9 segue distante (o resto é o estado do mundo e o log de eventos).
- **Follow-ups**: compressão do log de eventos (Fase 28 T1); Postgres.
