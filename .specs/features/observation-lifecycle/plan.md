# Ciclo de vida da observação [Estado: Implementado, aguardando Verifier independente (2026-10-14)]

Limitação deixada pelas #58/#59 A: fechar a janela do jogo não retirava o escopo de observação nem
encerrava a conversa, e o servidor só descartava fonte sumida no próximo POST de alguém.

## Flow

1. `ObservationScopeEndpoints.cs` (extended) - purga periódica (duas vezes por janela de heartbeat), sem esperar POST
2. `Scenes/boot.gd` (extended) - `quit_gracefully`: fechar a janela chama `release_observation` (escopo `null` + `/conversations/end`), com teto de 2 s, e só então sai

## Impact

| Front | What changes |
| --- | --- |
| domain | none |
| stored data | none - registro de observação é volátil, fora do hash |
| contract | none |

## Criteria

1. WHEN uma fonte para de enviar batimento por mais que o timeout THEN o servidor SHALL removê-la sem novo POST (.NET)
2. WHEN o jogador fecha a janela do jogo THEN o cliente SHALL retirar a fonte e encerrar a conversa aberta antes de sair
3. IF o servidor não responde ao fechar THEN o cliente SHALL sair em até 2 s (`QUIT_GRACE`)
4. The system SHALL sair uma única vez, mesmo com dois pedidos de fechar

**Independent test:** `bash scripts/client-test.sh --filter test_app` e `dotnet test --filter ObservationScopeEndpointsTests`.

## Out of scope

Queda abrupta do processo (kill): só o timeout do servidor cobre.
