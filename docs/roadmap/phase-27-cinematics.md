# Fase 27 — Motor cinematográfico

**Objetivo**: assistir. A rotina de um NPC vivo, um ano da vida dele, ou a vida inteira de
alguém que morreu há séculos — montadas a partir do event log e dos relatos. Começa em
**texto**, evolui para **animação 2D** de sprites composicionais, e deixa o caminho aberto
para 3D sem reescrever o stream de cena.

O Tier B do log (a rotina miúda) é **descartado** pela retenção do ADR-0007. Logo, a
cinemática de um NPC vivo ou recente tem detalhe completo, e a de um morto há muito tempo só
pode ser reconstruída a partir do **relato** — que pode ser falso. Isso não é limitação, é a
feature: o filme de uma lenda é uma dramatização, o motor sabe disso, e a interface diz ao
espectador qual é o caso e quão confiável é a fonte.

> **Spec, não gate.** Os critérios abaixo são a intenção; os critérios finais são escritos
> sob `rules/eval-criteria.md` quando a fase for ativada. Não comece esta fase antes das
> Fases 10 e 12 fecharem.

## Tasks
1. **Stream de cena a partir do log**: sequência estruturada de quem, onde, o quê, com quem
   e quando. Dado, nunca texto — o stream não conhece renderizador.
2. **Recorte e câmera**: um dia, um ano, uma vida; centrado num NPC, num lugar ou num evento
   marcante. O recorte é uma consulta indexada, não uma varredura do log.
3. **Fonte declarada por cena**: Tier A, Tier B ou relato — e, no caso de relato, o meio, a
   cadeia de transmissão e a distorção acumulada. Cena sem fonte não é emitida.
4. **Fidelidade declarada derivada da fonte**, exposta ao espectador: gravação (evento com
   detalhe retido), reconstrução (esqueleto sem rotina) ou dramatização (só relato). O
   rótulo é calculado, nunca escolhido pelo renderizador.
5. **Divergência visível**: quando duas comunidades sustentam versões incompatíveis, o
   recorte oferece as duas e diz de quem é cada uma.
6. **Renderizador de texto primeiro**, reusando os templates determinísticos da Fase 12.
   Caminho padrão e fallback quando o provider de LLM falha.
7. **Renderizador 2D depois**: sprites composicionais montados dos traços canônicos de
   aparência que vêm no stream. O traço é o dado; o sprite é derivado e volátil (ADR-0014).
8. **Contrato de renderização estável**: o stream é a fronteira. Um renderizador 3D entra
   depois como terceiro consumidor, sem tocar em nada a montante.
9. **Lacuna declarada em vez de invenção**: recorte sem cobertura suficiente devolve o
   buraco marcado. Nem o motor nem a LLM preenchem o que não existe.

## Critérios de verificação
- **Nenhuma cena inventada**: toda cena do stream resolve para um `eventId` existente no log
  **ou** para um `relatoId` existente no cânone, e declara qual. Falha se alguma cena ficar
  sem fonte declarada ou apontar para um id inexistente. Mesma família do critério de
  ancoragem da Fase 12.
- **A fidelidade declarada bate com a fonte usada**: NPC vivo rende cenas de Tier A+B com
  fidelidade de gravação; NPC morto além da janela de retenção rende cenas de relato com
  fidelidade de dramatização. Par na mesma seed: forçar a coleta do Tier B por flag de teste
  tem de **mudar o rótulo** do mesmo recorte de gravação para dramatização. Se não mudar, o
  rótulo é decoração e não mede nada.
- **Renderizar não muda o mundo**: 1000 ticks com renderização contínua de recortes produzem
  o mesmo hash canônico que 1000 ticks sem renderizar nada.
- **O mesmo recorte produz a mesma cena**: mesma seed, mesmo recorte, dois processos
  separados — o stream serializado é byte-idêntico. Comparação sobre o stream, nunca sobre a
  imagem ou a prosa.
- **Traço é canônico, sprite é volátil** (ADR-0014): trocar a versão do renderizador ou
  regerar todos os sprites **não muda** o hash canônico; mutar por reflexão qualquer traço de
  aparência (altura, cor, marcas) **muda** — ele é genético e alimenta atração e
  reconhecimento. Falha se algum traço ou algum campo de sprite ficar sem cobertura.
- **O contrato é estável**: o renderizador de texto e o renderizador 2D consomem o mesmo
  recorte e o stream serializado é byte-idêntico nos dois. Se trocar de renderizador mudar o
  stream, o contrato vazou para a apresentação e o 3D custará uma reescrita.

## Fora do escopo
Cliente 3D, animação e voz: Fase 14. Prosa de crônica, jornal e biografia: Fase 12 — aqui
ela é reusada, não reinventada. A distorção em si: Fase 10. Busca, índice de eventos
marcantes e controles de tempo: Fase 26. Encarnação e sessão de jogador: Fase 25. Arte,
estilo visual e produção de sprites não têm gate.

## Questões em aberto
- Onde fica a fronteira entre "recente" e "antigo"? É exatamente a janela de retenção do
  Tier B, ou a cinemática declara uma janela própria e paga por reter mais?
- A dramatização mostra o que o relato **diz**: se a inflação de magnitude transformou 200
  mortos em 2.000, a cena desenha 2.000 corpos, ou desenha 200 e anota a inflação? A primeira
  opção é honesta com a crença; a segunda é honesta com o espectador.
- Havendo versões divergentes, qual a câmera usa por padrão — a da cultura do espectador, a
  da cultura do NPC retratado, ou as duas lado a lado sempre?
- Uma vida inteira em 2D é caro. O recorte "vida" é resumo por significância, ou reprodução
  completa acelerada — e nesse caso, de onde vem o detalhe dos anos cujo Tier B já sumiu?

## Ver também
[historical-memory.md](../domain/historical-memory.md) ·
[history.md](../domain/history.md) · [memory.md](../domain/memory.md) ·
[npc.md](../domain/npc.md) · [llm-contract.md](../domain/llm-contract.md) ·
[ADR-0007](../adr/ADR-0007-memoria-historica-degradavel.md) ·
[ADR-0006](../adr/ADR-0006-snapshot-mais-event-log.md) ·
[ADR-0014](../adr/ADR-0014-canonico-vs-volatil.md) ·
[rules/database-entities.md](../../rules/database-entities.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md)
