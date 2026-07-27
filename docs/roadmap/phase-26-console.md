# Fase 26 — Console de análise e modo god

**Objetivo**: uma superfície de análise e autoria **separada da API de jogo**. Pausar e
escolher a velocidade; pesquisar cidades, cidadãos e potências — que são dinâmicas, com
nomes que só existem porque emergiram (Fase 24); listar eventos marcantes e ler sobre eles
com todos os documentos atrelados; e, só em modo god, **reescrever um fato**, o que
**ramifica** (ADR-0008) e cria uma linha nova. Nunca `UPDATE` no passado.

> **Spec, não gate.** Os critérios abaixo são a intenção; os critérios finais são escritos
> sob `rules/eval-criteria.md` quando a fase for ativada. Não comece esta fase antes da
> Fase 18 fechar.

## Tasks
1. **Controles de tempo reusando a Fase 1**: pausar, retomar, velocidade em ticks por segundo
   real, avanço de N ticks — **globais por branch e exclusivos do admin** (ADR-0015), com
   aviso a todo jogador conectado (Fase 25). Nada disso entra no snapshot: é estado do
   hospedeiro, não do mundo.
2. **Índice de entidades**: cidades, cidadãos, potências, tecnologias, doenças e cultos,
   indexados **no nascimento da entidade**, pelo identificador estrutural. O índice não lê
   catálogo: quem só existe porque emergiu em runtime tem de ser encontrável.
3. **Nomes emergentes na busca**, incluindo os divergentes: a mesma entidade pode ser
   procurada pelo nome que qualquer cultura lhe deu (ADR-0013 + Fase 10).
4. **Índice de eventos marcantes** ordenado pela significância calculada na Fase 10 —
   primeiro contato, guerra, epidemia, fundação, colapso.
5. **Leitura de evento marcante**: devolve o esqueleto do fato **e** todos os documentos
   atrelados (livro, crônica, canção, monumento, registro oficial), com a proveniência e a
   divergência entre eles quando as versões se contradizem.
6. **Visão de Verdade exclusiva do admin**, em rota e autorização próprias, fisicamente
   separada da consulta de crença. O critério de vazamento da Fase 10 continua valendo.
7. **Reescrita de fato em modo god = intervenção que ramifica**: cria branch a partir do
   snapshot do tick alvo, com `seed_B = H(seed_A, tick, id_intervenção)`. A linha-mãe fica
   intocada. Rebuild é o replay da linha nova, não uma edição da antiga.
8. **Diff entre a linha original e a reconstruída**: divergência de estado e de eventos,
   amostrada por significância para que comparar séculos não vire um dump.
9. **Auditoria em dois lugares** (ADR-0015): **leitura** (busca, inspeção, visão de verdade,
   pausa, velocidade) vai para um store **separado**, fora do hash canônico, com o **mesmo**
   invariante append-only — o teste que exige `UPDATE` e `DELETE` falhando vale igual naquela
   tabela. **Intervenção** não tem trilha paralela: ela já é fato do mundo e ramifica
   (ADR-0008), e o evento de gênese do branch filho registra quem interveio, quando e sobre
   qual fato.

## Critérios de verificação
- **Pausado, o mundo não anda**: com a simulação pausada, N ticks de tempo real passam e o
  contador de ticks do mundo e o hash canônico ficam idênticos. Retomar volta a andar.
- **Reescrever não altera a linha original**: após a intervenção em modo god, o snapshot e o
  log da linha-mãe são **byte-idênticos** aos de antes, e existe um branch novo cujo hash
  diverge a partir do tick alvo. Mesmo critério da Fase 18; sem a metade byte-idêntica, um
  `UPDATE` silencioso passaria.
- **Nenhuma rota de admin é alcançável pela API de jogo**: enumerar por reflexão **todas** as
  rotas expostas e exigir que cada rota de admin exija a autorização de admin — falha se
  alguma rota ficar sem cobertura. Par de mutação: desligar a autorização por flag de teste
  tem de **fazer este critério falhar**.
- **A busca acha o que nunca esteve em catálogo**: cenário roda até emergir uma potência em
  runtime, com a LLM desligada e o rótulo vindo do fallback determinístico; a busca encontra
  a entidade pelo identificador estrutural e pelo rótulo. Reprovar se o índice depender de
  qualquer arquivo de catálogo.
- **Evento marcante devolve as versões, não uma versão**: no cenário com duas comunidades
  que sustentam versões incompatíveis do mesmo fato, a consulta devolve os dois documentos e
  marca a divergência. Resposta com versão única reprova.
- **Toda ação de admin fica registrada, cada uma no seu lugar**: enumerar por reflexão todas
  as rotas de admin; cada rota de **leitura** exige entrada no store separado, e cada rota de
  **intervenção** exige o evento de gênese do branch filho nomeando quem, quando e qual fato.
  Falha se alguma rota não registrar ou ficar sem cobertura na enumeração.
- **Admin que só lê não altera o hash canônico de mundo nenhum**: exercer toda a superfície
  de leitura (busca, inspeção, visão de verdade, pausa, velocidade) deixa o hash canônico de
  todos os branches idêntico ao de um braço sem admin — é o que preserva a comparabilidade
  entre mundos. E `UPDATE` ou `DELETE` na tabela de auditoria retornam `Failure`.

## Fora do escopo
Ramificação, inércia histórica, âncora e coleta: Fase 18 — aqui só são consumidas.
Cinemática e reprodução de cenas: Fase 27. Mapa e camadas visuais: Fase 15. Geração de prosa
sobre o que foi encontrado: Fase 12. Regras de encarnação e sessão de jogador: Fase 25.
Distorção de relato: Fase 10. Ergonomia e layout do console não têm gate.

## Questões em aberto
- O branch criado em modo god precisa de âncora para persistir (ADR-0008). O admin conta como
  âncora, ou a linha é coletada assim que ele fecha a aba?
- Quanto tempo pode durar uma pausa de admin? Um admin distraído congela todos os jogadores
  do branch (ADR-0015), e hoje a pausa não tem teto declarado nem expiração.
- A auditoria vive em dois lugares (store de leitura; gênese do filho para intervenção): como
  juntar os dois para responder "o que o admin X fez", e sob qual relógio se ordena a junção?
- Busca por nome divergente: procurar pelo nome que **uma** cultura usa deve achar a entidade
  para qualquer admin, ou a visão de admin também respeita o recorte cultural?
- Diff entre linhas amostrado por significância pode esconder exatamente a mudança que o
  admin quis fazer. Existe um modo exato, e ele é viável em séculos de divergência?

## Ver também
[timelines.md](../domain/timelines.md) ·
[historical-memory.md](../domain/historical-memory.md) ·
[history.md](../domain/history.md) · [time-and-ticks.md](../domain/time-and-ticks.md) ·
[cities.md](../domain/cities.md) ·
[ADR-0008](../adr/ADR-0008-ramificacao-como-modelo-temporal.md) ·
[ADR-0007](../adr/ADR-0007-memoria-historica-degradavel.md) ·
[ADR-0013](../adr/ADR-0013-emergencia-aberta-motor-estrutura-llm-nome.md) ·
[ADR-0015](../adr/ADR-0015-pausa-global-e-auditoria-de-admin.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md)
