# Fase 5 — Economia — Context

**Gathered:** 2026-07-27 (modo autônomo — sem usuário disponível; gray areas resolvidas pelo
agente e registradas como `AD-NNN` em `docs/decisions-log.md`)
**Spec:** `.specs/features/phase-05-economy/spec.md`
**Status:** Ready for design

---

## Feature Boundary

A vila produz, estoca, consome e negocia. Recursos e dinheiro são inteiros e conservados;
escassez vira preço, preço vira fome, fome vira pressão social. Ver `spec.md` para o escopo
completo (29 requisitos ECON-01..29) e a tabela "Fora do escopo".

---

## Implementation Decisions

### Identidade do local econômico (`Workplace`)

- Um único tipo `Workplace` cobre produção, estoque e mercado — o papel é decidido pelo
  `LocationType` do catálogo do cenário (recipe declarada = produz; flag de mercado = também
  precifica), nunca uma subclasse por papel.
- Id novo (`WorkplaceId`, long monotônico), não reusa `LocationId` (Guid) reservado pela
  AD-024 para o modelo de cidade da Fase 8.
- Existe um único `Workplace` de mercado por assentamento nesta fase (cidade ainda não é
  entidade real) — múltiplos mercados por cidade fica para quando cidade existir de verdade.

### Modelo de dinheiro e emprego

- `Money` vive em dois lugares: `Npc.Wallet` (saldo pessoal) e `Workplace.Treasury` (caixa do
  negócio, alimentada pela venda da própria produção).
- O empregador que paga salário é o `Workplace`, não um NPC-proprietário — propriedade de
  negócio (dono, lucro, herança) fica fora do escopo desta fase.
- Vínculo de emprego é `Npc.Employer : WorkplaceId?`, simétrico ao `Npc.Household`.

### Transação atômica

- A lista de passos (`MarketTransaction.Steps`) é um array ordenado, público, construído
  explicitamente — nunca uma lista de índices mantida à mão em um teste separado. O teste de
  fault-injection itera `Steps.Count`, então um passo novo entra na cobertura sozinho (mesma
  filosofia de `ActionCatalog.Create` e `ReferentialIntegritySweep`).
- Os quatro efeitos de uma compra/venda (débito comprador, crédito vendedor, débito estoque
  vendedor, crédito estoque comprador) aplicam todos ou nenhum — nunca parcial.

### Consumo (elo com a Fase 4)

- `Eat` (ação já existente da Fase 4) passa a exigir 1 unidade de
  `EconomyRules.FoodResourceId`/`WaterResourceId` no estoque do `Household` do NPC antes de
  restaurar `Hunger`/`Thirst`. Sem estoque, a ação completa mas a necessidade não é restaurada
  — a Fase 4 já sabe a consequência (NEEDS-03, morte por fome sustentada).
- Reabastecer o `Household` é a nova ação `Buy` (viagem a um `Workplace` de mercado +
  transação atômica) — não existe um sistema de consumo separado, o hook de conclusão de ação
  do `BehaviorDecisionSystem` (Fase 4) é reaproveitado.

### Agent's Discretion

- Fórmula exata de sensibilidade de preço (`EconomyRules.PriceSensitivity`) e a curva de
  `EstoqueOfertado / DemandaEstimada` — qualquer fórmula monotônica (preço sobe quando a razão
  cai) satisfaz ECON-23/24/25; a Design escolhe uma concreta.
- Quais profissões têm recipe de produção declarada além de agricultor/lenhador (que produzem
  a partir de recurso natural de célula) — guarda/curandeiro/professor/comerciante são
  assalariados sem produção física; ferreiro pode ou não ter recipe com insumo, a critério da
  Design/scenario JSON.
- Layout exato dos campos de `EconomyRules`/`EconomyCatalog` (nomes, agrupamento) — segue o
  padrão de `NeedsRules`/`ActionCatalog`, mas a Design decide a forma final.

### Declined / Undiscussed Gray Areas → Assumptions

Nenhuma gray area ficou sem decisão — modo autônomo resolveu todas como `AD-039..AD-045` (ver
`spec.md` § Assumptions & Open Questions e `docs/decisions-log.md`). As mais relevantes:

- Reuso vs. novo tipo de id para "local" → novo `WorkplaceId` (AD-039).
- Escopo de `ActionType.Buy` como sétima ação (AD-040).
- Cunhagem/destruição como evento raro nomeado, não sistema periódico (AD-042).

---

## Specific References

Nenhuma referência de produto específica — a fase segue integralmente
`docs/roadmap/phase-05-economy.md` e `docs/domain/economy.md`, sem "quero que pareça X"
adicional (não houve sessão de discussão interativa; modo autônomo).

---

## Deferred Ideas

- Rotas comerciais entre cidades e migração puxada por diferencial de preço → Fase 8
  (roadmap explícito).
- Habilidade/produtividade que melhora produção por ofício → Fase 6 (roadmap explícito).
- Imposto recorrente, tesouro de governo, política fiscal → Fase 10+ (política/governo).
- Propriedade de negócio por NPC (dono, herança, lucro) e propriedade de terra → escopo de
  propriedade mais amplo, não pedido pelo roadmap desta fase.
- Poupança e dívida como decisão comportamental do NPC → `economy.md` cita como elemento de
  domínio; nesta fase o NPC só tem saldo, sem lógica de poupar/pedir emprestado.
