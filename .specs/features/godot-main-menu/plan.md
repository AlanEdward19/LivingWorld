# Godot main menu [Estado: Fatia 1 implementada; fatias 2+ por vir]

## Problem

A simulação só aparecia via testes ou subindo API e cliente na mão, direto num mundo já criado. Falta
uma porta de entrada jogável que use o que o backend já oferece (períodos, preview, criar mundo).

Quando a fatia 1 entrar, `bash scripts/play.sh` abre um menu: Continuar o mundo em execução ou Novo
mundo (período, nome, seed, prévia do mapa real, criar) e F10 volta ao menu de dentro do jogo.

## Flow

Só cliente; o servidor decide o mundo (ADR-0022).

1. `boot.gd` (new, main scene) - abre o menu; `--steps` pula direto ao mundo
2. `main_menu.gd` (new) - título, estado do servidor, Novo mundo
3. `api_client.gd` (exists, extended) - `/periods`, `/periods/{id}/catalog`, `/worlds/preview`, `/worlds/create`
4. `json_raw.gd` (new) - troca a seed no texto do cenário sem reescrever números
5. `app.gd` (exists, extended) - `menu_requested` (F10 e botão Menu)
6. `scripts/play.sh` (new) - API + cliente juntos

## Impact

| Front | What changes |
| --- | --- |
| domain | termos: menu principal, prévia de mundo |
| stored data | none - o servidor persiste o mundo criado como já fazia |

## Landing

| One-way door | Literal shape | Alternative rejected |
| --- | --- | --- |
| 1. cenário como texto cru | `period_scenario` devolve o JSON do período como string, só com `Seed` trocada | parsear e reserializar no Godot: `1` vira `1.0` e o servidor rejeita campos inteiros |

## Criteria

### S1: Menu e novo mundo (P1)

1. WHEN o servidor responde THEN Continuar SHALL ficar habilitado; offline SHALL desabilitar e dizer como subir
2. WHEN abre Novo mundo THEN SHALL listar os períodos do servidor com a prévia do mapa da seed escolhida
3. WHEN Criar tem sucesso THEN SHALL entrar na simulação; em erro SHALL mostrar a mensagem do servidor e ficar
4. WHEN F10 THEN SHALL voltar ao menu sem fechar o servidor

## Out of scope (fatias seguintes, cada uma com spec)

Mundos salvos (lista/carregar: o backend só expõe o mundo atual), crônicas e biografias, conversa com NPC,
autoria, vigiados, tesouraria e trocas, encarnar jogador.

## Sources

- `src/LivingWorld.Api/World/`, `src/LivingWorld.Api/Periods/PeriodsEndpoints.cs`
