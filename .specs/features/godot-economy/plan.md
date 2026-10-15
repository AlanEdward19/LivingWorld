# Godot economia da cidade e vendas da oficina [Estado: Implementado, aguardando Verifier independente (2026-10-14)]

Issue #59, fatia F. Só cliente + contratos congelados; nenhuma rota nova.

## Problem

`/cities/{id}/treasury` e `/workplaces/{id}/trades` existem mas o cliente não as chama: o jogador não vê para onde vai o dinheiro da cidade nem o que cada oficina vende.

## Flow

1. `Contract/api_client.gd` (extended) - `treasury`, `workplace_trades`; shapes `city-treasury`, `workplace-trades`
2. `Scenes/economy_window.gd` (new) - tesouro (saldo + barras de entrou/saiu por mês) e vendas por dia e recurso
3. `Scenes/panels.gd` / `app.gd` (extended) - botão Tesouro (ficha da cidade) e "Vendas do local de trabalho" (ficha do NPC com emprego)
4. `Scenes/boot.gd` / `app.gd` - o período do mundo criado chega ao app para nomear recursos (`descriptors.resource`)
5. `EconomyContractTests` (new .NET) - semeia cidade, oficina e livros e congela as formas

## Impact

| Front | What changes |
| --- | --- |
| domain | none |
| stored data | none - só leitura |
| contract | 2 shapes novos; nenhum campo novo na API |

## Landing

- **Dívida**: não existe rota que diga o período do mundo em andamento; o nome do recurso só aparece se o mundo foi criado nesta sessão (senão "recurso N"). Rota nova pede issue própria.
- Não há ficha de prédio no cliente: as vendas abrem pelo local de trabalho do NPC selecionado.

## Criteria

1. WHEN abre o tesouro THEN SHALL chamar `GET /cities/{id}/treasury` e mostrar o saldo e, por mês (calendário do servidor), barras de entrou e saiu proporcionais ao maior valor
2. WHEN abre as vendas THEN SHALL chamar `GET /workplaces/{id}/trades` e listar dia (data do calendário), recurso (nome do catálogo do período ou número), quantidade, valor e compras, com o total
3. IF vazio, 404 ou 5xx THEN SHALL mostrar mensagem própria (5xx também avisa); corpo fora do contrato vira aviso
4. WHILE o NPC não tem emprego THEN o botão de vendas SHALL ficar oculto
5. WHEN o mundo foi criado pelo menu THEN o período SHALL chegar ao app e os nomes de recurso SHALL vir de `/periods/{id}/catalog`; sem período, nenhum pedido de catálogo
6. The system SHALL usar só GET; resposta tardia de outro assunto é descartada; Esc fecha
7. The system SHALL manter `stress:500` com a janela aberta dentro de ±3 FPS

**Independent test:** `bash scripts/client-test.sh --filter test_economy`.

## Out of scope

Gráficos longos, filtros de período, edição de preço.

## Performance

M1 Pro, `stress:500`, 2026-10-14: sem janela 74,0 / 75,0 / 76,2; com as vendas abertas 77,7 / 76,9 / 78,1 (sem regressão).
