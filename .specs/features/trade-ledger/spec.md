# Livro de trocas — Specification

> **Spec, não gate. Sem implementação.** Origem: roadmap F5-T8 ("toda transação vai para o event log")
> e `RelationshipEventType.Trade`, que nunca é disparado. AD-089.

## Problem Statement

`TryApplyBuy` (`BehaviorDecisionSystem.Effects.cs`) executa `MarketTransaction` e muda 4 saldos sem
gravar evento: a história não sabe quem comprou o quê, e o evento de relação `Trade` não tem fonte.
Gravar um evento por compra pode inchar o log (compra é ação diária por NPC). O teto de bytes/NPC/ano
(`PerfRules`; medido 131.737 B/NPC/ano no fixture de 1.000) manda **medir antes** de escolher entre evento
por compra e agregado.

## Goals

- [ ] **Registro verdadeiro das trocas** sem estourar o orçamento de armazenamento.
- [ ] **Trade vira relação**: hoje o vendedor é `Workplace.Treasury` (AD-044, sem dono NPC). Define-se a
      contraparte: o empregado que atende a oficina, escolhido de forma determinística e documentada.
- [ ] **Consumidor causal**: preço percebido por NPC (lembrança do preço pago), confiança no vendedor
      que atende, e consulta histórica ("quanto custou o trigo no ano X") pela API de inspeção.

## Desenho candidato (decidir com medição)

| Opção | Custo | Fidelidade |
| --- | --- | --- |
| A. Evento `Trade` por compra | Alto: cerca de 1 evento/NPC/dia | Total |
| B. Agregado diário por (oficina, recurso): quantidade, valor, nº de compradores | Baixo, O(oficinas) | Sem par comprador↔vendedor |
| C. B mais evento só para compra relevante (valor acima do p90 da oficina, ou primeiro contato) | Médio | Alta onde importa |

Recomendação inicial: **C** (cânone limitado por comunidade, ADR-0007).

## Requisitos

| ID | Requisito |
| --- | --- |
| TRD-01 | Toda compra bem-sucedida tem o registro correspondente (evento ou agregado); teste por soma. |
| TRD-02 | Compra recusada não grava nada e deixa o hash idêntico (estende o teste de `MarketTransaction`). |
| TRD-03 | Bytes/NPC/ano do cenário padrão sobe no máximo 10% sobre `scale-sensor.json`. |
| TRD-04 | `ApplyEvent(Trade)` ocorre só com contraparte NPC; deltas declarados no cenário (hoje 0). |
| TRD-05 | O livro agregado soma ao total de moeda movimentada pelo `MarketTransaction` (conservação). |
| TRD-06 | Mesma seed, dois processos: livro idêntico. |

## Out of Scope

Comércio entre cidades; imposto/tesouro (`Mint/Destroy` ainda sem fonte, spec separada); crédito.

## Medição obrigatória

Antes: contar compras/NPC/dia no `MeasureHarness` e projetar bytes de evento. Depois: µs/NPC-tick (no máximo +3%),
alocação/tick, bytes/NPC/ano e população no ano 10. A compra é caminho quente: não pode alocar por chamada.
