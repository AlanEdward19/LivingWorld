# Godot art audio pass [Estado: Entregue (2026-10-14); stress:500 medido em 75-77 FPS e verificação independente aprovada]

## Problem

O cliente Godot usa assets provisórios procedurais: `.specs/features/godot-client-experience/plan.md` deixa fora música, arte final, modelos importados, mobília e cômodos do interior, e `STATE.md` lista "som e arte final" como pendência. Hoje o interior mostra só cama (sono) e ferramenta (trabalho), os NPCs são pawns e não há áudio.

Para o mundo parecer vivo no cliente faltam som ambiente, passos, mobília por tipo de prédio e personagens com animação distinta. Evidência: `client/godot/Scenes/ambience.gd`, `animation_catalog.gd`, `interior_stage.gd`.

Quando isto entrar, o cliente tem áudio ambiente e de ação, mobília por tipo de prédio e modelos de NPC diferenciados, sem tocar a simulação.

## Flow

Só cliente; assets entram por catálogo de dados, nunca por regra de simulação.

1. `animation_catalog.gd` (exists, extended) - mapeia ação para clipe e som
2. `ambience.gd` (exists, extended, door 1) - áudio por hora do dia, bioma e prédio
3. `interior_stage.gd` (exists, extended) - mobília por tipo de prédio vinda de catálogo de dados
4. `npc_actor.gd` (exists, extended) - modelo por profissão e idade
5. `scripts/client-test.sh` (exists) - testes de catálogo completo

## Impact

| Front | What changes |
| --- | --- |
| domain | termos: catálogo de áudio, catálogo de mobília |
| stored data | none - só arquivos de asset do cliente; hash idêntico |

## Relations

None - nenhuma entidade persistida.

## Surface

None - nothing consumed outside

## Landing

| One-way door | Literal shape | Alternative rejected |
| --- | --- | --- |
| 1. catálogo de assets | arquivo de dados `assets.json` por id de ação, tipo de prédio e profissão, com fallback para o procedural | assets citados direto no código GDScript: cada troca de arte vira edição de código |

- Nada mais aqui é difícil de reverter

## Criteria

### S1: Áudio (P1)

**Acceptance Criteria**

1. WHEN a hora do dia muda de período THEN the system SHALL trocar o áudio ambiente com fade
2. WHEN o NPC em foco muda de ação THEN the system SHALL tocar o som do catálogo da ação
3. IF o asset do catálogo falta THEN the system SHALL usar o procedural sem erro

**Independent test:** teste de cliente com catálogo parcial.

### S2: Mobília e modelos (P1)

**Acceptance Criteria**

4. WHEN o interior de um prédio abre THEN the system SHALL mostrar a mobília do catálogo para o tipo do prédio
5. The system SHALL manter todo tipo de prédio do cenário com entrada no catálogo
6. The system SHALL manter 50 FPS ou mais com `--steps "stress:500"` na máquina de referência

**Independent test:** teste de catálogo completo e stress de 500 NPCs.

### S3: Só leitura (P1)

**Acceptance Criteria**

7. The system SHALL manter o hash canônico inalterado após 1 h simulada conectado
8. The system SHALL manter todos os testes de `scripts/client-test.sh` verdes (89 hoje)

**Independent test:** `bash scripts/client-test.sh`.

## Out of scope

| Excluded | Why |
| --- | --- |
| música autoral e arte final encomendada | troca de assets, não de feature |
| criar mundo e crônicas no cliente | telas futuras |
| regra de simulação | ADR-0005, ADR-0014 |

## Assumptions

| Assumption | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| formato de áudio | OGG | suporte nativo no Godot | n |
| licença | assets livres ou procedurais | evita bloqueio de distribuição | n |

**Open questions:** none - all resolved or logged above.

## Observable

None - no user-facing surface

## Sources

- `.specs/features/godot-client-experience/plan.md`
- `STATE.md` - Handoff do cliente Godot
- `client/godot/Scenes/`
