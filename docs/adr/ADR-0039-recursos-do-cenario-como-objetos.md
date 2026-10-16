# ADR-0039: Recursos do cenário como objetos (efeito ao consumir vem do descritor)

- **Status**: aceito
- **Data**: 2026-10-16
- **Decisores**: Alan (issue #47, fatia 4; modelagem e critérios aprovados em chat antes do código)

## Contexto
Comer restaurava fome e sede sempre a 100, fixo em `TryApplyEat`. Recursos (`Resources[]`: `Edible`, `Preparation`) não tinham forma nem efeito próprio.
A #47 quer qualquer objeto, comida inclusive, descrito como dado. A fatia exige refatoração sem mudança de regra: com descritores equivalentes aos valores de hoje, o mundo não muda.

## Decisão
- **Um catálogo só**: o descritor de objeto ganha `resourceId` (int > 0, opcional, único por recurso). Objeto com `resourceId` descreve o recurso; forma obrigatória como em todo objeto.
  Campo opcional e omitido do snapshot quando nulo; `objectsFormat` continua 1.
- **Efeito ao consumir**: tokens `alvo:magnitude` com os alvos `hunger` e `thirst`, valendo **nível de 0 a 100** a que a necessidade é restaurada ao consumir 1 unidade.
  Objeto de recurso só aceita esses alvos (um de cada); objeto sem `resourceId` só aceita `sleep`/`eat` (ADR-0038).
- **Motor**: `TryApplyEat` lê `ObjectUse.ConsumeLevel(recurso, alvo)` e aplica `max(necessidade atual, nível)`: consumir nunca piora a necessidade. Sem catálogo, sem descritor ou sem o alvo, o nível é 100
  (a regra de sempre, e `max(x, 100) == 100`), então o hash de mundos sem `Objects` e a regra com descritores equivalentes não mudam. Consumo de estoque, `ResourceConsumed`,
  `Edible`, `Preparation` e a escolha da refeição (`FoodResolver`) ficam como estão; a conservação de dinheiro e estoque não muda.
- **Mobília padrão**: comida (recurso 1, `hunger:100`) e água (recurso 2, `thirst:100`) entram em `DefaultObjects` como paridade, com comportamento neutro.
- **Sem campo novo no snapshot de interior**; o cliente já ignora o que não conhece (os dois recursos viajam no catálogo).

## Alternativas consideradas
- Bloco `Resources` separado — dois catálogos e dois loaders para o mesmo conceito de "objeto".
- Efeito como multiplicador (como `sleep`/`eat`) — fome e sede não têm duração; o nível restaurado é o que muda hoje (fixo em 100).
- Tornar `Edible` derivado do descritor — mudaria a paridade (trigo cru não restaura fome por regra de cenário); fica para quando houver consumidor.

## Consequências
- **Positivas**: comida e água passam a ser dado (forma e efeito); cenários e mods podem declarar refeições que saciam menos ou mais, sem código.
- **Negativas / trade-offs**: o nível vale por unidade consumida, sem estado de qualidade por lote; o efeito não depende de quem come.
- **Follow-ups**: invenções de NPC e mods compõem objetos a partir destas partes (fatias 5 e 6); `Edible`/`Preparation` como efeitos do descritor.
