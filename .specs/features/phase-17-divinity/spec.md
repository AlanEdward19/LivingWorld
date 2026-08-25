# Fase 17 — Divindade e economia de crença — Specification

## Problem Statement

Um deus não é uma entidade nova no motor — é a potência da Fase 16 (`powers.md`) acoplada a um
recurso (fiéis) e à camada de crença já existente da Fase 10 (`historical-memory.md`). Hoje não
existe o vínculo: nada liga fiéis a poder, nada faz um deus esquecido decair, nada deixa a
distorção histórica mudar a natureza de uma divindade, e nada impede que "ser cultuado" seja
confundido com "ganhar poder de fato". Esta fase fecha esse vínculo, reusando os motores de
Fase 5 (economia), 8 (cidades/edifícios), 9 (arquivo frio) e 10 (distorção/cânone) sem reescrever
nenhum deles.

## Goals

- [ ] Um `Deity` é um portador de `PowerDescriptor` (Fase 16) com um pool de crença derivado de
      fiéis × devoção × frequência de retransmissão — nunca um campo de poder livre e arbitrário.
- [ ] O ciclo fiéis → poder → manifestação → mais fiéis roda nos dois sentidos: manifestação vira
      relato (Fase 10), relato retransmitido realimenta o pool, ausência de retransmissão drena.
- [ ] Deus sem fiéis decai monotonicamente até poder 0, onde a entidade é coletada (arquivo frio,
      mesmo padrão da Fase 9) — nunca um deus-fantasma eterno ocupando estado.
- [ ] A natureza corrente do deus (um rótulo dentre um conjunto declarado no cenário, ex.:
      Guerra/Colheita/Morte) deriva da doutrina corrente, nunca é campo fixo — os operadores de
      distorção do ADR-0007 (moralização, perda de causa, troca de atribuição, fusão) podem
      trocar o rótulo sem que ninguém "decida" a mudança.
- [ ] Culto é instituição, reusando Fase 5/8: templo é edifício com renda e empregados, sacerdote
      é profissão, dízimo é transação, doutrina é conhecimento transmitido, cisma é divergência
      cultural que spawna um segundo `Deity` com pool próprio.
- [ ] Vários deuses partilham a mesma população: pool de devoção por NPC é conservado (soma ≤ 1
      entre todos os deuses seguidos + parcela "sem-fé"); crescer um culto tira share de outro
      ou da parcela sem-fé.
- [ ] Intervenção divina é invocação de potência (Fase 16): custo em pool quando declarado,
      `Guaranteed` ou `ResolutionCheck` conforme o cenário, com modo de falha com consequência.
      Milagre sem testemunha ainda roda e ainda custa — só não vira relato.
- [ ] `Worshipped` (atribuição social) e `FaithPowered` (vínculo mecânico fiéis→poder) são campos
      independentes — um super-humano cultuado nunca ganha poder de crença automaticamente.
- [ ] Realidade do deus (real / esvaziado / falso / mito em ascensão) só é resolvida pela consulta
      de Verdade da Fase 10 — nenhum handler de jogo responde "esse deus é real?".

## Out of Scope

| Item | Razão |
| --- | --- |
| Potência genérica (eixos, custo, rolagem, aquisição) | Fase 16 — esta fase só consome `PowerDescriptor` já registrado, nunca redefine eixo. |
| Operadores de distorção e cânone limitado | Fase 10 (`ADR-0007`) — aqui só são consumidos sobre a doutrina, nunca reimplementados. |
| Sermão, mito narrado, hagiografia em prosa | Fase 12 (Narrativa) — geração de texto fica lá; aqui só o estado (pool, natureza, doutrina). |
| Culto que venera uma linha temporal perdida | Fase 18 (Timelines) — depende de branch, que não existe ainda. |
| Culto de carga por contato com o desconhecido/alienígena | Fase 19 (Cosmos) — depende de contato, fora do domínio deste ciclo. |
| Herança de mais de 2 "pais" doutrinários num cisma | `NatalitySystem`-like só modela divergência binária por ora; N-way fica pra quando for pedido. |
| UI/visualização de panteão | Cliente é Fase 15; esta fase só expõe estado consultável (mesmo padrão CLI/API da Fase 8). |

## Assumptions & Decisions (confirmadas com o usuário — 2026-08-25)

| Decisão | Escolha confirmada | Racional |
| --- | --- | --- |
| Pool de fiéis | **Conservado por NPC, com parcela "sem-fé"**: cada NPC distribui devoção total entre 0..N deuses + parcela residual sem-fé, soma = 1 | Usuário confirmou explicitamente (Recommended aceito) — modela disputa direta entre cultos e persseguição como transferência de share, não efeito indireto |
| Piso de decaimento | **Zero com coleta**: deus chega a poder 0 e a entidade é arquivada (mesmo padrão de arquivo frio de mortos da Fase 9) | Usuário confirmou explicitamente — evita deus-fantasma barato ocupando estado pra sempre |
| Natureza do deus | **Enum de cenário**: rótulo categórico dentre conjunto declarado no cenário; distorção pode trocar o rótulo corrente | Usuário confirmou explicitamente — mais simples de testar divergência (rótulo A ≠ rótulo B) que vetor contínuo |
| Cisma | **Entidade nova**: spawna um segundo `Deity` com `DeityId` e pool próprios, herdando parte dos fiéis do original | Usuário confirmou explicitamente — panteão cresce organicamente, fiel escolhe/herda qual doutrina segue |
| Milagre sem testemunha | **Roda normal, custa pool** — só não vira relato (Fase 10 exige testemunha pra relato) | Usuário confirmou explicitamente — intervenção divina não sabe se alguém está olhando; evita efeito colateral de perf ditar mecânica |

**Todas as 5 decisões estão confirmadas** — nenhuma pendência bloqueando Design.

## User Stories

### P1: Deus como portador de potência com pool de crença

**User Story**: Como quem desenha um cenário com divindades, quero que um deus seja um
`PowerDescriptor` (Fase 16) acoplado a um pool de crença calculado a partir de fiéis, devoção e
retransmissão — nunca um campo de poder livre — pra que toda a mecânica de invocação já existente
funcione sem reescrita.

**Why P1**: É a fundação — sem o acoplamento, nenhuma outra história desta fase tem onde pendurar.

**Acceptance Criteria**:

1. WHEN um `Deity` é criado THEN o motor SHALL exigir um `PowerDescriptor` válido (mesma
   validação de contrato da Fase 16.1) e um pool de crença inicial derivado de
   `Σ(devoção_i × frequência_retransmissão_i)` sobre os fiéis correntes — nunca um valor
   digitado direto sem fiéis por trás.
2. WHEN o pool de crença é recalculado a cada tick de reavaliação THEN o valor SHALL refletir
   exatamente o fiéis/devoção/retransmissão correntes daquele tick — nenhum campo obsoleto
   sobrevive entre reavaliações.
3. WHEN o `PowerDescriptor` de um `Deity` é usado numa invocação THEN o motor SHALL aplicar o
   mesmo pipeline `Prepare`/`PrepareEffects`/resolução da Fase 16, sem caminho de bypass por
   ser "poder de deus".
4. WHEN a mesma seed e o mesmo histórico de fiéis são usados em duas execuções THEN o pool de
   crença e o estado do `Deity` SHALL ser byte-idênticos entre elas.

**Independent Test**: criar um `Deity` com 50 fiéis de devoção uniforme, invocar um milagre e
confirmar que o custo é debitado do pool calculado a partir dos fiéis correntes, não de um valor
hardcoded.

---

### P1: Realimentação fiéis ⇄ poder e decaimento até coleta

**User Story**: Como quem quer que "deus esquecido morre" seja mecânico e não narrativo, quero
que manifestação vire relato (Fase 10), relato retransmitido realimente o pool, e ausência de
retransmissão drene o poder até 0, onde a entidade é coletada.

**Why P1**: É o ciclo central do domínio (`divinity-and-belief.md`) — sem ele, "deus" é só um
poder com nome bonito, sem o comportamento de morte por esquecimento que justifica a fase.

**Acceptance Criteria**:

1. WHEN um milagre é manifestado com testemunha(s) presente(s) THEN o motor SHALL gerar um
   relato na camada de história da Fase 10, sujeito aos mesmos operadores de distorção/cânone
   de qualquer outro relato.
2. WHEN um relato de manifestação é retransmitido THEN o pool de crença do `Deity` de origem
   SHALL aumentar proporcionalmente à retransmissão — mesmo mecanismo usado no cálculo do pool
   (AC anterior), não uma fórmula paralela.
3. WHEN um `Deity` não recebe retransmissão nova em uma janela declarada no cenário THEN seu
   pool SHALL decair monotonicamente tick a tick (`poder(t+1) ≤ poder(t)`) até atingir 0 — uma
   única subida sem manifestação nem fiel novo é falha de implementação, não comportamento
   esperado.
4. WHEN o pool de um `Deity` atinge exatamente 0 THEN o motor SHALL coletar a entidade (mesmo
   padrão de arquivo frio de mortos da Fase 9) — nenhum `Deity` permanece ativo em memória
   quente com pool 0 além do tick de coleta.
5. WHEN um `Deity` sob perseguição (culto ativamente suprimido, ver P1 seguinte) é comparado a
   um `Deity` baseline na mesma seed THEN a perda de pool do perseguido SHALL exceder o
   decaimento natural do baseline por uma margem maior que o spread observado entre seeds
   distintas do próprio baseline.

**Independent Test**: `Deity` com fiéis ativos por 5 anos, depois cortada toda retransmissão —
pool decai monotonicamente por 10 anos simulados até 0 e a entidade some do índice de vivos,
aparecendo no arquivo frio.

---

### P1: Natureza derivada da doutrina, nunca campo fixo

**User Story**: Como quem quer que a distorção histórica tenha consequência mecânica real sobre
divindades, quero que a natureza corrente de um `Deity` (rótulo do cenário) seja recalculada a
partir da doutrina corrente — a mesma doutrina que os operadores de distorção da Fase 10 alteram
— nunca um campo que alguém escreve uma vez e esquece.

**Why P1**: É o "gancho mais forte do modelo" segundo `divinity-and-belief.md` — sem ele, a fase
não entrega a promessa central ("deus da colheita vira deus da guerra sem decidir").

**Acceptance Criteria**:

1. WHEN a doutrina corrente de um `Deity` é lida THEN o motor SHALL derivá-la aplicando os
   operadores de distorção do ADR-0007 (moralização, perda de causa, troca de atribuição,
   fusão) sobre a doutrina fundadora e o histórico de relatos retransmitidos — nunca um valor
   armazenado direto e mutável por escrita externa.
2. WHEN a doutrina corrente diverge o suficiente da doutrina fundadora (limiar declarado no
   cenário) THEN o rótulo de natureza do `Deity` SHALL trocar para o rótulo do enum de cenário
   mais consistente com a doutrina corrente — sem que nenhum sistema "decida" a troca
   explicitamente.
3. WHEN o mesmo par de doutrina fundadora/histórico roda em dois braços da mesma seed — um com
   os operadores de distorção do ADR-0007 ligados, outro desligados (controle) — THEN o braço
   tratado SHALL divergir da natureza fundadora em pelo menos o número de cultos declarado no
   cenário, e o braço de controle SHALL ter zero divergências.
4. WHEN a mesma seed roda duas vezes com o mesmo histórico THEN a natureza corrente resultante
   SHALL ser byte-idêntica entre execuções.

**Independent Test**: `Deity` fundado como "Colheita", 3 séculos de relatos de guerra
retransmitidos com moralização e perda de causa ativas — natureza corrente diverge para
"Guerra"; braço de controle sem os operadores permanece "Colheita".

---

### P1: Culto como instituição (templo, sacerdócio, doutrina, dízimo, cisma)

**User Story**: Como quem desenha um cenário com religião viva, quero que templo, sacerdote,
doutrina e dízimo reusem os motores de edifício/profissão/conhecimento/transação já existentes
(Fase 5/8), e que cisma spawne um segundo `Deity` com pool próprio herdado do original.

**Why P1**: Confirma que nenhum subsistema novo de "instituição religiosa" é criado — é a
garantia de reuso que evita duplicar economia/sociedade.

**Acceptance Criteria**:

1. WHEN um templo é fundado para um `Deity` THEN ele SHALL ser modelado como edifício (Fase 8)
   com renda e slots de emprego, e o dízimo pago pelos fiéis SHALL ser uma transação econômica
   comum (Fase 5) — nenhum ledger paralelo de "moeda de fé".
2. WHEN um NPC ocupa o cargo de sacerdote de um templo THEN ele SHALL ser modelado como
   profissão comum (Fase 6/5) com salário pago pela renda do templo.
3. WHEN a doutrina de um `Deity` é ensinada/transmitida entre NPCs THEN o mecanismo SHALL ser o
   mesmo de conhecimento transmitido já usado por outras culturas/tradições — nenhum grafo de
   transmissão de doutrina paralelo.
4. WHEN uma comunidade de fiéis diverge da doutrina corrente além de um limiar declarado no
   cenário (cisma) THEN o motor SHALL criar um novo `Deity` com `DeityId` próprio e pool
   próprio, transferindo a parcela de devoção dos fiéis que aderem à nova doutrina (a soma de
   devoção por NPC continua ≤ 1, ver P1 de panteão).
5. WHEN um cisma ocorre THEN o `Deity` original SHALL manter os fiéis que não migraram, com seu
   pool recalculado normalmente pela fórmula de fiéis/devoção correntes (nenhum bônus/penalidade
   arbitrária só por ter havido cisma).

**Independent Test**: templo com sacerdote e dízimo ativo por 1 ano gera renda mensurável;
comunidade dividida artificialmente em doutrina divergente produz um segundo `DeityId` com pool
> 0, e o original continua ativo com pool reduzido proporcionalmente aos fiéis remanescentes.

---

### P1: Panteão com pool de devoção conservado por NPC

**User Story**: Como quem quer disputa religiosa emergente, quero que a devoção de cada NPC seja
um recurso conservado (soma ≤ 1 entre todos os deuses seguidos + parcela sem-fé), de forma que
crescer um culto sempre tire share de outro culto ou da parcela sem-fé — nunca surja poder do
nada.

**Why P1**: Sem conservação, "perseguição rouba fiéis do vizinho" e "sincretismo" não têm base
mecânica — viram só efeitos narrativos soltos.

**Acceptance Criteria**:

1. WHEN a devoção de um NPC é lida THEN `Σ devoção_por_deus(NPC) + devoção_sem_fé(NPC) SHALL`
   ser exatamente 1 (dentro de epsilon de ponto flutuante declarado) — nunca menor nem maior.
2. WHEN um NPC aumenta devoção a um `Deity` (conversão) THEN a devoção SHALL vir
   proporcionalmente de outro(s) `Deity`(s) que o NPC já seguia e/ou da parcela sem-fé — nunca
   criada do zero sem redução em outro lugar.
3. WHEN dois `Deity`s do mesmo panteão competem pela mesma população base THEN o crescimento do
   pool de um SHALL correlacionar-se com a perda de share dos outros ao longo do tempo simulado
   (não é preciso soma-zero perfeita tick a tick — a devoção pode oscilar — mas a tendência
   agregada ao longo de uma janela declarada no cenário deve mostrar a transferência).

**Independent Test**: panteão com 2 `Deity`s e 100 NPCs de devoção inicial dividida — evento de
conversão forçada em metade dos NPCs move devoção mensuravelmente de um deus para o outro,
mantendo a soma por NPC em 1.

---

### P1: Intervenção divina como invocação de potência opcional

**User Story**: Como quem desenha cenários com milagres, quero que uma intervenção divina reuse
os mesmos eixos opcionais de custo/rolagem da Fase 16 — nunca um sistema de milagre paralelo —
e que milagre sem testemunha ainda rode e custe, só não vire relato.

**Why P1**: É a garantia explícita de reuso da Fase 16 ("mesmos eixos opcionais") e resolve a
questão em aberto do esqueleto original.

**Acceptance Criteria**:

1. WHEN um `Deity` invoca uma intervenção THEN o motor SHALL usar o mesmo pipeline de resolução
   opcional da Fase 16 (`Guaranteed` ou `ResolutionCheck` conforme declarado no `PowerDescriptor`
   do deus) — nenhuma rolagem/custo reimplementado para divindade.
2. WHEN uma intervenção declara custo em pool THEN o pool SHALL ser debitado
   independentemente de haver testemunha presente — milagre sem testemunha roda e custa
   normalmente.
3. WHEN uma intervenção com testemunha(s) resulta em falha (modo `ResolutionCheck` reprovado)
   THEN o motor SHALL gerar uma consequência de falha declarada no cenário (presságio ambíguo,
   milagre no fiel errado, sinal caro e inútil) — nunca uma falha silenciosa sem efeito
   observável.
4. WHEN uma intervenção ocorre sem nenhuma testemunha presente THEN nenhum relato SHALL ser
   gerado na Fase 10 (relato exige testemunha, regra já existente lá) — mas o custo em pool e o
   efeito mecânico do milagre SHALL ter ocorrido normalmente.

**Independent Test**: `Deity` com `ResolutionCheck` configurado invoca 20 intervenções em cenário
controlado — subset com testemunha gera relatos correlacionados a sucesso/falha; subset sem
testemunha custa pool igual mas não gera relato algum.

---

### P1: `Worshipped` ≠ `FaithPowered` — cultuado não implica poder de crença

**User Story**: Como quem quer modelar tanto deuses "de fato" quanto humanos extraordinários
cultuados sem saber, quero que ser considerado divino por uma comunidade (`Worshipped`) e ter o
vínculo mecânico fiéis→poder ativo (`FaithPowered`) sejam campos independentes.

**Why P1**: Decisão explícita já registrada no esqueleto do roadmap (item 8b) — evita que todo
super-humano cultuado vire automaticamente uma fonte de poder de crença.

**Acceptance Criteria**:

1. WHEN um NPC é atribuído como divino por uma comunidade (evento social, Fase 10/12) THEN o
   motor SHALL marcar `Worshipped = true` sem alterar `FaithPowered`.
2. WHEN `FaithPowered` não está ligado para um NPC/entidade THEN nenhum pool de crença SHALL ser
   calculado ou debitado para ele, mesmo que `Worshipped = true` e existam fiéis retransmitindo
   relatos sobre ele.
3. WHEN um cenário explicitamente liga `FaithPowered` para uma entidade já `Worshipped` THEN a
   partir daquele tick o pool passa a ser calculado normalmente (mesma fórmula de fiéis/devoção)
   — a ligação é um evento único e explícito, nunca automático por tempo de culto acumulado.
4. WHEN a suíte de teste roda `test-worship-without-faith-power` THEN um NPC cultuado por anos
   SHALL não apresentar nenhum ganho mecânico mensurável atribuível à crença (nenhuma mudança em
   atributos, custo de poder, ou qualquer efeito de `powers.md`) — falha se qualquer efeito
   vazar sem `FaithPowered` ligado.

**Independent Test**: NPC extraordinário cultuado por 50 anos simulados sem `FaithPowered` —
nenhuma métrica de poder/pool muda; ligar `FaithPowered` no ano 51 faz o pool passar a existir e
crescer a partir dali.

---

### P1: Realidade do deus só na consulta de Verdade

**User Story**: Como quem quer preservar a ambiguidade ficcional (deus real vs. mito), quero que
a distinção real/esvaziado/falso/mito-em-ascensão só seja resolvida por um canal de debug/autoria
dedicado — nenhum handler de jogo pode responder "esse deus é real?".

**Why P1**: É a garantia central de indistinguibilidade do domínio (`divinity-and-belief.md`) —
sem ela, o "mistério" é só decorativo e qualquer sistema pode vazar a resposta.

**Acceptance Criteria**:

1. WHEN qualquer handler de consulta de crença/culto é enumerado por reflexão THEN nenhum SHALL
   resolver ou expor o campo de realidade do `Deity` (existência de `PowerDescriptor` real por
   trás) — falha se algum handler ficar sem cobertura na enumeração.
2. WHEN a checagem de não-vazamento é desligada por flag de teste (par de mutação, mesmo padrão
   da Fase 10) THEN o critério de verificação SHALL falhar — provando que o teste de fato
   detecta vazamento e não é tautologia.
3. WHEN um `Deity` esvaziado (pool baixo, sem manifestação recente) e um mito em ascensão (pool
   crescendo, sem manifestação ainda) têm o mesmo pool no mesmo tick e nenhuma manifestação na
   janela declarada THEN toda a superfície de consulta de crença acessível a handlers de jogo
   SHALL retornar respostas byte-idênticas entre os dois — qualquer divergência é vazamento.
4. WHEN a visão de Verdade da Fase 10 (canal de debug/autoria) é consultada para um `Deity`
   THEN ela SHALL retornar corretamente se há `PowerDescriptor` real por trás (deus real/
   esvaziado) ou não (deus falso/mito) — este é o único canal autorizado a responder.

**Independent Test**: par de cenários (deus real esvaziado vs. mito em ascensão) convergindo pro
mesmo pool sem manifestação — toda API/consulta de jogo retorna idêntico; só a visão de Verdade
distingue os dois corretamente.

## Edge Cases

- WHEN um `Deity` tem pool > 0 mas 0 fiéis correntes (todos morreram/converteram simultaneamente)
  THEN o motor SHALL tratá-lo como sem retransmissão possível e iniciar o decaimento normal até
  coleta — nenhum estado "fiéis fantasma" sustenta o pool.
- WHEN dois cultos de doutrinas divergentes (pós-cisma) competem pela mesma comunidade THEN a
  soma de devoção por NPC SHALL continuar ≤ 1, mesmo com os dois `Deity`s ativamente convertendo.
- WHEN um milagre é invocado com pool insuficiente para o custo declarado THEN o motor SHALL
  recusar a invocação com falha declarada (mesmo padrão de recusa por recurso insuficiente já
  usado em Fase 16/5) — nunca debitar pool para negativo.
- WHEN a doutrina fundadora de um `Deity` nunca sofre nenhuma retransmissão distorcida (culto
  isolado, sem contato externo) THEN a natureza corrente SHALL permanecer igual à fundadora
  indefinidamente — divergência exige histórico de distorção real, nunca decai por tempo puro.

## Requirement Traceability

| ID | Story | Status |
| --- | --- | --- |
| DIV-01 | P1: Deus como portador — exige `PowerDescriptor` válido + pool derivado de fiéis | Pending |
| DIV-02 | P1: Deus como portador — pool recalculado a cada reavaliação, sem campo obsoleto | Pending |
| DIV-03 | P1: Deus como portador — invocação usa pipeline padrão da Fase 16, sem bypass | Pending |
| DIV-04 | P1: Deus como portador — determinismo por seed | Pending |
| DIV-10 | P1: Realimentação — manifestação com testemunha vira relato na Fase 10 | Pending |
| DIV-11 | P1: Realimentação — retransmissão realimenta o pool | Pending |
| DIV-12 | P1: Realimentação — sem retransmissão, decaimento monotônico até 0 | Pending |
| DIV-13 | P1: Realimentação — pool 0 aciona coleta (arquivo frio) | Pending |
| DIV-14 | P1: Realimentação — perseguição bate o decaimento natural, 10/10 seeds | Pending |
| DIV-20 | P1: Natureza — doutrina corrente derivada dos operadores de distorção do ADR-0007 | Pending |
| DIV-21 | P1: Natureza — divergência de doutrina troca o rótulo de natureza | Pending |
| DIV-22 | P1: Natureza — braço tratado diverge, braço de controle zero divergências | Pending |
| DIV-23 | P1: Natureza — determinismo por seed | Pending |
| DIV-30 | P1: Culto — templo/dízimo reusam edifício/transação da Fase 5/8 | Pending |
| DIV-31 | P1: Culto — sacerdote é profissão comum | Pending |
| DIV-32 | P1: Culto — doutrina transmitida via mecanismo de conhecimento existente | Pending |
| DIV-33 | P1: Culto — cisma cria `Deity` novo com pool próprio | Pending |
| DIV-34 | P1: Culto — original mantém fiéis remanescentes pós-cisma | Pending |
| DIV-40 | P1: Panteão — soma de devoção por NPC = 1 (deuses + sem-fé) | Pending |
| DIV-41 | P1: Panteão — conversão realoca devoção, nunca cria do zero | Pending |
| DIV-42 | P1: Panteão — crescimento de um deus correlaciona com perda de share de outro | Pending |
| DIV-50 | P1: Intervenção — usa pipeline `Guaranteed`/`ResolutionCheck` da Fase 16 | Pending |
| DIV-51 | P1: Intervenção — custo debitado independente de testemunha | Pending |
| DIV-52 | P1: Intervenção — falha gera consequência declarada, nunca silenciosa | Pending |
| DIV-53 | P1: Intervenção — sem testemunha, sem relato, mas custo/efeito ocorrem | Pending |
| DIV-60 | P1: Worshipped≠FaithPowered — atribuição social não altera `FaithPowered` | Pending |
| DIV-61 | P1: Worshipped≠FaithPowered — sem `FaithPowered`, nenhum pool calculado | Pending |
| DIV-62 | P1: Worshipped≠FaithPowered — ligação explícita ativa pool a partir do tick | Pending |
| DIV-63 | P1: Worshipped≠FaithPowered — `test-worship-without-faith-power` sem vazamento | Pending |
| DIV-70 | P1: Verdade — nenhum handler de crença expõe campo de realidade (enumeração) | Pending |
| DIV-71 | P1: Verdade — par de mutação: desligar checagem derruba o critério | Pending |
| DIV-72 | P1: Verdade — deus esvaziado e mito em ascensão respondem byte-idêntico | Pending |
| DIV-73 | P1: Verdade — canal de Verdade da Fase 10 resolve corretamente | Pending |

**Coverage**: 31 total. Todas as decisões de Assumptions confirmadas — sem bloqueio pra Design.

## Success Criteria

- [ ] Deus sem fiéis decai monotonicamente até 0 e é coletado — provado em 10 anos simulados.
- [ ] Perseguição bate o esquecimento natural em 10/10 seeds, com margem maior que o spread entre
      seeds do baseline.
- [ ] Distorção muda a natureza do deus com controle pareado (braço sem operadores = zero
      divergências).
- [ ] Crença nunca revela realidade: enumeração de handlers + par de mutação provando que o teste
      detecta vazamento de verdade.
- [ ] Deus esvaziado e mito em ascensão convergem para respostas byte-idênticas em toda superfície
      de jogo, no ponto de pool/manifestação equivalentes.
- [ ] `test-worship-without-faith-power` prova que atribuição social sozinha não gera ganho
      mecânico.
- [ ] Desligar a economia de crença muda o hash canônico em 10 anos (crença entrou na conta).
- [ ] `dotnet test` completo sem regressão nas suítes `Extraordinary*`/`History*`/`Society*`.
