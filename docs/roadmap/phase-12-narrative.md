# Fase 12 — Narrativa

**Objetivo**: o passado vira texto legível — crônicas, biografias e rumores. A narrativa
renderiza **relatos** (possivelmente falsos), não fatos: o motor já distorceu, a LLM só
põe em prosa. Rumor e crônica são o mesmo mecanismo em meios diferentes.

## Tasks
1. **Saída estruturada antes da prosa**: o gerador devolve `claims: [{texto, eventIds[]}]`
   e só então renderiza. Claim sem `eventIds` não é publicado — descartado com log. A prosa
   final é função dos claims; nada entra no texto que não passou por um claim ancorado.
2. **Resumo histórico por período**: agregador que pega a janela de N anos de um local e
   devolve os fatos salientes já ordenados por significância (secas, guerras, fundações,
   mortes notáveis), antes de qualquer geração de texto. O ranking é dado, não estilo.
3. **Crônica/jornal periódico**: job em lote, fora do caminho crítico, que transforma o
   agregado em prosa. Exemplo: seca de 3 anos + queda de produção + migração + revolta →
   "Após três colheitas perdidas, os moradores de Valen se revoltaram contra o rei."
4. **Narrativa por template determinístico**: cada tipo de agregado tem um template. É o
   caminho padrão sem LLM e o fallback quando o provider falha. Mesma entrada, mesmo texto.
5. **Biografia de NPC**: linha do tempo pessoal a partir dos eventos em que o NPC é
   participante — nascimento, família, profissão, feitos, morte.
6. **Relato como unidade única**: rumor, tradição, crônica e livro são o **mesmo** relato
   em meios diferentes (ver `historical-memory.md`). Cada salto de transmissão aplica os
   operadores de distorção do motor, semeados e determinísticos. A LLM nunca escolhe a
   distorção — ela recebe o relato já distorcido e escreve.
7. **Propagação e crença**: o relato entra na memória semântica do ouvinte com uma
   confiança; o NPC pode agir sobre informação falsa. É feature — o esqueleto do fato fica
   no motor, a crença fica no NPC, e as duas consultas nunca se misturam.
8. **Endpoint de leitura**: listar crônicas por local e período, ler biografia, ver relatos
   em circulação. A verdade de origem só aparece na consulta de motor, nunca na de jogo.

## Critérios de verificação
- **Ancoragem estruturada**: para cada `claim`, `eventIds` é não vazio e todo id existe no
  log. Além disso, **todo numeral e todo nome próprio** do texto final aparece em algum
  evento ancorado por aquele claim — numeral ou nome órfão reprova. A asserção é sobre a
  estrutura; "afirmação" nunca é extraída de prosa livre.
- **Relevância, não preenchimento**: a crônica de uma janela cita, por `eventId`, **≥ 1**
  dos K eventos mais significativos daquela janela segundo o agregador da task 2. Um
  template "nada digno de nota ocorreu" reprova — antes ele passava por não ser vazio.
- Desligar a LLM (`NullLlmProvider`) ainda produz crônica, biografia e descrição por
  template **a partir do mesmo relato distorcido**: o conjunto de `eventIds` ancorados e os
  operadores de distorção aplicados são idênticos aos da execução com LLM. Só a prosa muda.
- A mesma seed produz a **mesma** cadeia de distorção com a LLM ligada e desligada —
  comparação sobre o relato estruturado (operadores aplicados, na ordem, por salto), nunca
  sobre texto. Se ligar a LLM mudar um operador, ela está escolhendo a distorção: reprova.
- **Monotonicidade da distorção**: relato em 5 saltos — o evento no log continua
  byte-idêntico, `distância(fato, versão_n)` é **não decrescente** em n e **estritamente
  > 0** a partir do salto declarado no cenário. Distorcer só o primeiro salto reprova.
- Relato com confiança abaixo do limiar do cenário não entra na memória semântica do
  ouvinte.
- Duas execuções do mesmo mundo semeado geram a mesma cadeia (mesmos ouvintes, mesmos
  operadores, mesma ordem).
- Biografia de um NPC morto lista os eventos dele em ordem cronológica, sem nenhum evento
  posterior ao tick da morte.
- **Ler não muda, transmitir muda**: gerar narrativa de 10 anos de mundo não altera o hash
  canônico; transmitir um relato **altera** (a crença de alguém mudou). Se as duas
  operações derem o mesmo hash, a crença está fora da conta e o hash não protege esta fase.
- **Custo sem cronômetro**: o sistema de narrativa está registrado com frequência
  ≥ `Monthly` e **não** consta na lista de sistemas executados num tick diário — inspeção
  do registro de sistemas, custo zero. Medir tempo em CI mede o runner, não o código.

## Fora do escopo
Voz e leitura falada são Fase 14. Narrativa não propõe nem aplica ação —
`proposedActions` continua vazio aqui; conversa interativa é Fase 11. A distorção em si é
Fase 10: aqui ela só é renderizada, nunca decidida.

## Ver também
[historical-memory.md](../domain/historical-memory.md) ·
[history.md](../domain/history.md) ·
[memory.md](../domain/memory.md) ·
[llm-contract.md](../domain/llm-contract.md) ·
[ADR-0006](../adr/ADR-0006-snapshot-mais-event-log.md) ·
[rules/llm-boundary.md](../../rules/llm-boundary.md)
