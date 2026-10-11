# Cliente Godot — Living World

Cliente gráfico só de leitura (ADR-0022). O motor (`src/LivingWorld.Api`) é dono do estado, do tempo e do
determinismo; este projeto só desenha o que a API projeta. Godot 4.7 (qualquer build; o .NET não é usado), tudo em GDScript.

## Rodar

```bash
# 1. API (outro terminal) — porta 5214
ConnectionStrings__World="Data Source=/tmp/lw.db" TICK_LOOP_ENABLED=true dotnet run --project src/LivingWorld.Api -c Release --urls http://localhost:5214
# 2. Cliente
/Applications/Godot_mono.app/Contents/MacOS/Godot --path client/godot
```

## Controles

| Ação | Tecla / mouse |
| --- | --- |
| Mover / girar / zoom | botão direito arrasta · Shift+arrasta · roda · WASD/setas · Q/E |
| Entrar na cidade | zoom até a escala de cidade, ou duplo clique na cidade |
| Entrar numa casa | clique no prédio (andares: PgUp/PgDn) · Esc sai |
| Pessoas | L abre a lista (segue a cidade em foco) · clique num NPC · botão Seguir ou F |
| Orientação | passe o mouse (dica) · minimapa (clique/arraste) · avisos de eventos |
| Som | ambiente procedural (vento, pássaros de dia, grilos à noite) · M silencia |
| Relógio | pausa, passo, +1 ano e velocidades 1× a 200× na barra; o céu segue a hora do mundo |

## Estrutura

| Pasta | Papel |
| --- | --- |
| `Contract/` | Única camada que fala HTTP/WebSocket: snapshot, deltas por `sequence`, `replay`, reconexão, `ApiClient` (todas as demais rotas); valida contra `tests/contracts/shape.*.json` |
| `Store/` | Estado local reconstruível (`WorldStore`) e rótulos de ação vindos do servidor |
| `Scenes/` | Mundo 3D (terreno, água, árvores, prédios com telhado e janelas), câmera (`CameraRig`, trocável), NPCs articulados por pose, interior multi-andar, ciclo dia/noite, minimapa, avisos, painéis |
| `tests/` | Testes headless (`bash scripts/client-test.sh [--filter arquivo::teste]`), com transporte falso |

Capturas e demos automáticas: `-- --steps "wait:2;city;wait:1;shot:/tmp/a.png;quit"` (ver `Scenes/dev_steps.gd`).
