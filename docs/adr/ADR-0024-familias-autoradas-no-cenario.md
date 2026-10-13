# ADR-0024: Famílias autoradas no cenário

- Status: aceito (pedido do dono do projeto em 2026-10-13)

## Contexto

O motor gera a população inicial sorteando tudo (`PopulationGenerator`). O cliente Godot precisa deixar o
usuário criar famílias e pessoas específicas no novo mundo, no estilo The Sims. Hoje não há contrato para isso.

## Decisão

Estender o cenário com o membro opcional `AuthoredHouseholds` (forma em `.specs/features/authored-households/plan.md`).
Validado na borda por `AuthoredHouseholdsLoader`; construído no Domain por `GenerateAuthored`; só no CRIAR mundo.
Campos não autorados (genes, corpo, vitalidade) continuam sorteados pelos streams derivados por NPC, então a
mesma seed e o mesmo bloco dão o mesmo mundo. Sem o bloco, nada muda (hash canônico preservado).

## Alternativas rejeitadas

- Rota `POST /npcs` com o mundo rodando: muda estado vivo, log causal e hashes; fica para uma fase posterior.
- Cliente escrevendo NPCs direto no estado: viola ADR-0005 e ADR-0022.

## Consequências

Contrato público de cenário cresce (aditivo, retrocompatível). O menu passa a gerar esse bloco em texto cru.
