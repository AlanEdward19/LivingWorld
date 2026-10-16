# FAM-32: diversidade calibrada [Estado: Implementado; verificação independente pendente]

## Problem

O critério e a asserção de FAM-32 divergem; a seleção reduz a variância registrada em AD-088.

## Flow

ScenarioRunner e NeutralDriftScenarioHarness existentes → amostras pareadas → baseline de 20 seeds → gates curto e de 100 anos.

## Impact

- Regras de herança preservadas; corrige-se o bloqueio de cortejo e a demografia, conforme ampliação explícita do usuário.
- Mudam baseline, testes e critério da fase aprovado pelo usuário.

## Relations

None - nenhum dado persistido muda.

## Surface

None - sem nova API.

## Landing

| One-way door | Literal shape | Alternative rejected |
| --- | --- | --- |
| critério FAM-32 | faixa e piso medidos em tests/baselines/family-diversity.json | desigualdade contra zero: contradiz seleção direcional; ADR-0034 |

## Criteria

Acceptance Criteria:

1. WHEN rodam 20 seeds pareadas THEN o teste SHALL exigir a faixa de diferença de CV de Vitality medida no baseline.
2. WHEN roda o controle negativo THEN o teste SHALL comparar atributo sem seleção direta com o neutro nas mesmas seeds.
3. WHEN a seleção de mortalidade é amplificada THEN o gate SHALL reprovar esse braço e aprovar o modelo atual.
4. WHEN passam os checkpoints de 100 anos THEN o CV SHALL permanecer acima do piso derivado do baseline.
5. The documentação SHALL declarar o mesmo critério executado, com FAM-32 Verified apenas após prova independente.

6. IF um candidato incompatível tem maior atração THEN o cortejo SHALL procurar um parceiro válido, sem violar incesto ou a janela fértil.

7. WHEN colegas trabalham no mesmo local durante o turno THEN o relógio SHALL registrar convivência, permitindo vínculos entre famílias da próxima geração.

8. WHEN nasce um NPC ou se forma um domicílio por casamento THEN o sistema SHALL preservar a cidade de origem, permitindo emprego local na geração seguinte.

9. WHEN um NPC é escolhido antes de sua vez na lista THEN ele SHALL manter esse cortejo, sem escolher outro parceiro no mesmo tick.

## Out of scope

FAM-06/20/22 e ajuste do modelo genético: fora da #65.

## Assumptions

Open questions: none - decisões confirmadas pelo usuário.

## Observable

| Surface | Decision | Landing |
| --- | --- | --- |
| documento | texto coincide com gate | AC 5 |

## Sources

- Issue #65, ADR-0034.
- Decisões explícitas do usuário em 2026-10-15.

## Calibration

20 seeds × 100 anos no cenário completo: mínimo CV 0,11330497764439135; população mínima 3.
População final entre 3 e 67; diferença Vitality média 0,0001055465, EP 0,0019169683.
Openness média −0,0042322281, EP 0,0035036824 (IC95 contém zero).
Regravação explícita: LW_RECORD_FAMILY_DIVERSITY=1, nunca no gate.

## Performance evidence

3 pares alternados contra develop: CPU mediana 1,121 → 0,844 µs/NPC-tick;
alocação 255.345 → 256.189 B/tick (+0,33%), tetos +3%/+2% preservados.
Amostras e comando em `saved-worlds/plan.md`; carga concorrente impede alegar aceleração.
