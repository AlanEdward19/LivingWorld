# Cliente Godot — Living World

Cliente gráfico só de leitura (ADR-0022). O motor (`src/LivingWorld.Api`) é dono do estado, do tempo e do
determinismo; este projeto só desenha o que a API projeta. Godot 4.7 (qualquer build; o .NET não é usado), tudo em GDScript.

## Rodar

Atalho: `bash scripts/play.sh` sobe a API (mundo vivo) e abre o cliente no **menu principal** (Continuar · Novo mundo · F10 volta ao menu; Novo mundo tem "Personalizar" (mapa, assentamentos, prédios, famílias e pessoas) e, depois de criar, "Retoques"). Manualmente:

```bash
# 1. API (outro terminal) — porta 5214
ADMIN_TOKEN=dev ConnectionStrings__World="Data Source=/tmp/lw.db" TICK_LOOP_ENABLED=true dotnet run --project src/LivingWorld.Api -c Release --urls http://localhost:5214
# 2. Cliente
LW_ADMIN_TOKEN=dev /Applications/Godot_mono.app/Contents/MacOS/Godot --path client/godot
```

Pausa, velocidade, passo e +1 ano são do admin (ADR-0015): a API exige `X-Admin-Token` igual a `ADMIN_TOKEN` e o cliente manda o valor de `LW_ADMIN_TOKEN` (o `play.sh` gera um por sessão). Sem o token esses botões recebem 403 e nada muda.

## Controles

| Ação | Tecla / mouse |
| --- | --- |
| Mover / girar / zoom | botão direito arrasta · Shift+arrasta · roda · WASD/setas · Q/E |
| Entrar na cidade | zoom até a escala de cidade, ou duplo clique na cidade |
| Entrar numa casa | clique no prédio (andares: PgUp/PgDn) · Esc sai |
| Pessoas | L abre a lista (segue a cidade em foco) · clique num NPC · botão Seguir ou F |
| Cérebro do NPC | B (ou botão Cérebro na ficha) abre/fecha o painel das decisões do NPC selecionado: linha do tempo + fluxo Estímulo → Ponderação → Decisão · Vigiar retém o rastro inteiro · Esc fecha |
| Conversar | T (ou botão Conversar na ficha) pede a conversa; o NPC pode recusar (ocupado, hostil, indisponível) · Enter envia · Esc encerra |
| Orientação | passe o mouse (dica) · minimapa (clique/arraste) · avisos de eventos |
| Som | ambiente procedural com fade ao trocar de período, som da ação do NPC selecionado (catálogo `assets.json`) · M silencia |
| Relógio | pausa, passo, +1 ano e velocidades 1× a 200× na barra; o céu segue a hora do mundo |

## Estrutura

| Pasta | Papel |
| --- | --- |
| `Contract/` | Única camada que fala HTTP/WebSocket: snapshot, deltas por `sequence`, `replay`, reconexão, `ApiClient` (todas as demais rotas); valida contra `tests/contracts/shape.*.json` |
| `Store/` | Estado local reconstruível (`WorldStore`) e rótulos de ação vindos do servidor |
| `Scenes/` | Mundo 3D (terreno, água, árvores, prédios com telhado e janelas), câmera (`CameraRig`, trocável), NPCs articulados por pose, interior multi-andar, ciclo dia/noite, minimapa, avisos, painéis |
| `tests/` | Testes headless (`bash scripts/client-test.sh [--filter arquivo::teste]`), com transporte falso |

Capturas e demos automáticas: `-- --steps "wait:2;city;wait:1;shot:/tmp/a.png;quit"` (ver `Scenes/dev_steps.gd`).

## Desempenho

`-- --steps "wait:2;stress:500;quit"` põe 500 NPCs andando e imprime os quadros por segundo. Medido num M1 Pro: ~77 FPS com a câmera longe (NPCs simplificados) e ~52 FPS perto. Acima de 150 NPCs e zoom longe, o NPC vira tronco e cabeça; fora da vista só interpola a posição.

Zoom LOD (2026-10-13, M1 Pro, `stress:500`, câmera padrão): antes 78,1 / 77,8 / 78,6 FPS; depois 78,4 / 78,9 / 76,9 (teto de 79 pelo vsync, ruído de ±1). No mapa-múndi afastado os NPCs viram bolhas (acima de 60% do zoom máximo).
Arte e áudio (2026-10-14, M1 Pro, `stress:500`, câmera padrão, API local): 75,8 / 77,2 / 75,3 FPS (teto de 77-79 pelo vsync), contra 76,9-78,9 antes do catálogo: dentro de ±3 e bem acima dos 50 FPS exigidos.
Painel Cérebro (2026-10-14, M1 Pro, `stress:500`, API local): sem painel 79,4 / 80,0 / 77,7 FPS; com o painel aberto num NPC 77,2 / 77,5 / 76,4 (média 77,0 contra 79,0: dentro de ±3; antes da mudança 77,3 / 78,3 / 75,6). O painel só busca `/npcs/{id}` quando a hora do mundo muda e fechado não faz requisição.

O rastro de cognição só é gravado para NPC em escopo observado: `Contract/scope_observer.gd` publica o escopo da câmera (`POST /observation/scope`, batimento de 10 s, `null` ao voltar ao menu).
Janela de conversa (2026-10-14, M1 Pro, `stress:500`, API local): sem janela 79,4 / 78,2 / 78,8 FPS; com a janela aberta 78,2 / 77,7 / 77,8 (dentro de ±3).
