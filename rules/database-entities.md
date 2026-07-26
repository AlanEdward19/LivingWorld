# rules/database-entities.md — carregada em: banco / entidade / migração

Persistência atual: **SQLite** via EF Core. Alvo futuro: **Postgres** (ver `docs/adr/ADR-0002`).

## Regras
- Toda mudança de schema é **migração versionada**. **Nunca** edite migração já aplicada —
  crie uma nova. `up` sempre tem `down` (ou justificativa no ADR).
- **Sem recurso exclusivo de SQLite.** Nada de `AUTOINCREMENT` implícito, tipagem frouxa
  ou `datetime()`. O que não existir em Postgres não entra — a migração é o custo.
- Entidade de persistência **não** vaza para a borda. API devolve DTO; a LLM recebe snapshot.
- `Domain` não conhece EF. Mapeamento fica em `Infrastructure` (configuração por tipo,
  não atributo dentro da entidade de domínio).
- Índice para todo campo usado em filtro/join quente (`CityId`, `HouseholdId`, `Tick`).
  Cuidado com N+1: carregue explícito, nunca lazy loading.

## Modelo de escrita do mundo
- O mundo **não** é salvo NPC a NPC a cada tick. Simulação roda em memória e persiste em
  **snapshot + event log**: snapshot periódico (configurável) e log append-only de eventos.
- **Zero round-trips de banco durante o tick.** O banco só é tocado nas fronteiras de
  snapshot. Um interceptor conta comandos e reprova o gate se algum vazar para dentro do laço.
- **Log em dois tiers desde a Fase 3.** Tier A (nascimento, morte, casamento, migração,
  fundação, guerra, invenção) permanece como esqueleto imutável. Tier B (transação, refeição,
  turno) vive em buffer circular, é agregado no fecho do ano e descartado. Ver ADR-0007.
- Event log é **imutável**. Corrigir história = novo evento compensatório, nunca `UPDATE`.
- Escrita é em lote, dentro de uma transação por tick persistido. Sem round-trip por NPC.
- População agregada (regiões distantes) persiste como contadores, não como linhas de NPC.
  Materialização de NPC cria as linhas sob demanda. Ver `docs/domain/simulation-lod.md`.

## BranchId (ADR-0009)
- Toda entidade persistida e toda linha de event log carregam `BranchId`. Chave composta e
  índices contemplam a coluna desde a primeira migração.
- Branch é parâmetro **explícito** de repositório e consulta, nunca implícito nem ambiente.
- Até a fase temporal existe um único branch (raiz). Consulta sem filtro de `BranchId`
  reprova no teste de arquitetura — é o que impede o campo de virar decoração morta.

## Invariantes que o banco deve proteger
- `Npc.BirthTick <= Npc.DeathTick` (quando morto). FK de pai/mãe aponta para NPC existente.
- Dinheiro e estoque são inteiros **não negativos** — constraint no banco, não só no código.
- Todo NPC vivo tem exatamente uma residência ou um marcador explícito de sem-teto.

## Exemplo — invariante na entidade, não no chamador
```csharp
Money(long cents) { if (cents < 0) throw new ArgumentOutOfRangeException(); }
// nunca existe Money inválido em lugar nenhum do sistema
```
