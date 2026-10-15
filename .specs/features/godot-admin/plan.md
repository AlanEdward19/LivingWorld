# Godot console de admin [Estado: Implementado, aguardando Verifier independente (2026-10-14)]

Issue #59, fatia E. Só cliente + contratos congelados; nenhuma rota nova.

## Problem

`/admin/*` (Fase 26, ADR-0015) existe mas o operador só tem curl. O cliente também não avisa os jogadores quando o mundo está pausado.

## Flow

1. `Contract/http_transport.gd` / `api_client.gd` (extended) - cabeçalho `X-Admin-Actor`; `admin_pause|resume|speed|search|audit`; shapes `admin-*`
2. `Scenes/admin_window.gd` (new) - abas Mundo (pausa com motivo e teto, retomar, velocidade), Busca (abre a ficha) e Auditoria
3. `Scenes/app.gd` (extended) - botão e F8 só com `LW_ADMIN_TOKEN`; faixa de pausa na barra do topo para todos
4. `AdminContractTests` (new .NET) - congela pausa/retomada, busca e auditoria

## Impact

| Front | What changes |
| --- | --- |
| domain | none |
| stored data | none no cliente; o servidor audita cada ação (já existia) |
| contract | 3 shapes novos; nenhum campo novo na API |

## Landing

- Teto 30 dias espelha `SimulationHost.MaxAdminPauseDays`: o servidor segue sendo o juiz (400 aparece na tela).
- **Dívida**: "Pausado por X desde <data>: <motivo>" para TODOS exige expor ator/motivo em rota pública (`/simulation/status` só traz `isPaused`). Rota nova pede issue própria (regra da #59). Hoje todos veem "Mundo pausado"; o detalhe completo aparece para quem pausou por este cliente.

## Criteria

1. WHILE sem `LW_ADMIN_TOKEN` THEN o console SHALL NOT aparecer (nem botão, nem F8)
2. WHEN o operador pausa THEN o cliente SHALL exigir motivo e teto entre 1 e 30 dias do calendário antes de enviar, mandar `{reason, maxTicks}` com `X-Admin-Actor`, e mostrar o 400/403/5xx do servidor
3. WHEN retoma ou muda a velocidade THEN SHALL enviar a rota certa com o operador; velocidade não positiva não sai
4. WHEN busca THEN SHALL enviar `q` e `kind` (nome/cidade/profissão) codificados; clicar num resultado SHALL abrir a ficha do NPC; vazio/400/403 têm mensagem
5. WHEN abre a auditoria THEN SHALL pedir `fromTick` e listar quando (calendário), quem, ação, alvo e detalhe
6. WHILE o mundo está pausado THEN a barra do topo SHALL avisar todos; com os detalhes se este cliente pausou; some ao voltar
7. IF resposta foge do contrato THEN vira aviso; janela fechada não pede nada
8. The system SHALL manter `stress:500` com o console aberto dentro de ±3 FPS

**Independent test:** `bash scripts/client-test.sh --filter test_admin`.

## Out of scope

Autenticação real (#63), ramificar mundo, editar NPC.

## Performance

M1 Pro (máquina ocupada), `stress:500`, 2026-10-14: sem console 71,0 / 70,7 / 76,8; busca aberta 72,6 / 73,0 / 72,7.
