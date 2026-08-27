# Fase 23 — Intriga — Specification

## Problem Statement

Intriga não é subsistema novo — é a camada de crença da Fase 10 e a memória social da Fase 7
usadas contra as pessoas. Hoje não existe segredo, chantagem, traição, humor, rancor de longo
prazo, fofoca, reputação por comunidade, facção/conspiração, persona de portador de potência, nem
publicação como ação de agente. Esta fase fecha tudo isso como subproduto do que já existe —
ninguém escreve enredo, o mundo produz rixa/escândalo/conspiração emergentemente.

## Goals

- [ ] Segredo é fato do esqueleto (Fase 10) com propagação de crença restrita (dono, cúmplices,
      conjunto de quem sabe, risco de vazamento por salto de transmissão) — nunca tabela paralela
      de segredos.
- [ ] Toda ação hostil exige motivo E oportunidade como pré-condição — faltando um, a ação nem
      entra no conjunto candidato da utility.
- [ ] Chantagem é modificador de negociação a partir de segredo alheio, exigindo que o
      chantagista acredite no segredo (consulta de crença, nunca de verdade).
- [ ] Traição é agir contra vínculo com confiança acima do limiar do cenário, por ganho
      mensurável — efeito é colapso do eixo de confiança (Fase 7) + memória episódica de alta
      importância em quem viu, nunca um flag `traidor`.
- [ ] Pilha de humor: modificadores transitórios (fonte/magnitude/decaimento declarados),
      somados numa pilha inspecionável que entra como peso na utility (Fase 4) — humor é sempre
      derivado, nunca campo escrito à mão.
- [ ] Rancor de longo prazo: memória episódica negativa que sobrevive à compactação virando
      crença sobre pessoa/linhagem, decai, prescreve no prazo do cenário, reacende só com evento
      novo do mesmo alvo. Rixa de linhagem (multi-geracional) tem prazo próprio, mais longo, à
      parte do prazo de rancor individual.
- [ ] Briga/violência resolve pelo primitivo único (ADR-0011, perfil `Dramático`), com sucesso
      parcial de primeira classe ("você venceu, mas alguém viu" alimenta a próxima intriga).
- [ ] Fofoca é transmissão enviesada — os operadores de distorção da Fase 10, probabilidade
      modulada pelos eixos de relação de quem conta com o alvo e com o ouvinte (inimigo infla,
      aliado omite).
- [ ] Reputação por comunidade é agregado das crenças daquela comunidade sobre um NPC, distinto
      da verdade e do que cada indivíduo acredita — estado mantido/cacheado, invalidado por
      evento (nunca recalculado do zero por tick).
- [ ] Facção/conspiração: organização com objetivo oculto (segredo de múltiplos donos),
      recrutamento por afinidade e rancor comum, exposição pública como evento com consequência.
- [ ] Persona do portador de potência (Fase 16): identidade é verdade (`PersonaDescriptor`),
      associação persona↔dono é crença por observador (`IdentityAttributionBelief`) — nunca
      booleano global; exposição é gradual (testemunha→grupo→rumor→comunidade).
- [ ] Publicar é ação de agente (jornalista forma crença, avalia risco/interesse/ganho, decide),
      produz `PublicationEvent` que a Fase 12 só transforma em texto depois — reação ao
      extraordinário reusa reputação-por-comunidade, nunca `if target.HasPower`.
- [ ] Intriga (chantagem/traição/briga com testemunha) exige NPCs materializados — região
      agregada não gera intriga nomeada.

## Out of Scope

| Item | Razão |
| --- | --- |
| Prosa de escândalo e crônica | Fase 12 (Narrativa) — aqui só o `PublicationEvent`/estado, texto é de lá. |
| Guerra entre estados | Fase 10/`society.md`. |
| Segredo de culto | Fase 17 — aqui só o mecanismo genérico de segredo, que a Fase 17 pode consumir. |
| Orientação/estado de divulgação | Fase 22 — aqui só consumidos (divulgação alimenta risco de exposição). |
| Combate tático | Fora do projeto — briga resolve numa rolagem única do ADR-0011, nunca sistema tático. |
| Ferimento localizado e recuperação | Fase própria de saúde/corpo, fora desta — aqui só o resultado da rolagem de briga. |
| Testemunha probabilística em região agregada | Decisão explícita do usuário — intriga só existe em região materializada. |

## Assumptions & Decisions (confirmadas com o usuário — 2026-08-25)

| Decisão | Escolha confirmada | Racional |
| --- | --- | --- |
| Segredo + cânone limitado | **Vira fato irrevelável** — segredo despejado do cânone continua existindo no esqueleto de fatos (nunca apagado, ADR-0007), mas ninguém novo pode aprendê-lo; quem já sabia continua sabendo, chantagem em andamento sobrevive | Usuário confirmou explicitamente (Recommended) — consistente com "fato nunca é apagado" |
| Reputação por comunidade | **Estado mantido, invalidado por evento** — cache incremental, mesmo espírito de decaimento preguiçoso da Fase 9 | Usuário confirmou explicitamente (Recommended) — preserva o teto de custo por NPC-tick |
| Ausência de testemunha + LOD | **Só em região materializada** — intriga nomeada exige NPCs detalhados, região agregada não gera | Usuário confirmou explicitamente (Recommended) — mesma disciplina já usada pra ação social específica em outras fases |
| Rancor + linhagem | **Linhagem tem prazo próprio, mais longo** — rancor individual prescreve no prazo normal; rixa de linhagem é entidade separada agregando rancor de múltiplos indivíduos da mesma família, com prazo à parte | Usuário confirmou explicitamente (Recommended) — sustenta rixa multi-geracional sem rancor individual imortal |

**Todas as 4 decisões estão confirmadas** — nenhuma pendência bloqueando Design.

## User Stories

### P1: Segredo como fato com propagação restrita

**User Story**: Como quem desenha intriga, quero que segredo seja um fato comum do esqueleto da
Fase 10 com atributo de propagação restrita (dono, cúmplices, quem sabe, risco de vazamento) —
nunca uma tabela paralela de "segredos" como entidade nova.

**Why P1**: É a fundação — sem reuso direto do fato, todo o resto (chantagem, exposição,
publicação) duplicaria a camada de crença.

**Acceptance Criteria**:

1. WHEN um fato é declarado como segredo THEN ele SHALL ganhar atributos de propagação
   restrita (dono, lista de cúmplices, conjunto de quem sabe, risco de vazamento por salto de
   transmissão) sobre o MESMO `Fact` já modelado pela Fase 10 — nunca uma entidade `Secret`
   paralela desconectada do esqueleto de fatos.
2. WHEN o segredo é despejado do cânone limitado (Fase 10) THEN ele SHALL virar fato irrevelável
   — continua existindo (nunca apagado, ADR-0007), mas nenhum NPC novo pode aprendê-lo; quem já
   sabia continua sabendo, e chantagem em andamento sobre ele continua válida.
3. WHEN todo NPC que conhece um segredo é auditado a cada tick (10 anos, gate) THEN ele SHALL
   ter uma cadeia de transmissão rastreável até o dono — NPC sabendo sem cadeia reprova.
4. WHEN toda superfície de acesso à crença é enumerada por reflexão THEN o teste SHALL reprovar
   se algum caminho resolver pro fato direto (verdade) OU ficar sem cobertura — par de mutação:
   desligar a checagem por flag tem que fazer este critério falhar.

**Independent Test**: segredo criado com dono + 2 cúmplices; auditoria de 10 anos confirma que
todo NPC que sabe tem cadeia até o dono; despejo do cânone não apaga o fato, só impede
aprendizado novo.

---

### P1: Motivo e oportunidade como pré-condição de ação hostil

**User Story**: Como quem quer intriga plausível, quero que toda ação hostil (chantagem,
traição, briga) exija motivo (necessidade/rancor/ganho/ordem de facção) E oportunidade
(proximidade/acesso/ausência de testemunha) — faltando um, a ação nem entra no conjunto candidato
da utility.

**Why P1**: Sem essa dupla pré-condição, ações hostis ocorreriam sem causa rastreável ou sem
plausibilidade situacional.

**Acceptance Criteria**:

1. WHEN o conjunto de ações candidatas de um NPC é montado THEN toda ação classificada como
   hostil SHALL exigir motivo E oportunidade presentes simultaneamente — ausência de qualquer um
   dos dois remove a ação do conjunto ANTES da pontuação (mesmo padrão de filtro-antes-da-utility
   já usado na Fase 21).
2. WHEN oportunidade inclui "ausência de testemunha" THEN essa checagem SHALL só se aplicar em
   região materializada (Fase 8) — região agregada nunca produz ação hostil nomeada por não ter
   "quem vê quem" resolvido por NPC.

**Independent Test**: NPC com motivo mas sem oportunidade (testemunha presente, sem acesso) não
tem ação hostil candidata; mesmo NPC com oportunidade aberta a considera.

---

### P1: Chantagem exige crença, não verdade

**User Story**: Como quem quer chantagem coerente com a separação crença/verdade da Fase 10,
quero que o chantagista precise acreditar no segredo que usa — consulta de crença, nunca de
verdade — pra chantagem funcionar.

**Why P1**: Preserva a garantia central da Fase 10 (crença ≠ verdade) aplicada a um novo
consumidor.

**Acceptance Criteria**:

1. WHEN uma chantagem é executada THEN o motor SHALL consultar as crenças do chantagista
   naquele tick — nunca a verdade do fato (canal de Fase 10 separado, `HistoryTruthQuery`-like).
2. WHEN o segredo não está nas crenças do chantagista naquele tick THEN a chantagem SHALL ser
   recusada — auditado a cada tick em 10 anos, toda chantagem executada tem o segredo nas
   crenças do chantagista no mesmo tick.

**Independent Test**: chantagista sem o segredo em suas crenças não consegue executar a ação;
auditoria de 10 anos confirma 100% de chantagens com crença presente no tick de execução.

---

### P1: Traição colapsa confiança, nunca flag

**User Story**: Como quem quer traição com consequência mecânica real, quero que agir contra um
vínculo de confiança acima do limiar do cenário, por ganho mensurável, colapse o eixo de
confiança da Fase 7 e grave memória episódica de alta importância em quem viu — nunca um campo
`traidor` que rotula o NPC pra sempre.

**Why P1**: Preserva "sem flag global" — o mesmo espírito já usado noutras fases pra recusar
score/rótulo fixo.

**Acceptance Criteria**:

1. WHEN um NPC age contra um vínculo cuja confiança está acima do limiar do cenário, por ganho
   mensurável THEN o eixo de confiança daquele vínculo (Fase 7) SHALL colapsar — magnitude
   proporcional à traição, nunca um evento binário sem grau.
2. WHEN a traição é testemunhada THEN quem viu SHALL registrar memória episódica de alta
   importância sobre o evento — nunca só o alvo diretamente afetado.
3. WHEN o esquema de `Npc` é inspecionado THEN nenhum campo `IsTraitor`/`traidor` SHALL existir
   — traição é sempre lida do colapso de confiança + memória episódica, nunca um rótulo
   armazenado.
4. WHEN um par base/tratamento na mesma seed varia densidade de segredos (mais segredos no
   tratamento) THEN a taxa de traição SHALL ser maior no braço tratado em 18/20 seeds.

**Independent Test**: par base/tratamento (densidade de segredo) — mais traições no tratado,
18/20 seeds; inspeção de esquema não encontra campo `traidor`.

---

### P1: Pilha de humor como peso derivado na utility

**User Story**: Como quem quer NPCs com estado emocional real, quero uma pilha de modificadores
transitórios (fonte/magnitude/decaimento) que entra como peso na utility (Fase 4) — humor é
sempre derivado da pilha, nunca um campo escrito à mão.

**Why P1**: Referência de mecânica explícita do domínio (pilha de pensamentos do RimWorld) —
sem pilha inspecionável, humor vira caixa preta.

**Acceptance Criteria**:

1. WHEN um evento gera um modificador de humor THEN ele SHALL ser empilhado com fonte,
   magnitude e taxa de decaimento declaradas — nunca escrito direto num campo agregado.
2. WHEN o humor de um NPC é consultado THEN o valor SHALL ser sempre derivado da soma corrente
   da pilha (inspecionável item a item) — nunca um campo `Mood` armazenado independentemente.
3. WHEN o humor entra na utility (Fase 4) como peso THEN desligar a pilha por flag SHALL mudar
   o hash canônico em 10 anos E reduzir a diversidade de ações distintas escolhidas, par na
   mesma seed, em 18/20 seeds.

**Independent Test**: pilha com 3 modificadores de fontes diferentes soma corretamente e decai
nas taxas declaradas; desligar a pilha muda o hash e reduz diversidade de ação, 18/20 seeds.

---

### P1: Rancor de longo prazo — individual prescreve, linhagem tem prazo próprio

**User Story**: Como quem quer rixa que sobrevive gerações sem rancor individual imortal, quero
que memória episódica negativa vire crença sobre pessoa/linhagem, decaindo e prescrevendo no
prazo do cenário para o indivíduo, mas com a rixa de linhagem carregando um prazo próprio, mais
longo, separado.

**Why P1**: Resolve a tensão explícita do roadmap entre "rancor prescreve" e "rixa
multi-geracional persiste".

**Acceptance Criteria**:

1. WHEN o rancor individual de um NPC contra um alvo não recebe evento novo THEN
   `rancor(t+1) ≤ rancor(t)` SHALL valer a cada tick, chegando a zero no prazo do cenário
   (prescrição).
2. WHEN um evento novo do mesmo alvo ocorre THEN o rancor individual SHALL reacender — nunca
   permanece prescrito indefinidamente após reincidência.
3. WHEN rancores individuais de múltiplos membros da mesma linhagem contra a mesma linhagem-alvo
   se acumulam THEN uma entidade de rixa de linhagem SHALL agregar esse rancor com um prazo de
   prescrição PRÓPRIO, declarado separadamente no cenário e mais longo que o prazo individual —
   rixa de linhagem sobrevive à prescrição de qualquer rancor individual específico.
4. WHEN a origem da rixa de linhagem não é mais lembrada por ninguém vivo (mesma distorção da
   Fase 10 que já degrada causa) THEN a rixa SHALL continuar existindo como crença sobre a
   linhagem — "a rixa cuja origem ninguém lembra direito" é resultado esperado, não bug.

**Independent Test**: rancor individual sem evento novo prescreve no prazo do cenário; rixa de
linhagem com o mesmo padrão de eventos, mas prazo de linhagem mais longo, ainda ativa quando o
rancor individual equivalente já teria prescrito.

---

### P1: Briga pelo primitivo único, sucesso parcial de primeira classe

**User Story**: Como quem quer violência sem sistema tático, quero que toda briga resolva numa
única rolagem (ADR-0011, perfil `Dramático`), com "você venceu, mas alguém viu" como resultado
de primeira classe que alimenta a próxima intriga.

**Why P1**: Preserva "combate tático fora do projeto" — briga é evento único, não sequência de
ações.

**Acceptance Criteria**:

1. WHEN uma briga é resolvida THEN o motor SHALL usar exatamente uma rolagem
   (`Resolver.Resolve`, perfil `Dramático`) — nunca uma sequência de turnos/ações táticas.
2. WHEN o resultado é `PartialSuccess` THEN o desfecho declarado (ex.: "venceu, mas foi visto")
   SHALL ser tratado como resultado de primeira classe, gerando consequência real (ex.: memória
   episódica em testemunha, entrada pro sistema de reputação) — nunca um caso degenerado
   ignorado.

**Independent Test**: N brigas com seed controlada produzem distribuição de resultados
compatível com o perfil `Dramático`; `PartialSuccess` sempre gera consequência rastreável
(testemunha/reputação).

---

### P1: Fofoca como transmissão enviesada por relação

**User Story**: Como quem quer fofoca emergente, quero que ela reuse os operadores de distorção
da Fase 10, com probabilidade modulada pelos eixos de relação de quem conta com o alvo e com o
ouvinte — inimigo infla, aliado omite.

**Why P1**: Reuso direto — sem ele, fofoca duplicaria a máquina de distorção.

**Acceptance Criteria**:

1. WHEN um relato é retransmitido como fofoca THEN o motor SHALL aplicar os MESMOS operadores
   de distorção da Fase 10 — nunca um pipeline de distorção paralelo.
2. WHEN a relação de quem conta com o alvo é hostil THEN a probabilidade de operadores que
   inflam (ex.: `MagnitudeInflation`) SHALL ser maior do que quando a relação é neutra.
3. WHEN a relação de quem conta com o alvo é próxima/aliada THEN a probabilidade de operadores
   que omitem (ex.: `ConvenientOmission`) SHALL ser maior do que quando a relação é neutra.

**Independent Test**: mesmo fato retransmitido por um contador inimigo do alvo vs. um contador
aliado — distribuição de operadores aplicados diverge na direção prevista (inflação vs. omissão).

---

### P1: Reputação por comunidade, distinta da verdade

**User Story**: Como quem quer retratos sociais divergentes, quero que reputação seja um
agregado cacheado (invalidado por evento) das crenças de uma comunidade sobre um NPC — distinto
da verdade e do que cada indivíduo acredita, podendo divergir entre comunidades.

**Why P1**: É o critério "reputação não é verdade" — sem cache/invalidação por evento, o custo
por tick explodiria (mesma disciplina de decaimento preguiçoso da Fase 9).

**Acceptance Criteria**:

1. WHEN a reputação de um NPC numa comunidade é consultada THEN o valor SHALL vir de um estado
   mantido (cache), recalculado SÓ quando uma crença relevante daquela comunidade muda — nunca
   recalculado do zero a cada tick.
2. WHEN duas comunidades diferentes observam o mesmo NPC THEN SHALL existir pelo menos 1 caso
   onde a reputação diverge entre elas E ambas divergem do fato (verdade) — cada comunidade
   agindo coerente com sua própria versão.

**Independent Test**: NPC com histórico de crenças distintas em 2 comunidades — reputação
divergente entre elas, ambas diferentes da verdade consultada pelo canal de Fase 10; mudar uma
crença invalida só o cache daquela comunidade, sem recálculo global.

---

### P1: Facção e conspiração como segredo de múltiplos donos

**User Story**: Como quem quer organizações ocultas emergentes, quero que facção seja
organização com objetivo oculto (segredo compartilhado por múltiplos donos), recrutamento por
afinidade e rancor comum, e exposição pública como evento com consequência real.

**Why P1**: Reusa segredo (multi-dono) em vez de inventar um segundo tipo de sigilo.

**Acceptance Criteria**:

1. WHEN uma facção é criada com objetivo oculto THEN esse objetivo SHALL ser modelado como o
   MESMO tipo de segredo já desenhado nesta fase, com múltiplos donos (membros fundadores) —
   nunca um tipo de sigilo paralelo.
2. WHEN um NPC é avaliado pra recrutamento THEN afinidade e rancor comum (compartilhado com
   membros existentes) SHALL influenciar a decisão — mesma disciplina de motivo já usada pra
   ação hostil.
3. WHEN a existência/objetivo da facção é exposto publicamente THEN o evento SHALL ter
   consequência real modelada (mudança de reputação, reação cultural, possível dissolução) —
   nunca uma revelação sem efeito mecânico.

**Independent Test**: facção com objetivo oculto compartilhado por 3 fundadores; recrutamento
correlaciona com afinidade/rancor comum; exposição pública altera reputação mensuravelmente.

---

### P1: Persona do portador de potência — associação é crença, nunca booleano global

**User Story**: Como quem desenha identidade secreta de portador de potência (Fase 16), quero
que a persona seja verdade (`PersonaDescriptor`, dono único) mas a associação persona↔dono seja
crença por observador — exposição gradual (testemunha→grupo→rumor→comunidade), nunca um
booleano que liga de uma vez.

**Why P1**: Preserva a mesma separação crença/verdade central da Fase 10, aplicada a identidade
secreta.

**Acceptance Criteria**:

1. WHEN uma persona é declarada THEN `PersonaDescriptor` (dono = `NpcId` único) SHALL ser
   verdade — consultável só pelo canal de Verdade, nunca por handler de jogo.
2. WHEN um observador forma uma crença sobre quem é o dono de uma persona THEN ela SHALL ser
   modelada como `IdentityAttributionBelief` (observador, candidato, confiança, evidências) —
   nunca um campo `bool SecretIdentityKnown` global.
3. WHEN uma atribuição é falsa (observador crê no candidato errado) THEN ela SHALL funcionar
   mecanicamente igual a uma atribuição verdadeira (mesmo pipeline de crença) — o motor nunca
   corrige silenciosamente uma crença errada.
4. WHEN alguém vê o efeito de uma potência THEN isso NÃO SHALL equivaler a ver o dono — a
   associação nasce só de evidência observável (rosto, voz, assinatura, testemunho).
5. WHEN a exposição de uma persona evolui THEN ela SHALL passar pelos estágios graduais
   (testemunha isolada → grupo → rumor → comunidade) — nunca um salto direto de "ninguém sabe"
   pra "todos sabem".

**Independent Test**: persona com dono real — observador que só viu o efeito não forma crença
de identidade; observador com evidência direta forma `IdentityAttributionBelief` correta ou
incorreta (ambas funcionam igual mecanicamente); exposição avança pelos 4 estágios
gradualmente.

---

### P1: Publicar é ação de agente, produz evento pra Fase 12 consumir

**User Story**: Como quem quer jornalismo emergente, quero que publicar seja decisão de um
agente (jornalista) que forma crença e avalia risco/interesse/ganho — produzindo
`PublicationEvent` que a Fase 12 só transforma em texto depois, nunca uma renderização direta.

**Why P1**: Preserva a separação "motor decide o quê, Fase 12 decide como contar" já usada em
outras fases.

**Acceptance Criteria**:

1. WHEN um jornalista avalia uma informação THEN ele SHALL decidir entre
   ignorar/investigar/publicar/suprimir com base em crença própria + risco/interesse/ganho
   calculados — nunca uma regra fixa "toda informação vira notícia".
2. WHEN a decisão é publicar THEN o motor SHALL produzir um `PublicationEvent` (dado
   estruturado) — nenhum texto é gerado por este sistema, texto é exclusivamente Fase 12.
3. WHEN um portador de potência ganha um apelido de imprensa THEN esse rótulo SHALL ser
   negociado (o portador pode aceitar ou rejeitar) — nunca um nome imposto automaticamente sem
   reação do portador.
4. WHEN a reação pública ao extraordinário (medo/culto/fascínio) é calculada THEN ela SHALL
   reusar o mesmo mecanismo de reputação-por-comunidade já desenhado nesta fase, aplicado a
   portadores — nunca um caminho `if target.HasPower` separado.

**Independent Test**: jornalista com informação de alto risco/baixo ganho decide suprimir;
mesmo jornalista com informação de alto ganho decide publicar, produzindo `PublicationEvent`;
reação pública ao portador é literalmente o mesmo cálculo de reputação-por-comunidade.

## Edge Cases

- WHEN um segredo tem mais de um dono (facção) e um dos donos morre THEN o segredo SHALL
  continuar existindo com os donos remanescentes — nunca desaparece por morte de um dono
  parcial.
- WHEN uma ação hostil teria motivo e oportunidade, mas o alvo está em região agregada THEN a
  ação SHALL ser recusada (sem intriga nomeada fora de materialização) — nunca forçar
  materialização como efeito colateral de uma checagem de oportunidade.
- WHEN a pilha de humor de um NPC fica vazia (todos os modificadores decaíram) THEN o humor
  SHALL ser o baseline neutro declarado no cenário — nunca erro por pilha vazia.
- WHEN rancor de linhagem e rancor individual do mesmo par de pessoas coexistem THEN cada um
  SHALL decair/prescrever de forma independente pelo seu próprio prazo — nunca um prazo
  "vencendo" o outro.

## Requirement Traceability

| ID | Story | Status |
| --- | --- | --- |
| INT-01 | P1: Segredo — atributo de propagação sobre o Fact existente | Pending |
| INT-02 | P1: Segredo — despejo do cânone vira fato irrevelável | Pending |
| INT-03 | P1: Segredo — auditoria de cadeia até o dono, 10 anos | Pending |
| INT-04 | P1: Segredo — enumeração por reflexão + par de mutação | Pending |
| INT-10 | P1: Motivo/oportunidade — filtro antes da utility | Pending |
| INT-11 | P1: Motivo/oportunidade — testemunha só em região materializada | Pending |
| INT-20 | P1: Chantagem — consulta crença do chantagista, nunca verdade | Pending |
| INT-21 | P1: Chantagem — auditoria 10 anos, crença presente no tick | Pending |
| INT-30 | P1: Traição — colapso de confiança proporcional | Pending |
| INT-31 | P1: Traição — memória episódica em testemunha | Pending |
| INT-32 | P1: Traição — nenhum campo traidor no esquema | Pending |
| INT-33 | P1: Traição — par densidade de segredo, 18/20 seeds | Pending |
| INT-40 | P1: Humor — modificador empilhado com fonte/magnitude/decaimento | Pending |
| INT-41 | P1: Humor — valor sempre derivado da pilha | Pending |
| INT-42 | P1: Humor — desligar pilha muda hash e reduz diversidade, 18/20 seeds | Pending |
| INT-50 | P1: Rancor — individual prescreve, rancor(t+1)≤rancor(t) | Pending |
| INT-51 | P1: Rancor — reacende com evento novo | Pending |
| INT-52 | P1: Rancor — linhagem tem prazo próprio mais longo | Pending |
| INT-53 | P1: Rancor — rixa sobrevive à origem esquecida | Pending |
| INT-60 | P1: Briga — uma rolagem única, perfil Dramático | Pending |
| INT-61 | P1: Briga — PartialSuccess gera consequência real | Pending |
| INT-70 | P1: Fofoca — mesmos operadores de distorção da Fase 10 | Pending |
| INT-71 | P1: Fofoca — relação hostil aumenta inflação | Pending |
| INT-72 | P1: Fofoca — relação aliada aumenta omissão | Pending |
| INT-80 | P1: Reputação — estado mantido, invalidado por evento | Pending |
| INT-81 | P1: Reputação — diverge entre comunidades e da verdade | Pending |
| INT-90 | P1: Facção — objetivo oculto é segredo multi-dono | Pending |
| INT-91 | P1: Facção — recrutamento por afinidade/rancor comum | Pending |
| INT-92 | P1: Facção — exposição pública tem consequência real | Pending |
| INT-A0 | P1: Persona — PersonaDescriptor é verdade | Pending |
| INT-A1 | P1: Persona — associação é IdentityAttributionBelief por observador | Pending |
| INT-A2 | P1: Persona — atribuição falsa funciona igual à verdadeira | Pending |
| INT-A3 | P1: Persona — ver efeito ≠ ver dono | Pending |
| INT-A4 | P1: Persona — exposição gradual em 4 estágios | Pending |
| INT-B0 | P1: Publicação — decisão de agente, nunca regra fixa | Pending |
| INT-B1 | P1: Publicação — produz PublicationEvent, texto é Fase 12 | Pending |
| INT-B2 | P1: Publicação — apelido é negociado | Pending |
| INT-B3 | P1: Publicação — reação ao extraordinário reusa reputação-por-comunidade | Pending |

**Coverage**: 37 total. Todas as decisões de Assumptions confirmadas — sem bloqueio pra Design.

## Success Criteria

- [ ] Ninguém sabe sem caminho: enumeração por reflexão + assert de cadeia em 10 anos + par de
      mutação.
- [ ] Segredo causa traição: taxa maior no braço com mais segredos, 18/20 seeds.
- [ ] Chantagista acredita no que usa: 100% das chantagens auditadas em 10 anos.
- [ ] Rancor decai, prescreve, só reacende por evento — rancor(t+1)≤rancor(t) sem evento novo.
- [ ] Humor entrou na conta: hash muda + diversidade de ação reduz ao desligar, 18/20 seeds.
- [ ] Reputação não é verdade: ≥1 NPC com reputação divergente entre 2 comunidades e de ambas
      com o fato.
- [ ] `dotnet test` completo sem regressão nas suítes `History*`/`Population*`/`Society*`/
      `Extraordinary*`.
