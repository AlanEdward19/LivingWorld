# ADR-0002: SQLite agora, Postgres depois

- **Status**: aceito
- **Data**: 2026-07-26
- **Decisores**: Alan

## Contexto
O objetivo técnico #1 é rodar 100 NPCs por 100 anos num único processo, sem jogadores
simultâneos e sem worker concorrente. Exigir um servidor de banco para rodar um teste
adiciona atrito diário sem entregar nada que o MVP precise. Ao mesmo tempo, o alvo de
longo prazo — múltiplos workers, vários jogadores, event store grande — pede Postgres.

## Decisão
Vamos usar **SQLite via EF Core** agora e migrar para **Postgres** quando houver escrita
concorrente real. Para que a migração seja barata, vale a regra: **nenhum recurso
exclusivo de SQLite**. Nada de tipagem frouxa, `AUTOINCREMENT` implícito ou funções de
data do SQLite. O que não existe em Postgres não entra no schema.

Um mundo é um arquivo `.db` — dá para versionar, copiar e comparar snapshots.

## Alternativas consideradas
- **Postgres desde o início** — evitaria a migração, mas exige Docker/servidor no
  caminho de qualquer teste, e concorrência ainda não é um problema real do MVP.
- **Só em memória + snapshot em arquivo** — mais rápido para simular 100 anos, mas perde
  consulta ad-hoc do mundo, que é exatamente o que o objetivo técnico #2 (inspecionar
  qualquer NPC vivo) precisa.

## Consequências
- **Positivas**: zero infra para desenvolver e testar; um mundo = um arquivo; suíte de
  testes roda contra banco real, não mock.
- **Negativas / trade-offs**: uma migração de provider no futuro; SQLite trava a escrita
  no arquivo, então não há worker concorrente até a troca; sem tipos ricos do Postgres.
- **Follow-ups**: manter a suíte de integração capaz de rodar contra os dois providers
  antes de virar a chave. Novo ADR na migração efetiva.
