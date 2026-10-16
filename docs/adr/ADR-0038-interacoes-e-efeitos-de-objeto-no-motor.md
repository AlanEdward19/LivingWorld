# ADR-0038: Interações e efeitos de objeto aplicados pelo motor (duração de dormir e comer, conforto na decisão)

- **Status**: aceito
- **Data**: 2026-10-16
- **Decisores**: Alan (issue #47, fatia 3; escopo "mínimo vertical", efeito de duração e fator de conforto aprovados em chat antes do código)

## Contexto
A fatia 1 deu forma aos objetos (ADR-0034) e a 2 ligou uso e pose (ADR-0037). Faltava o objeto **causar** algo no mundo, só pelo motor,
sem `ActionType` novo por objeto (AD-012). Dormir define o sono para 100×eficiência do lugar (teto 100): uma cama melhor não pode
passar de 100, então o eixo de melhoria é o **tempo** para recuperar.

## Decisão
- **Descritor** (campos opcionais e aditivos; `objectsFormat` continua 1; omitidos do snapshot quando ausentes, então o hash de catálogos antigos não muda):
  - `effects`: tokens `alvo:magnitude` (formato da Fase 16). Alvos `sleep` e `eat`; magnitude finita e maior que -1.
  - `interactions`: `[{action, profession?, skill?:{id, min}}]`. Sem a lista, vale quem tem ponto de uso; com ela, só as ações listadas e quem cumpre
    profissão e habilidade mínima. "Dono" fica de fora (sem dono de objeto no mundo ainda).
- **Efeito**: `ObjectUse.DurationHours` divide a duração da ação (catálogo de ações) por (1 + magnitude), mínimo 1 h, para Sleep e Eat quando o NPC está na
  célula de sono do domicílio e a moradia (`Household.Dwelling`) tem, na mobília do seu tipo, o primeiro objeto que atende à ação e que ele pode usar. O agendador
  de acordar, a conclusão da ação e as apresentações usam a mesma função. A recuperação (`SetSleep`, refeição, estoque) continua nas regras de sempre: o objeto só
  muda **quando** elas valem, por isso conservação de dinheiro e estoque não muda.
- **Decisão (oportunidade)**: `DecisionContext.Comfort` (`ObjectComfort`, 2 floats) carrega a magnitude da moradia; a utilidade de Sleep e Eat é multiplicada por
  (1 + conforto). Sem catálogo é zero e o produto é exato (×1,0): scoring e hash idênticos aos de antes.
- **Projeção**: `usingObject` (ADR-0037) passa a respeitar `interactions`, com a mesma função `ObjectUse.Serving` do motor.
- **Mobília padrão** sem efeitos: os mundos criados pelo menu não mudam de comportamento; o autor liga os efeitos no cenário.
- **Desempenho**: catálogo indexado por id e por tipo de prédio (sem texto nem enumerador por chamada); nada no tick quando não há catálogo.

## Alternativas consideradas
- Teto acima de 100 para o sono — quebra a invariante 0-100 das necessidades.
- Decaimento mais lento depois de acordar — exigiria um multiplicador por NPC como estado canônico novo.
- Estado por instância (desgaste), habilidade ganha e estoque como efeito — fatias seguintes; pedem entidade de objeto no mundo.

## Consequências
- **Positivas**: objeto novo muda o mundo por dado (cama que poupa horas de sono, mesa que acelera a refeição), com rastro determinístico e sem código por objeto.
- **Negativas / trade-offs**: o objeto é o do **tipo** de prédio da moradia, não de uma peça física; o efeito vale em casa. Mundo com efeitos ligados muda de hash por desenho.
- **Follow-ups**: instância e desgaste, `skill:` e `stock:` como alvos, dono do objeto (fatias seguintes da #47).
