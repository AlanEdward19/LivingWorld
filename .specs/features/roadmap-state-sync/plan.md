# Roadmap/STATE sync [Estado: Entregue - #57, 2026-10-14]

## Problem
`ROADMAP.md` marcava 24/25/26 como `spec` apesar das fatias AD-110/111/112; `STATE.md` descrevia handoffs
antigos; a Fase 10 tinha `- [ ]` sem rastro; PERIOD-12 (Fase 13) não tinha teste.

## Critérios (EARS)
1. WHEN lido, `ROADMAP.md` e `docs/roadmap/phase-24..26` SHALL mostrar `parcial` com o que falta e a issue (#61/#62/#63); #64 na 16, #58 na 28; 14 segue `adiada`.
2. `STATE.md` SHALL descrever só o presente (uma entrada 2026-10-14, issues abertas, riscos corrigidos).
3. `bash scripts/check-docs.sh` SHALL sair 0.
4. Cada `- [ ]` de `phase-10-history/spec.md` SHALL virar `- [x]` com teste citado que existe e passa.
5. WHEN o exemplo de `docs/domain/period-authoring-example.md` é enviado a `POST /periods` sem transformação, SHALL responder 201 e `GET /periods/{id}` 200; WHEN um campo obrigatório é removido do exemplo, o teste SHALL reprovar.
6. `AGENTS.md` SHALL mudar só a linha do `HARNESS.md`.

## Portas de mão única
Nenhuma (docs + 1 teste). Sem contrato público, sem dependência.

## Desempenho
Sem código de produção alterado: motor e cliente inalterados (sem medição necessária).

## Fora de escopo
Implementar o que falta de 24/25/26 (#61–#63); ordem das fases (#66).
