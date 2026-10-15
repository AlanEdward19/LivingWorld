# Sandbox de decisão "E se…?" [Estado: Implementado, aguardando Verifier independente (2026-10-14)]

Issue #58, fatia 2 (Fase 28 task 12). Rota nova = contrato público: decidida em `docs/adr/ADR-0026-rota-decision-sandbox.md`
(o usuário pediu implementar tudo sem supervisão).

## Problem

`DecisionSandbox` existia só como classe. O painel Cérebro não tem como mostrar "o que este NPC decidiria se…".

## Flow

1. `Simulation/Behavior/Decision/NpcDecisionSandbox.cs` (new) - contexto real do NPC + overrides, roda `DecisionSandbox.Decide`
2. `Api/Sandbox/DecisionSandboxEndpoints.cs` (new) - `POST /npcs/{id}/decision-sandbox`; 404 / 400
3. `Contract/api_client.gd` (extended) - `decision_sandbox`; shape `decision-sandbox` congelado
4. `Scenes/brain_panel.gd` (extended) - botão "E se…?": sliders de 4 necessidades e 10 traços, "Simular", mesmo fluxo

## Impact

| Front | What changes |
| --- | --- |
| domain | none |
| stored data | none - só leitura, hash idêntico |
| contract | rota nova, `shape.decision-sandbox.json`, `openapi.v1.json` regravado |

## Landing

- Rota pública: ADR-0026. Corpo opcional e compatível para estender (memória/crença sintética).

## Criteria

1. WHEN a rota é chamada 100× THEN o hash canônico do mundo SHALL permanecer idêntico
2. WHEN só a fome muda no corpo THEN `topPressures` SHALL mudar; mudar traços SHALL mudar o rastro
3. WHEN o NPC não existe/morreu THEN 404; WHEN valor fora de 0–100, traço ou ação desconhecidos THEN 400
4. WHEN o jogador aperta Simular THEN o cliente SHALL enviar só o que difere do NPC real e desenhar o resultado no mesmo fluxo da fatia 1
5. IF a resposta é 400/404/5xx ou foge do contrato THEN SHALL avisar sem quebrar o painel
6. The system SHALL NOT escrever nada além de `POST /npcs/{id}/decision-sandbox`; nova hora não troca a simulação na tela
7. The system SHALL manter `stress:500` com o painel em "E se…?" dentro de ±3 FPS

**Independent test:** `dotnet test --filter DecisionSandboxEndpointTests` e `bash scripts/client-test.sh --filter test_brain_sandbox`.

## Out of scope

Memória/crença/foresight sintéticos; `ProvisionTargetDays` do mercado (usa o padrão); persistir simulações.

## Performance

M1 Pro, `stress:500`, 2026-10-14: sem painel 77,6 / 78,3 / 78,6; painel em "E se…?" 74,4 / 76,2 / 76,2 (média -2,6, dentro de ±3).
