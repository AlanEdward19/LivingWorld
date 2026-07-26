# Fase 10 — Interação com LLM

**Objetivo**: um jogador seleciona um NPC vivo, conversa com ele por texto e a LLM
gera só linguagem — o motor valida tudo e é o único que escreve no mundo. Fecha o
**objetivo técnico #3**.

## Tasks
1. **Memória do NPC completa**: operacional (volátil), episódica (o que viveu),
   semântica (o que sabe), social (o que pensa de quem) e cultural (o que a comunidade
   crê). Todo registro tem importância (0–100), tick de origem, participantes e local.
2. **Recuperação ponderada**: `Recall(npc, query, n)` pontua por importância + recência
   + relevância, com pesos declarados no cenário. Empate desempata por ID de memória.
3. **Montagem de contexto a partir da crença**: o prompt nasce do `NpcKnowledge` —
   memórias recuperadas e relatos que o NPC ouviu **na versão distorcida que ele guarda**
   (Fase 9), relações conhecidas, local, necessidades. Nunca do `WorldState`, nunca da
   consulta de verdade. Verdade e crença são duas consultas e só a crença chega ao prompt.
4. **Seleção e sessão de conversa**: endpoint para abrir conversa com um NPC, enviar fala
   e encerrar; histórico por sessão. A sessão é presa a um NPC e a um tick — o mundo
   continua avançando por fora. Cliente visual é a Fase 14; aqui basta o endpoint.
5. **Provider real + ADR-0007**: escolher Ollama local e/ou Claude API, medir custo por
   interação e latência p95, e registrar a decisão em `ADR-0007-provider-llm-real.md`.
   `FakeLlmProvider` continua sendo o provider dos testes.
6. **Validação estrita da saída**: parse para DTO tipado → schema → `proposedActions`
   conferido contra `açõesPermitidas(npc, ctx)`, a lista fechada do que **aquele** NPC pode
   executar **naquele** contexto. Qualquer falha = rejeição inteira + log + fallback. A
   validação é uma etapa única, com flag de teste que a desliga — só para o par de mutação.
7. **Aplicação de consequências pelo motor**: só a partir do DTO **validado** o motor grava
   memória episódica da conversa e atualiza a relação NPC↔jogador. Resposta de fallback é
   texto de tela, não fato do mundo — não nasce memória dela.
8. **Fallback determinístico**: provider indisponível, timeout, saída inválida ou orçamento
   estourado → resposta por template a partir do estado do NPC. A conversa degrada, o
   mundo não trava.
9. **Compactação de memória em lote**: job periódico que resume memórias antigas de baixa
   importância, fora do caminho crítico do tick. Nunca inventa fato novo e nunca toca
   memória com importância ≥ o limiar canônico do cenário.
10. **Corpus de injeção versionado**: `tests/fixtures/prompt-injection/*.json` com ≥ 20
    entradas (falas de jogador tentando virar diretiva de sistema), cada uma com os asserts
    esperados. Enumerado por reflexão — entrada nova sem cobertura reprova.
11. **Bloqueio de rede no gate**: `HttpMessageHandler` e resolver de DNS de teste que
    **lançam** em qualquer saída de rede. Bloqueio no runtime, não lista de tipos banidos:
    cobre HttpClient, socket, gRPC, WebSocket e o transporte que ainda não existe.

## Critérios de verificação
- Saída de LLM que tenta alterar atributo, criar item, mover NPC ou matar alguém é
  **rejeitada e logada**, e o **hash canônico** antes == depois, byte a byte.
  **Par de mutação**: com a flag que desliga o validador, **este** critério falha. Se ele
  passar com o validador desligado, não estava medindo nada — a LLM só não tinha caminho.
- Corpus de injeção: para **cada** uma das ≥ 20 entradas, três asserts objetivos —
  `proposedActions ⊆ açõesPermitidas(npc, ctx)`, hash canônico inalterado, **zero** campos
  fora do schema. O teste nunca compara strings nem classifica a prosa gerada.
- Contexto montado usa a crença: um fato que o NPC conhece em versão distorcida entra no
  prompt **na versão distorcida**, e o teste falha se a versão verdadeira aparecer no
  prompt. **Par de mutação**: trocar a consulta de crença pela de verdade faz este critério
  falhar.
- Contexto montado para o NPC A **não contém** fato secreto conhecido só pelo NPC B
  (o teste planta o segredo em B e asserta ausência no prompt).
- Loop de tick imune ao provider: um fake com atraso injetado de K ticks e outro que lança
  a cada chamada. Contagem de ticks executados == esperado **e** hash canônico idêntico ao
  de rodar a mesma seed **sem conversa nenhuma** — chamada pendurada ou rejeitada não
  produz DTO validado, logo não produz consequência.
- `scripts/verify.sh` roda com `FakeLlmProvider` e o bloqueio de rede armado; um teste que
  tenta abrir conexão de propósito **falha por exceção do bloqueio** (prova que está ligado).
- `Recall(npc, query, 5)` devolve as mesmas 5 memórias, na mesma ordem, em duas execuções
  do mesmo mundo semeado.
- DTO sem `dialogue`, com `emotion` desconhecida ou com JSON truncado → rejeitado inteiro,
  nunca "consertado".
- Compactar memória de um NPC com 1000 registros reduz a contagem, muda **só o hash
  volátil** e deixa o **hash canônico** intacto; o conjunto de IDs de memória com
  importância ≥ limiar do cenário é **exatamente** o mesmo antes e depois (comparação de
  conjunto, não de contagem). Memória abaixo do limiar é volátil por definição — é isso que
  torna a compactação livre; acima do limiar ela é canônica e a compactação não a alcança.

## Fora do escopo
Narrativa gerada (crônicas, relatos, rumores) é Fase 11. Voz e conversa 3D são Fase 13.
Cliente visual é Fase 14. LLM tomando decisão de comportamento de NPC em background não
entra em fase nenhuma.

## Ver também
[memory.md](../domain/memory.md) ·
[historical-memory.md](../domain/historical-memory.md) ·
[llm-contract.md](../domain/llm-contract.md) ·
[ADR-0004](../adr/ADR-0004-abstracao-de-provider-llm.md) ·
[rules/llm-boundary.md](../../rules/llm-boundary.md)
