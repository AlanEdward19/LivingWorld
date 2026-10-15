# Godot main menu [Estado: Fatias 1, 2 e 3 implementadas; telas de backend entregues pela #59]

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

### S2: Personalizar o mundo antes e depois de criar (P1)

O cenário é texto cru editado só nos membros do topo (`world_editor.gd`); o servidor valida na prévia e na criação.

5. WHEN o usuário muda mapa, população, natalidade, longevidade ou assentamentos THEN o cenário enviado SHALL levar
   essas mudanças (inteiros continuam inteiros) e a prévia SHALL refletir o mesmo rascunho
6. WHEN clica na prévia THEN o assentamento marcado SHALL ir para a célula; o primeiro é a aldeia inicial
7. WHEN o mundo é criado THEN SHALL abrir os retoques: reescrever a personalidade (10 traços) de um NPC e conceder
   poder do catálogo do mundo (`/authoring/...`); sem poderes no cenário, o seletor fica desabilitado e diz isso

**O que o backend permite hoje (só cliente) x o que exige endpoint novo**

| Pedido | Hoje | Exigiria |
| --- | --- | --- |
| tamanho do mapa, população, natalidade, longevidade | membros do cenário (feito) | - |
| posicionar assentamentos | `Settlements[{Name,X,Y}]` (feito; ruas/orientação não editadas) | - |
| posicionar prédios do catálogo | `Workplaces` (tipo, posição, vagas), com colocação segura no servidor (feito, fatia 3); a planta é do servidor | editor de plantas (fase posterior) |
| criar famílias e NPCs personalizados | `AuthoredHouseholds` no cenário (feito, fatia 3, ADR-0024, spec `authored-households`) | NPC com o mundo rodando (fase posterior) |
| criar poderes novos | catálogo vem do cenário (`Extraordinary`); o cliente só concede os existentes | editor de `PowerDescriptor` no cenário |
| personalidade de NPC | `PUT /authoring/npcs/{id}/personality` (feito) | - |

Contrato novo é decisão do dono do projeto (AGENTS.md: perguntar antes).

## Out of scope (fatias seguintes, cada uma com spec)

Mundos salvos (lista/carregar: o backend só expõe o mundo atual; ver #67).

Entregues depois, cada uma com spec própria em `.specs/features/`: conversa com NPC (`godot-npc-conversation`),
crônicas, biografias e relatos (`godot-narratives`), crenças (`godot-beliefs`), encarnar jogador (`godot-player`),
console de admin (`godot-admin`), tesouraria e trocas (`godot-economy`), tecnologias (`godot-technologies`),
materializar NPC (`godot-materialize`), vigiados e painel Cérebro (`godot-brain-panel`).

## Sources

- `src/LivingWorld.Api/World/`, `src/LivingWorld.Api/Periods/PeriodsEndpoints.cs`
