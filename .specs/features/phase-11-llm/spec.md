# Fase 11 (Interacao com LLM) Specification

## Problem Statement
A simulação já gera mundo coerente sem LLM, mas ainda falta conversa com NPC vivo sem violar a fronteira de autoridade do motor. Esta fase define um pipeline onde a LLM gera apenas linguagem e intenção proposta; o motor valida, decide e aplica consequências. O objetivo é habilitar diálogo útil ao jogador sem abrir caminho para escrita direta no mundo, perda de determinismo ou travamento do tick.

## Goals
- [ ] Conversa jogador↔NPC por sessão, com contexto baseado em crença do NPC e histórico da sessão.
- [ ] Início de conversa respeita disponibilidade social: NPC pode recusar e não é forçado a parar ação corrente.
- [ ] Saída da LLM estritamente validada (DTO + schema + ações permitidas por contexto), com rejeição total em qualquer violação.
- [ ] Fallback determinístico para indisponibilidade, timeout, saída inválida ou orçamento excedido, sem parar simulação.
- [ ] Segurança de fronteira: nenhum handler de jogo acessa consulta de Verdade; apenas Crença entra no prompt.

## Out of Scope
| Feature | Reason |
| --- | --- |
| Narrativa longa (jornais, crônicas, biografias) | Fase 12 |
| Cliente visual/UX de conversa | Fase 15 |
| Voz, animação e conversa 3D | Fase 14 |
| LLM decidindo comportamento autônomo de NPC em background | Fora do roadmap atual |

## Assumptions & Open Questions
| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Provider real inicial | Suportar Claude API e/ou Ollama sob `ILlmProvider`; testes sempre usam `FakeLlmProvider` | Mantém gate offline e permite decisão final em ADR sem bloquear a fase | n |
| Persistência da sessão | Sessão de conversa vinculada a `NpcId` e `OpenedAtTick`; expira por regra de cenário | Evita estado eterno e facilita replay determinístico | n |
| Pesos do `Recall` | `importance`, `recency`, `relevance` vêm de `LlmRules` do cenário | Evita números mágicos em código/teste | n |
| Budget por interação | Limites de token/custo por sessão vêm de cenário e baseline | Controle de custo sem hardcode | n |
| Open questions | none | Todas as ambiguidades operacionais relevantes foram assumidas acima | n/a |

## User Stories
### P1: Sessão de conversa segura
**User Story**: Como jogador, quero abrir, conversar e encerrar sessão com um NPC vivo para obter resposta contextual sem pausar o mundo.  
**Acceptance Criteria**:
1. WHEN `StartConversation(npcId)` é chamado THEN o sistema SHALL avaliar disponibilidade social do NPC no tick atual e responder `Accepted` ou `Rejected` com motivo determinístico.
2. WHEN `SendPlayerMessage(sessionId, text)` é chamado THEN o sistema SHALL registrar turno e acionar pipeline de contexto→LLM→validação→resposta.
3. WHEN `EndConversation(sessionId)` é chamado THEN o sistema SHALL encerrar sessão sem apagar histórico persistido.
4. WHEN a conversa é aceita THEN o sistema SHALL manter a ação atual do NPC quando ela for compatível com conversar; só ações incompatíveis podem ser pausadas.

### P1: Contexto por crença e memória do NPC
**User Story**: Como motor, quero montar o prompt com conhecimento do NPC (não verdade global) para preservar perspectiva local e evitar onisciência.  
**Acceptance Criteria**:
1. WHEN contexto da LLM é montado THEN o sistema SHALL usar apenas memória operacional/episódica/semântica/social/cultural + relatos de crença.
2. WHEN existe divergência Verdade vs Crença THEN o sistema SHALL injetar no prompt somente a versão de Crença.
3. WHEN segredo é conhecido só por outro NPC THEN o sistema SHALL manter esse segredo fora do prompt do NPC atual.

### P1: Validação estrita e aplicação controlada
**User Story**: Como motor, quero aceitar apenas saída válida da LLM e aplicar consequência só após validação completa.  
**Acceptance Criteria**:
1. WHEN a resposta da LLM é recebida THEN o sistema SHALL exigir parse em DTO tipado + validação de schema; falha em qualquer etapa rejeita tudo.
2. WHEN `proposedActions` contém ação fora de `AllowedActions(npc, ctx)` THEN o sistema SHALL rejeitar resposta, logar violação e usar fallback.
3. WHEN DTO válido é aprovado THEN o sistema SHALL aplicar somente efeitos permitidos (memória episódica da conversa e relação NPC↔jogador).
4. WHEN fallback é usado THEN o sistema SHALL não criar fato canônico novo por si só.

### P1: Fallback determinístico e resiliência do tick
**User Story**: Como operador, quero falha degradada de LLM sem travar simulação.  
**Acceptance Criteria**:
1. WHEN provider está indisponível/timeout/erro THEN o sistema SHALL responder com template determinístico.
2. WHEN orçamento por interação é excedido THEN o sistema SHALL interromper chamada externa e responder por fallback.
3. WHEN chamadas de LLM atrasam ou falham THEN o sistema SHALL manter avanço de ticks e hash canônico equivalentes ao cenário sem conversa.

### P2: Segurança de rede e injeção
**User Story**: Como mantenedor, quero gate que detecte prompt injection e qualquer egress de rede indevido.  
**Acceptance Criteria**:
1. WHEN `scripts/verify.sh` roda THEN o sistema SHALL bloquear egress de rede por runtime guard; tentativa de conexão deve lançar exceção.
2. WHEN corpus versionado de injeção é executado THEN o sistema SHALL validar cada fixture com: ações permitidas, hash canônico inalterado e zero campos fora do schema.
3. WHEN flag de teste desliga validador THEN o sistema SHALL fazer o critério de segurança falhar (par de mutação obrigatório).

### P2: Compactação de memória em lote
**User Story**: Como operador, quero reduzir memória volátil antiga sem tocar memória canônica importante.  
**Acceptance Criteria**:
1. WHEN job de compactação roda THEN o sistema SHALL reduzir memórias antigas de baixa importância fora do caminho crítico do tick.
2. WHEN memória tem importância >= limiar canônico THEN o sistema SHALL preservar exatamente o conjunto de IDs antes/depois.
3. WHEN compactação conclui THEN o sistema SHALL manter hash canônico inalterado.

## Edge Cases
- WHEN DTO chega truncado ou com `emotion` inválida THEN sistema SHALL rejeitar integralmente, sem autocorreção heurística.
- WHEN duas mensagens do jogador chegam fora de ordem THEN sistema SHALL serializar por sessão com ordenação determinística por `TurnId`.
- WHEN NPC morre durante sessão aberta THEN sistema SHALL encerrar sessão e bloquear novos turnos.
- WHEN o jogador tenta iniciar conversa com NPC em ação incompatível e sem disponibilidade THEN sistema SHALL rejeitar sem alterar a ação do NPC.
- WHEN sessão é retomada em snapshot/replay THEN sistema SHALL reproduzir os mesmos resultados para mesma seed e mesmos inputs.

## Requirement Traceability
| Requirement ID | Story | Status |
| --- | --- | --- |
| LLM-01..03 | Sessão de conversa segura | Pending |
| LLM-04..06 | Contexto por crença e memória | Pending |
| LLM-07..10 | Validação estrita e aplicação controlada | Pending |
| LLM-11..13 | Fallback determinístico e resiliência | Pending |
| LLM-14..16 | Segurança de rede e injeção | Pending |
| LLM-17..19 | Compactação de memória em lote | Pending |

## Success Criteria
- [ ] Conversa com NPC vivo funciona por endpoint sem acesso do jogo à consulta de Verdade.
- [ ] Qualquer saída inválida da LLM é rejeitada com fallback e hash canônico estável.
- [ ] Corpus de injeção + bloqueio de rede passam no gate com par de mutação efetivo.
- [ ] Compactação reduz memória volátil e preserva memória canônica por ID.
