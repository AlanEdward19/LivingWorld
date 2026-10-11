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
| Pessoas | L abre a lista · clique num NPC · F segue |
| Relógio | botões na barra superior |

## Estrutura

| Pasta | Papel |
| --- | --- |
| `Contract/` | Única camada que fala HTTP/WebSocket: snapshot, deltas por `sequence`, `replay`, reconexão, validação contra `tests/contracts/shape.*.json` |
| `Store/` | Estado local reconstruível (`WorldStore`) e rótulos de ação vindos do servidor |
| `Scenes/` | Mundo 3D, câmera (`CameraRig`, trocável), NPCs animados, interior multi-andar, painéis |
| `tests/` | Testes headless (`bash scripts/client-test.sh [--filter arquivo::teste]`), com transporte falso |

Capturas e demos automáticas: `-- --steps "wait:2;city;wait:1;shot:/tmp/a.png;quit"` (ver `Scenes/dev_steps.gd`).
