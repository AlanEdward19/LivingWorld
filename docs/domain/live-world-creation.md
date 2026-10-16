# Criar família e local de trabalho com o mundo rodando

Decisão em [ADR-0033](../adr/ADR-0033-criacao-ao-vivo-de-familia-e-local-de-trabalho.md) (issue #46, fatia 4). O corpo das rotas é o
mesmo JSON do cenário, validado pelos mesmos loaders (ver [aparência e habilidades](period-authoring-appearance.md)).

## Rotas

| Rota | Corpo | Resposta 200 |
|---|---|---|
| `POST /authoring/households` | um item de `AuthoredHouseholds[]`: `Settlement?`, `Members[]` (`Name`, `Sex`, `AgeYears`, `ProfessionId`, `Personality?`, `Appearance?`, `Skills?`, `Spouse?`, `Mother?`, `Father?`) | `{ householdId, cityId, npcIds[] }` |
| `POST /authoring/workplaces` | um item de `Workplaces[]`: `LocationTypeId`, `X`, `Y`, `MaxVacancies`, `Treasury`, `Stock`, `Prices` | `{ workplaceId, buildingId?, cityId, x, y }` |

Erros: **403** sem `X-Admin-Token` válido; **409** com pausa **de admin** ativa (a pausa comum não impede); **400** `{ error }` com o campo
(`...Members[0].ProfessionId: fora do catálogo do período`, `Workplaces[].X/Y: fora do mapa`, `MaxVacancies`, `Treasury`).
Toda chamada, aceita ou recusada, entra na auditoria de admin (`GET /admin/audit`).

## O que acontece

- **Família**: vai para o assentamento nomeado (precisa ter cidade) ou para a cidade mais antiga; a cultura é a mais comum dali. Casa, agenda de
  óbito e despertar saem do mesmo caminho da geração inicial. Com a economia ligada ganham comida e água no domicílio e 50 moedas por pessoa,
  **cunhadas** (`Minted`), para a conservação do dinheiro seguir valendo.
- **Local de trabalho**: edifício na cidade mais próxima, na célula pedida se livre, senão onde o posicionador achar espaço (o mesmo código do
  cenário, `WorkplacePlacer`). O caixa inicial também é cunhado.
- **Origem**: `AuthoredImmigration` (`domicilioId|cidadeId|npcIds`) e `AuthoredWorkplaceFounded` (`localId|cidadeId|tipo|x|y`) entram no log
  causal; a apresentação diz "Uma família chegou à cidade" e "Um novo local de trabalho foi fundado".
- **Depois**: a família come, trabalha, envelhece e morre pelas regras de sempre; o local entra na economia e no mercado de trabalho.

## Garantias

Criação válida muda o hash e aparece no log. Inválida **não escreve nada** (nem evento): hash igual, motivo só na auditoria. Mesma seed + mesmo
comando no mesmo tick = mesmo mundo, em dois processos.

## Cliente

Console de admin (F8, exige `LW_ADMIN_TOKEN`), aba **Criar**: "Criar família…" abre o criador de pessoas do menu (genes de aparência, habilidades
e prévia 3D) e envia cada família; "Fundar local de trabalho" pede tipo, célula, vagas e caixa.
