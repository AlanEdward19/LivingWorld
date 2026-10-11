# godot-client-experience checks

Profile: light
Plan: `.specs/features/godot-client-experience/plan.md`

10 checks in 3 slices · 0 one-way doors · 0 open

## Checks

### E1 - Tempo e luz
**C1** - Tick 12 dá sol forte, céu claro e janelas apagadas; tick 7 sol mais fraco e mais quente; tick 23 luar azulado fraco, céu escuro, sol em outra posição e janelas acesas (AC 1.1)
Proof: `bash scripts/client-test.sh --filter "test_ui_modules::test_day_cycle_follows_server_hour"`

**C2** - Pausado a hora não anda; a 4 ticks/s avança 4 h em 1 s e vira o dia (AC 1.2, 1.3)
Proof: `bash scripts/client-test.sh --filter "test_ui_modules::test_day_cycle_runs_only_while_server_runs"`

### E2 - Orientação
**C3** - A dica traz nome e rótulo do servidor para NPC, andares para prédio, população para cidade, e nada sem alvo (AC 2.1)
Proof: `bash scripts/client-test.sh --filter "test_ui_modules::test_hover_text_comes_from_store_and_server_labels"`

**C4** - Clicar no minimapa pede a célula clicada e o mapeamento célula↔minimapa é reversível (AC 2.2)
Proof: `bash scripts/client-test.sh --filter "test_ui_modules::test_minimap_maps_cells_and_requests_pan"`

**C5** - Evento do delta vira aviso com o texto do servidor (AC 2.3)
Proof: `bash scripts/client-test.sh --filter "test_ui_modules::test_delta_events_become_toasts_with_server_text"`

**C6** - No máximo 5 avisos visíveis e o histórico guarda todos (AC 2.3)
Proof: `bash scripts/client-test.sh --filter "test_ui_modules::test_toasts_keep_five_and_full_history"`

**C7** - O carregamento fica visível sem snapshot, some quando o primeiro chega, e o nome de `GET /npcs` chega ao NPC na cena (AC 2.4, 2.5)
Proof: `bash scripts/client-test.sh --filter "test_ui_modules::test_loading_overlay_until_first_snapshot_and_names_load"`
Proof: `bash scripts/client-test.sh --filter "test_ui_modules::test_loading_visible_until_first_snapshot"`
Proof: `bash scripts/client-test.sh --filter "test_ui_modules::test_loading_visible_during_normal_startup"`

**C9** - O nome do NPC só aparece sob o cursor ou selecionado, com o texto do nome (AC 2.5)
Proof: `bash scripts/client-test.sh --filter "test_ui_modules::test_actor_name_label_shows_on_hover_and_selection"`

**C10** - O tick do status vira a hora do céu e da barra superior (06:00, Amanhecer), e o minimapa leva a câmera à célula pedida (AC 1.1, 2.2)
Proof: `bash scripts/client-test.sh --filter "test_ui_modules::test_status_drives_sky_and_minimap_drives_camera"`

### E3 - Ficha
**C8** - As cinco barras mostram saúde 90, fome 80, sede 70, sono 60 e social 50 vindos de `GET /npcs/7` (AC 3.1)
Proof: `bash scripts/client-test.sh --filter "test_panels::test_select_npc_fetches_and_highlights"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| alvos da dica (3) | NPC C3 · prédio C3 · cidade C3 | - |
| fases do dia testadas (3) | Dia C1 · Noite C1 · Amanhecer C1 | - |

- Claims que citam rota ou campo de contrato (C7, C8) têm prova que atravessa o `ApiClient` com transporte falso
- Nenhum outro check afirma mais que o caso único que sua prova exercita

## Swept

- validation: n/a - sem entrada nova do usuário; formas do contrato seguem em `godot-client` C5
- failure modes: existing - banner "sem conexão" e estado de erro da lista em `godot-client`
- idempotency: n/a - só leitura
- authorization: n/a - sem rota nova
- concurrency: n/a - tudo no thread principal; leituras assíncronas aplicadas em ordem de chegada
- data lifecycle: n/a - nomes são recarregados a cada 20 s e a cada troca de escopo
- dependency failure: existing - `CursorStream` reconecta com espera crescente
- state transitions: C2
- observability: n/a - sem requisito de log

## Handoff

- E1 = 3k, E2 = 10k, E3 = 2k, total ≈ 15k, abaixo do budget de 150k - one builder
- Mechanism: one builder
