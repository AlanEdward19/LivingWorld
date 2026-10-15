# Godot encarnar jogador [Estado: Implementado, aguardando Verifier independente (2026-10-14)]

Issue #59, fatia D. Só cliente + contratos congelados; nenhuma rota nova. Sem autenticação (#63): o `playerId` é local.

## Problem

`/players/{npcId}/incarnate|action|logout` existe (Fase 25) mas o cliente não as chama: ninguém consegue jogar.

## Flow

1. `Contract/player_profile.gd` (new) - `playerId` gerado uma vez e guardado em `user://player.cfg`
2. `Contract/api_client.gd` (extended) - `incarnate`, `player_action`, `player_logout`; shapes `player-*`
3. `Scenes/player_hud.gd` (new) - faixa sob a barra: ações de `PlayerCommands.Choosable`, "Deixar o motor decidir", Sair, aviso de pausa
4. `Scenes/panels.gd` / `app.gd` (extended) - botão Encarnar e tecla I; câmera segue o personagem; pausa vem do relógio
5. `PlayerContractTests` (new .NET) - congela as 3 formas

## Impact

| Front | What changes |
| --- | --- |
| domain | none |
| stored data | `user://player.cfg` no cliente; mundo só muda pelos comandos do próprio jogador |
| contract | 3 shapes novos; nenhum campo novo na API |

## Landing

- `playerId` local é provisório até #63 (autenticação).

## Criteria

1. WHEN o jogador encarna THEN o cliente SHALL enviar `POST /players/{npcId}/incarnate {playerId}`; 409 (já controlado / já controla outro) e 404 têm mensagem própria e o HUD não abre
2. The system SHALL oferecer exatamente as 6 ações de `Choosable` (Eat, Sleep, Work, Socialize, Idle, Buy) com rótulos do catálogo do servidor, e "Deixar o motor decidir" enviando `action: null`
3. WHEN a ação é recusada (400) THEN SHALL avisar sem fechar; WHEN 404 THEN SHALL encerrar o HUD (perdeu o controle)
4. WHEN o jogador sai THEN SHALL `POST /players/{playerId}/logout` e mostrar que o personagem desapareceu no tick informado (não congelou); a câmera é solta
5. WHILE o mundo está pausado THEN o HUD SHALL avisar que as ações só andam quando o tempo voltar
6. WHILE sem personagem THEN SHALL NOT fazer requisição; só rotas `/players/*`; resposta fora do contrato não encarna
7. The system SHALL manter `stress:500` com o HUD ativo dentro de ±3 FPS

**Independent test:** `bash scripts/client-test.sh --filter test_player`.

## Out of scope

Autenticação, multiplayer, `UsePower`/`Travel` como escolha (o servidor as recusa).

## Performance

M1 Pro (máquina ocupada), `stress:500`, 2026-10-14: sem HUD 74,7 / 73,7 / 72,7; com o HUD ativo 73,1 / 73,6 / 72,6.
