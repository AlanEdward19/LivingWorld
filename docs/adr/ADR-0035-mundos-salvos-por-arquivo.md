# ADR-0035 — Mundos salvos em SQLite por mundo

## Status
Aceito pelo usuário em 2026-10-15, issue #67.

## Contexto
Criar outro mundo substitui o mundo atual e seu único banco.
O menu não permite listar nem recuperar mundos anteriores.

## Decisão
Cada mundo usa `worlds/<worldId>.db`, preservando snapshots e eventos.
A identidade pública continua derivada da seed pelo contrato existente.
Criar outra vez com seed já salva retorna 409; carregar recupera o existente.
Antes de trocar, salva-se o mundo anterior e pausa-se o relógio.
As operações de catálogo e persistência acontecem fora do tick.
O mundo atual e o catálogo sobrevivem ao reinício do servidor.
O banco legado é preservado e seu mundo atual é incorporado ao catálogo.

## Contrato aprovado
- GET /worlds: 200, lista por último salvo com worldId, name, seed,
  createdAt (data inicial), currentDate, population e lastSavedAtUtc.
- POST /worlds/{worldId}/load: 200 ou 404, sem troca em caso ausente.
- DELETE /worlds/{worldId}: 204, 404 ou 409 se for o atual.
- POST /worlds/create: acrescenta 409 para seed já salva.
- Cliente lista mundos, carrega e exige confirmação antes de apagar.

## Alternativa
Banco único com tabela de mundos foi rejeitado pelo usuário.
IDs por criação foram rejeitados para preservar o contrato por seed.

## Consequências
Não é permitido apagar o mundo atual; nenhum caminho vem do cliente.
Metadados operacionais UTC ficam fora do estado determinístico do mundo.
Não há migração de schema aplicada alterada nem dependência nova.
O repositório por mundo preserva eventos ao salvar novamente no tick zero;
o modo legado de slot único mantém seu reset original.
