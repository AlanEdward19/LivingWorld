# Fase 12 (Narrativa) Specification

## Problem Statement
A fase 10 introduz fatos e relatos degradáveis, mas o mundo ainda não transforma essa história em saídas legíveis para jogo e observação. Sem uma camada narrativa ancorada em evidência, o sistema corre risco de produzir texto plausível sem lastro em eventos, confundindo verdade canônica com crença e quebrando a fronteira do motor. Esta fase define narrativa como renderização de relatos já estruturados pelo motor: a LLM só escreve prosa, nunca decide fatos ou distorções.

## Goals
- [ ] Produzir crônicas/jornais periódicos por local e período com ancoragem explícita em eventos.
- [ ] Produzir biografia de NPC a partir da linha do tempo pessoal, preservando ordem cronológica e limites de vida.
- [ ] Tratar rumor, tradição, crônica e livro como o mesmo tipo de relato com meios de transmissão diferentes.
- [ ] Permitir crença derivada de relatos (inclusive falsos) sem misturar consulta de crença com verdade canônica.
- [ ] Garantir fallback determinístico sem LLM com mesma estrutura de relato e mesma cadeia de distorção.

## Out of Scope
| Feature | Reason |
| --- | --- |
| Conversa interativa jogador↔NPC e ações propostas em diálogo | Fase 11 |
| Decidir/aplicar operadores de distorção histórica | Fase 10 |
| Voz/leitura falada e apresentação audiovisual avançada | Fase 14 |
| Cliente visual React/TS de exploração narrativa | Fase 15 |

## Assumptions & Open Questions
| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Unidade narrativa canônica | Todo texto publicado deriva de `claims[]` com `eventIds[]` não vazio | Impede prosa sem evidência e habilita gate estrutural | n |
| Geração com e sem LLM | Pipeline estruturado é idêntico; só a superfície textual muda | Preserva determinismo e fronteira da LLM | n |
| Scheduling de narrativa | Geração periódica fica fora do tick diário (>= Monthly) | Evita regressão de custo no caminho quente | n |
| Persistência de crença | Relato abaixo de limiar de confiança não entra em memória semântica do ouvinte | Separa exposição de aceitação de informação | n |
| Open questions | none | Ambiguidades operacionais da fase foram fechadas por defaults acima | n/a |

## User Stories
### P1: Narrativa ancorada em eventos
**User Story**: Como jogador/observador, quero ler narrativa natural (crônica, jornal, resumo) sem perder o vínculo com os eventos que a originaram.  
**Acceptance Criteria**:
1. WHEN um texto narrativo é gerado THEN o sistema SHALL produzir primeiro `claims[]` estruturados contendo `text` e `eventIds[]`.
2. WHEN um claim não possui `eventIds[]` válidos THEN o sistema SHALL descartar o claim e registrar falha de ancoragem.
3. WHEN a prosa final é renderizada THEN o sistema SHALL usar exclusivamente claims aprovados, sem inserir conteúdo fora de claims ancorados.
4. WHEN numeral ou nome próprio aparece no texto final THEN o sistema SHALL comprovar origem em evento referenciado por algum claim.

### P1: Crônica periódica por relevância histórica
**User Story**: Como operador do mundo, quero gerar crônica por janela temporal citando fatos realmente salientes.  
**Acceptance Criteria**:
1. WHEN uma janela de N anos é solicitada para local X THEN o sistema SHALL agregar e ordenar eventos por significância antes da renderização textual.
2. WHEN a crônica da janela é publicada THEN o sistema SHALL referenciar pelo menos um `eventId` entre os K eventos mais significativos da janela.
3. WHEN o agregador encontra fatos relevantes THEN o sistema SHALL reprovar saída genérica de preenchimento ("nada digno de nota") sem citação relevante.
4. WHEN provider de LLM está indisponível THEN o sistema SHALL publicar crônica determinística via template com o mesmo conjunto de claims ancorados.

### P1: Relato único com distorção determinística
**User Story**: Como designer de simulação, quero que rumor, tradição, crônica e livro sejam meios de transmissão de um mesmo relato.  
**Acceptance Criteria**:
1. WHEN um relato muda de meio de transmissão THEN o sistema SHALL manter identidade do relato e aplicar somente operadores de distorção do motor.
2. WHEN a mesma seed e o mesmo mundo são executados THEN o sistema SHALL reproduzir a mesma cadeia de ouvintes, operadores e ordem de distorção.
3. WHEN LLM está ligada ou desligada THEN o sistema SHALL manter idênticos os operadores aplicados e os `eventIds` ancorados; apenas a prosa pode variar.
4. WHEN N saltos de transmissão ocorrem THEN o sistema SHALL manter o fato canônico byte-idêntico e distância relato↔fato não decrescente ao longo dos saltos.

### P1: Crença separada de verdade
**User Story**: Como sistema social, quero que NPCs possam agir com base em informação falsa sem contaminar a verdade do motor.  
**Acceptance Criteria**:
1. WHEN um NPC recebe relato com confiança abaixo do limiar THEN o sistema SHALL não inserir esse relato na memória semântica do ouvinte.
2. WHEN um NPC recebe relato acima do limiar THEN o sistema SHALL persistir crença no espaço de memória do NPC sem alterar o fato canônico de origem.
3. WHEN consultas de jogo e de motor são executadas THEN o sistema SHALL manter APIs separadas para crença (jogo) e verdade (motor), sem mistura de resultados.

### P2: Biografia de NPC
**User Story**: Como jogador, quero abrir biografia de um NPC e entender sua trajetória em ordem temporal.  
**Acceptance Criteria**:
1. WHEN a biografia de um NPC é solicitada THEN o sistema SHALL listar eventos onde o NPC participa em ordem cronológica.
2. WHEN o NPC já morreu THEN o sistema SHALL impedir inclusão de eventos posteriores ao tick de morte.
3. WHEN a biografia é renderizada sem LLM THEN o sistema SHALL usar template determinístico preservando os mesmos `eventIds` de origem.

### P2: Endpoints de leitura narrativa
**User Story**: Como consumidor de API, quero endpoints para listar crônicas, ler biografias e consultar relatos em circulação.  
**Acceptance Criteria**:
1. WHEN `GET /narratives/chronicles` é chamado com local/período THEN o sistema SHALL retornar itens com texto e metadados de ancoragem.
2. WHEN `GET /narratives/biographies/{npcId}` é chamado THEN o sistema SHALL retornar linha do tempo narrativa e referências estruturadas dos eventos.
3. WHEN `GET /narratives/reports` é chamado THEN o sistema SHALL retornar relatos em circulação com nível de confiança e origem de transmissão.

## Edge Cases
- WHEN dois jobs narrativos concorrentes processam a mesma janela THEN sistema SHALL garantir idempotência da publicação por chave `(local, periodStart, periodEnd)`.
- WHEN evento referenciado por claim não existe mais no armazenamento quente THEN sistema SHALL buscar no arquivo frio/índice histórico antes de reprovar claim.
- WHEN relato é transmitido mas não aceito por confiança THEN sistema SHALL registrar exposição sem mutar memória semântica do ouvinte.
- WHEN geração narrativa roda por 10 anos simulados THEN sistema SHALL não executar sistema narrativo no tick diário.

## Requirement Traceability
| Requirement ID | Story | Status |
| --- | --- | --- |
| NARR-01..04 | Narrativa ancorada em eventos | Pending |
| NARR-05..08 | Crônica periódica por relevância histórica | Pending |
| NARR-09..12 | Relato único com distorção determinística | Pending |
| NARR-13..15 | Crença separada de verdade | Pending |
| NARR-16..18 | Biografia de NPC | Pending |
| NARR-19..21 | Endpoints de leitura narrativa | Pending |

## Success Criteria
- [ ] Toda narrativa publicada tem cobertura de claims ancorados e reprova automaticamente em ausência de evidência.
- [ ] Com `NullLlmProvider`, crônica/biografia/relato continuam disponíveis com mesma estrutura de ancoragem e mesma cadeia de distorção.
- [ ] Consultas de jogo usam crença e não expõem verdade canônica por engano.
- [ ] Custo da narrativa permanece fora do caminho quente diário com agendamento periódico.
