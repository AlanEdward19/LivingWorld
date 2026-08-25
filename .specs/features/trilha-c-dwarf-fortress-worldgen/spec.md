# Trilha C — Geração de História do Mundo (estilo Dwarf Fortress)

## Problem Statement

Antes de jogar, o usuário quer gerar o mundo por X anos (flora/fauna/cidades/civilizações
evoluindo), ver os eventos como feed de texto (estilo Dwarf Fortress), avançar/retroceder anos
livremente, e só então apertar "iniciar simulação" pra entrar no jogo de verdade. Hoje o motor
já tem as peças (fast-forward headless, log causal imutável, geração de narrativa
determinística, snapshot em ticks passados) mas nenhuma delas está costurada numa experiência
de pré-jogo — é tudo backend interno sem endpoint parametrizado nem tela própria.

**Fora desta spec**: qualquer coisa do motor de poderes/Extraordinary — isso é
`.specs/features/phase-16-1-power-engine/` (spec própria, já Design+Tasks feitos).

## Goals

- [ ] Avançar a simulação N anos sob demanda (não só 1 ano fixo por chamada).
- [ ] Navegar (ir e voltar) por anos já gerados, isolado do save "real" que começa quando o
      jogador aperta "iniciar simulação" — nunca reescreve o branch principal durante a
      exploração de pré-jogo.
- [ ] Chronicle com substância de história de civilização (fundação, guerra, ascensão de
      dinastia), não só nascimento/morte/economia.
- [ ] Tela "Gerar História" entre o preset e o editor de mundo: contador de ano, avançar/
      retroceder N anos, feed de texto scrollável, câmera livre sobre o mapa sendo gerado,
      botão "Iniciar simulação" que fecha a fase de história e entra no jogo de verdade.

## Out of Scope

| Item | Razão |
| --- | --- |
| Fase 16 / motor de poderes (Extraordinary) | Virou spec própria (`phase-16-1-power-engine`) — não repetir aqui, mesmo tendo sido cogitado nesta trilha antes. |
| Rewind genérico durante o jogo AO VIVO (pós "iniciar simulação") | Escopo é só a fase de pré-jogo/geração de história — rewind de save real é feature própria, maior, fora desta trilha. |
| Conteúdo autorado de era moderna/futurista (`modern.json`/`futuristic.json`) | Trabalho de conteúdo/domínio grande, decisão de escopo separada (já registrado como gap no plano original). |
| Veículos como entidade simulada | Não pedido nesta trilha; fica registrado como gap de domínio, não requisito. |
| Flora/fauna como agentes simulados de verdade | Coberto pela spec do motor de poderes (`PWR-77..79`, `PWR-101..103`) como pré-requisito de mecânica de poder — aqui, chronicle só PODE mencionar textualmente eventos de flora/fauna quando esses conceitos existirem; não é esta spec que os cria. |

## Assumptions

| Assumption | Racional | Confirmado? |
| --- | --- | --- |
| Isolamento via `BranchId` já existente no repositório | `PersistentWorldRunner.LoadAt(tick)`/`BranchId` já existem como conceito, nunca ligados a endpoint — navegação exploratória de pré-jogo usa um branch efêmero, nunca o `BranchId.Root` que "Continuar" lê | Inferido do recon já feito nesta sessão, não uma pergunta literal ao usuário — sinalizar se limitação de slot único (ver `.specs/STATE.md` Handoff) afetar isso |
| `ChronicleGenerationSystem`/`GET /narratives/chronicles` é a base certa pro feed de texto | Já gera narrativa determinística a partir dos `Fact`s mais significativos — só precisa de `WorldEventKind`s novos pra ter substância de civilização | s |
| Tela nova fica entre `PresetStart.tsx` e `WorldEditor.tsx` | Ordem já combinada no plano original (`vivid-drifting-dolphin.md`) | s |

## User Stories

### P1: Avanço parametrizado de anos

**User Story**: Como jogador na tela de geração de história, quero avançar N anos de uma vez
(não só 1), pra gerar história rapidamente sem N cliques.

**Acceptance Criteria**:
1. WHEN o cliente chama o endpoint de avanço com `years=N` THEN o servidor SHALL rodar
   `SimulationHost.FastForward` pelo número de ticks equivalente a N anos, headless (sem
   exigir interação), retornando o novo ano/tick corrente.
2. WHEN `years` não é informado ou é ≤0 THEN o endpoint SHALL rejeitar com erro claro (não
   avançar silenciosamente 0 nem interpretar como "infinito").
3. WHEN o avanço termina THEN o chronicle (`GET /narratives/chronicles`) SHALL refletir os
   novos eventos do período avançado, sem precisar de chamada adicional de "flush".

**Independent Test**: chamar o endpoint com `years=5` num mundo novo avança exatamente 5 anos
de tick, e uma chamada subsequente ao chronicle mostra eventos dentro dessa janela.

---

### P1: Navegação (voltar/reavançar) escopada à pré-jogo

**User Story**: Como jogador explorando a história gerada, quero voltar pra um ano anterior e
seguir por outro caminho sem afetar o save "real" que vou começar depois.

**Acceptance Criteria**:
1. WHEN o cliente pede "ir pro ano Y" (Y no passado já gerado) THEN o servidor SHALL usar
   `PersistentWorldRunner.LoadAt(tick)` sobre um `BranchId` efêmero dedicado à sessão de
   geração de história — nunca o branch raiz que "Continuar" lê.
2. WHEN o jogador aperta "Iniciar simulação" THEN o estado corrente do branch efêmero (ano em
   que o jogador parou de navegar) SHALL se tornar o save real a partir dali — o branch
   efêmero anterior a esse ponto SHALL ser descartado (não é preciso manter todos os
   caminhos não escolhidos).
3. WHEN o jogador nunca navega pro passado (só avança) THEN o comportamento SHALL ser
   idêntico a hoje (nenhuma regressão pro fluxo linear).

**Independent Test**: avançar 10 anos, voltar pro ano 5, avançar mais 3 (total 8) — o
chronicle reflete só o caminho final (5→8), nunca uma mistura com o caminho descartado (5→10).

---

### P1: Eventos de civilização no chronicle

**User Story**: Como jogador lendo o feed de história, quero ver fundação de civilização,
guerra e ascensão de dinastia como eventos de verdade, não só nascimento/morte/economia.

**Acceptance Criteria**:
1. WHEN uma cidade atinge os critérios já existentes de "vira civilização" (mesmo limiar já
   usado por `SettlementFoundingSystem`/fundação, adaptado pro conceito de civilização, não
   reinventado) THEN um `WorldEventKind` novo (`CivilizationFounded` ou equivalente) SHALL
   ser logado.
2. WHEN duas cidades/civilizações entram em conflito sustentado (reusa qualquer sinal de
   conflito já existente no motor — inclui `WorldEventKind.CombatResolved`, se a spec do
   motor de poderes já tiver entregue essa peça; se não, este AC fica com um sinal mais
   simples de "conflito econômico/território" já existente) THEN `WorldEventKind.War` (ou
   equivalente) SHALL ser logado.
3. WHEN uma linhagem/família governante muda THEN `WorldEventKind.DynastyRise` (ou
   equivalente) SHALL ser logado.
4. WHEN `ChronicleGenerationSystem` narra o período THEN esses eventos novos SHALL aparecer
   com a mesma qualidade narrativa (texto determinístico) já usada pros eventos existentes.

**Independent Test**: cenário de mundo com múltiplas cidades gerando conflito ao longo de N
anos produz pelo menos 1 evento de cada `WorldEventKind` novo no log, refletido no chronicle.

---

### P1: Tela "Gerar História"

**User Story**: Como jogador criando um mundo, quero uma tela entre o preset e o editor onde
vejo o ano corrente, posso avançar/retroceder, ler o feed de eventos, navegar livremente pelo
mapa sendo gerado, e decidir quando "iniciar simulação" de verdade.

**Acceptance Criteria**:
1. WHEN o jogador sai do preset (`PresetStart.tsx`) THEN a tela "Gerar História" SHALL
   aparecer antes do `WorldEditor.tsx` (ordem: preset → gerar história → editor/jogo).
2. WHEN o jogador clica "avançar N anos" THEN o contador de ano SHALL atualizar e o feed
   SHALL mostrar os novos eventos (uma linha por evento, timestamp em anos, mais recente
   visível sem scroll adicional).
3. WHEN o jogador clica "retroceder pro ano Y" THEN o mapa e o feed SHALL refletir o estado
   daquele ano (reusa o endpoint de navegação acima).
4. WHEN o jogador move a câmera sobre o mapa THEN a mesma engine de render (`MapView`/
   `map-engine`, já usada no jogo) SHALL exibir o mundo sendo gerado, sem duplicar código de
   renderização.
5. WHEN o jogador clica "Iniciar simulação" THEN a tela SHALL fechar e o jogo real SHALL
   começar a partir do ano corrente exibido (branch efêmero vira o save real, ver história
   acima).

**Independent Test**: fluxo completo no browser — preset → tela de história → avançar 3 anos
→ retroceder 1 → avançar 2 → "iniciar simulação" → editor/jogo abre no ano correto, feed de
eventos consistente com o caminho final.

## Edge Cases

- WHEN o jogador navega pro passado repetidamente sem nunca "iniciar simulação" THEN o
  servidor SHALL continuar reescrevendo o MESMO branch efêmero (nunca acumular branches
  órfãos sem limite).
- WHEN `years` solicitado excede um teto de segurança (evitar travar a UI num avanço gigante
  de uma vez) THEN o endpoint SHALL aceitar mas o cliente SHALL avisar/paginar em lotes — não
  é erro de servidor, é UX (mostrar progresso, não travar).

## Requirement Traceability

| ID | Story | Status |
| --- | --- | --- |
| WGN-01 | P1: Avanço parametrizado — anos configuráveis | Pending |
| WGN-02 | P1: Avanço parametrizado — rejeita years inválido | Pending |
| WGN-03 | P1: Avanço parametrizado — chronicle reflete sem flush extra | Pending |
| WGN-10 | P1: Navegação — `LoadAt` sobre branch efêmero, nunca o raiz | Pending |
| WGN-11 | P1: Navegação — "iniciar simulação" promove o branch efêmero a save real | Pending |
| WGN-12 | P1: Navegação — fluxo linear sem regressão | Pending |
| WGN-20 | P1: Eventos — `CivilizationFounded` | Pending |
| WGN-21 | P1: Eventos — `War` (conflito sustentado) | Pending |
| WGN-22 | P1: Eventos — `DynastyRise` | Pending |
| WGN-23 | P1: Eventos — chronicle narra os novos kinds | Pending |
| WGN-30 | P1: Tela — ordem preset→história→editor | Pending |
| WGN-31 | P1: Tela — avançar N anos atualiza contador+feed | Pending |
| WGN-32 | P1: Tela — retroceder reflete estado do ano | Pending |
| WGN-33 | P1: Tela — câmera livre reusa engine de render existente | Pending |
| WGN-34 | P1: Tela — "iniciar simulação" fecha a fase e promove o branch | Pending |

**Coverage**: 14 total, 0 excluídos (nenhuma exclusão nesta spec além do Out of Scope acima).
