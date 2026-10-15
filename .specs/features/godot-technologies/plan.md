# Godot tecnologias da cultura [Estado: Verificado, PASS (Verifier independente, 2026-10-15)]

Issue #59, fatia G. Só cliente + contrato congelado; nenhuma rota nova.

## Problem

`GET /cultures/{id}/technologies` existe (emergência, ADR-0013) mas o cliente não a chama: o jogador não vê o que a cultura de um NPC descobriu.

## Flow

1. `Contract/api_client.gd` (extended) - `technologies`; shape `culture-technologies`
2. `Scenes/technology_window.gd` (new) - cada tecnologia pela chave, categoria, data, adoção e eixos ritmo/carga/alcance em barras
3. `Scenes/panels.gd` / `app.gd` (extended) - botão "Tecnologias da cultura" na ficha do NPC, só quando a cultura já tem itens (o app confere ao selecionar)
4. `TechnologyContractTests` (new .NET) - semeia uma tecnologia e congela a forma

## Impact

| Front | What changes |
| --- | --- |
| domain | none |
| stored data | none - só leitura |
| contract | 1 shape novo (`variationOfKey` qualquer valor: só variações o trazem); nenhum campo novo na API |

## Landing

- Sem nome, por decisão do ADR-0013: a chave do vetor identifica a tecnologia.

## Criteria

1. WHEN o NPC selecionado tem cultura com tecnologias THEN o botão SHALL aparecer; sem itens (ou erro) SHALL ficar oculto; trocar de NPC reavalia
2. WHEN abre THEN SHALL chamar `GET /cultures/{id}/technologies` e mostrar, por tecnologia, chave (e "variação de"), categoria, data do calendário, cidades que adotaram e 3 barras (ritmo, carga, alcance) com a escala do maior eixo
3. IF vazio, 404 ou 5xx THEN SHALL mostrar mensagem própria (5xx também avisa); corpo fora do contrato vira aviso
4. The system SHALL usar só GET; janela fechada não pede nada; Esc fecha
5. The system SHALL manter `stress:500` com a janela aberta dentro de ±3 FPS (mesmo componente das fatias B/F)

**Independent test:** `bash scripts/client-test.sh --filter test_technologies`.

## Out of scope

Nomear tecnologias (LLM só rotula, ADR-0013), filtrar por `fromTick`.

## Performance

M1 Pro, `stress:500`, 2026-10-14: sem janela 75,5 / 75,0 / 74,9; com a janela aberta 77,0 / 75,8 / 74,5 (sem regressão).
