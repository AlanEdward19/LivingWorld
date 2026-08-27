# Fase 22 — Imperfeição e diversidade — Specification

## Problem Statement

O mundo hoje não modela defeito congênito, doença, deficiência, nem a possibilidade de moralidade
emergente contra a expectativa do ambiente. Esta fase introduz condição (dado de cenário, nunca
`enum`), doença com eixos de transmissão/letalidade/imunidade, deficiência com consequência
funcional (reusando o pré-requisito de marco da Fase 21), moralidade emergente (nunca campo), e
o termo de sorte do modelo `w_gene + w_env + w_sorte` como canal declarado e auditável — forte o
bastante pra produzir o improvável, fraco o bastante pra não apagar a causalidade que a Fase 7
mediu.

## Goals

- [ ] Condição é dado de cenário com origem declarada (genética/ambiental/acaso) e curso
      (congênita/adquirida/crônica/progressiva/remissiva) — nunca `enum` de código, mesma regra
      já imposta a profissão e recurso na Fase 3.
- [ ] Doença tem eixos: vetor de transmissão nomeado (contato/água/ar/ferida/vertical),
      letalidade, incubação, imunidade (nenhuma/temporária/permanente). Curso individual sai da
      rolagem (ADR-0011) com resistência do NPC como modificador — mesma doença, desfechos
      diferentes.
- [ ] Deficiência tem consequência funcional real, reusando o pré-requisito de marco da Fase 21:
      a condição rebaixa o teto do eixo afetado, ações que o exigem saem do alcance.
- [ ] A cultura decide a reação (acolher/esconder/excluir/descartar), não a condição — mesma
      regra da Fase 16, a condição nunca carrega a reação embutida.
- [ ] Moralidade é sempre emergente (empatia/altruísmo/impulsividade da Fase 4, criação da Fase
      21, circunstância) — nunca um campo/escalar de alinhamento no esquema do NPC. Corrupção
      por artefato/entidade (Fase 16) modifica sistemas concretos (agressividade, paranoia,
      dependência, percepção), nunca um `Corruption` que implica `IsEvil`.
- [ ] Canal de sorte é explícito: peso declarado no cenário (default documentado, calibrado
      contra os baselines da Fase 7), stream próprio no RNG (ADR-0005), cauda pelo perfil `Raro`
      (ADR-0011) — nomeado e auditável, nunca ruído espalhado.
- [ ] Orientação sexual é atributo independente de cultura, mais um estado de divulgação
      (assumido/oculto/negado) que evolui com tolerância local, vínculo de quem sabe, e eventos
      de exposição. "Negado" é ação de fingimento deliberado (o NPC sabe, mas finge pros
      outros) — desmentível por prova, deixando o gancho pronto pra Fase 23.
- [ ] Divulgação alimenta estresse e risco, e deixa o gancho de chantagem pronto pra Fase 23 —
      aqui só o estado, a mecânica de segredo é de lá.
- [ ] Compatibilidade de cortejo (Fase 7) revista: valores, temperamento, orientação e estado de
      divulgação entram junto da atração, com motivo nomeado na rejeição — mesmo mecanismo do
      `Incesto`.

## Out of Scope

| Item | Razão |
| --- | --- |
| Segredo, chantagem, fofoca, traição como mecânica | Fase 23 — aqui só o estado de divulgação (assumido/oculto/negado). |
| Medicina como profissão, hospital e cura | Fases 6, 5 e 8. |
| Epidemia como relato histórico e prosa | Fases 10 e 12. |
| Poder que cura ou amaldiçoa | Fase 16. |
| Prevalência de condições (quão comum é cada uma) | Balanceamento, sem gate (ADR-0010) — regra de cenário. |
| Mutação evolutiva de patógeno | Decisão explícita do usuário — reintrodução é evento de cenário, não sistema de epidemiologia evolutiva. |
| Campo de moralidade/alinhamento no esquema do NPC | Proibido por design — "gente ruim" é padrão lido do log, nunca um escalar. |

## Assumptions & Decisions (confirmadas com o usuário — 2026-08-25)

| Decisão | Escolha confirmada | Racional |
| --- | --- | --- |
| Peso de sorte (`w_sorte`) | **Parâmetro de cenário com default documentado, calibrado contra os baselines da Fase 7** — nunca hardcoded, mas o motor sugere um valor de partida validado empiricamente | Usuário confirmou explicitamente (Recommended) — calibração é trabalho de Tasks/Execute, não decisão de arquitetura agora |
| Stream do par base/tratamento (critério "o improvável acontece") | **Livre — cada braço rola sorte independentemente** | Usuário confirmou explicitamente (Recommended) — fixar sorte igual nos 2 braços faria o improvável aparecer nos dois e sumir do teste, como o próprio roadmap já alertava |
| Condição congênita genética vs. predisposição (Fase 6/21) | **Mesmo mecanismo — condição é predisposição extrema**, com limiar declarado no cenário que a torna "condição nomeada" em vez de só "baixa aptidão" | Usuário confirmou explicitamente (Recommended) — zero duplicação, exatamente o que o roadmap pedia ("um dos dois deve sumir") |
| Reintrodução de doença extinta | **Evento de cenário** — autor decide quando/como (contato com região não-imune, viajante), sem sistema de mutação de patógeno | Usuário confirmou explicitamente (Recommended) — evita inventar epidemiologia evolutiva não pedida em fase nenhuma |
| Estado de divulgação "negado" | **Ação de fingimento deliberado** — o NPC sabe sua orientação, mas ativamente esconde/nega pros outros, desmentível por prova/evento de exposição | Usuário confirmou explicitamente (Recommended) — consistente com o gancho de chantagem da Fase 23, que precisa de algo desmentível |

**Todas as 5 decisões estão confirmadas** — nenhuma pendência bloqueando Design.

## User Stories

### P1: Condição como dado de cenário, nunca enum de código

**User Story**: Como quem desenha condições do mundo, quero declarar condição inteiramente como
dado de cenário — origem (genética/ambiental/acaso) e curso (congênita/adquirida/crônica/
progressiva/remissiva) — sem nenhum `enum` de código fixo.

**Why P1**: É a fundação — mesma regra já imposta a profissão e recurso na Fase 3, garante que
novas condições nunca exigem mudança de código.

**Acceptance Criteria**:

1. WHEN uma condição é declarada no cenário THEN ela SHALL especificar origem (genética/
   ambiental/acaso) e curso (congênita/adquirida/crônica/progressiva/remissiva) como dado — nunca
   um valor de `enum` C# fixo.
2. WHEN uma condição de origem genética é aplicada a um NPC THEN ela SHALL ser modelada como
   predisposição extrema (mesmo mecanismo `RateGene` da Fase 6/21) cruzando um limiar declarado
   no cenário que a torna "condição nomeada" — nunca um segundo mecanismo genético paralelo.
3. WHEN o esquema de dados de condição é inspecionado THEN nenhum campo SHALL existir fora do
   modelo de predisposição+limiar (pra origem genética) ou dos eixos de doença (para origem
   ambiental/acaso, ver história seguinte).

**Independent Test**: condição genética declarada com limiar 0.15 no eixo de motricidade fina —
NPC com `RateGene` abaixo do limiar é classificado com a condição; mecanismo é literalmente o
mesmo `RateGene` já usado pra taxa de aquisição de habilidade/marco.

---

### P1: Doença com eixos, curso individual por rolagem

**User Story**: Como quem desenha epidemias, quero que doença tenha vetor de transmissão
nomeado, letalidade, incubação e imunidade como eixos declarados, e que o curso individual saia
de uma rolagem com a resistência do NPC como modificador — mesma doença, desfechos diferentes.

**Why P1**: Sem eixos declarados e rolagem individual, "doença" vira um número de mortalidade
fixo sem textura.

**Acceptance Criteria**:

1. WHEN uma doença é declarada no cenário THEN ela SHALL especificar vetor de transmissão
   (contato/água/ar/ferida/vertical), letalidade, período de incubação, e tipo de imunidade
   pós-infecção (nenhuma/temporária/permanente) — todos como dado de cenário.
2. WHEN um NPC é exposto ao vetor declarado de uma doença dentro do alcance de contágio THEN o
   motor SHALL rolar (ADR-0011) o curso individual com a resistência/vitalidade do NPC como
   modificador — dois NPCs expostos à mesma doença podem ter desfechos diferentes.
3. WHEN todo caso novo de uma doença é auditado THEN ele SHALL ter um caso-fonte identificável
   com contato pelo vetor declarado dentro da janela de incubação — caso sem cadeia rastreável
   reprova.
4. WHEN o conjunto de doenças instanciadas no mundo é comparado ao catálogo do cenário carregado
   THEN ele SHALL ser sempre um subconjunto — nenhum NPC adoece de doença fora do catálogo.
5. WHEN uma doença com imunidade permanente se extingue (ninguém mais suscetível) THEN ela SHALL
   permanecer extinta até um evento de reintrodução explicitamente declarado no cenário — nunca
   um sistema de mutação de patógeno reintroduzindo sozinho.

**Independent Test**: cadeia de contágio auditada em 10 anos simulados (gate)/100 anos
(nightly) — todo caso tem fonte rastreável; conjunto de doenças ativas é sempre subconjunto do
catálogo carregado.

---

### P1: Deficiência com consequência funcional via pré-requisito de marco

**User Story**: Como quem quer deficiência com peso real, quero que ela rebaixe o teto do eixo
de desenvolvimento afetado (Fase 21) — as ações que exigem aquele eixo saem do alcance, nunca
uma etiqueta decorativa.

**Why P1**: Reusa o pré-requisito de marco já desenhado, evitando um segundo sistema de
capacidade.

**Acceptance Criteria**:

1. WHEN uma condição declara consequência funcional sobre um `DevelopmentAxis` (Fase 21) THEN o
   teto (`Ceiling`) daquele eixo SHALL ser reduzido pelo valor declarado — mesmo campo/mecanismo
   de teto já usado pela janela crítica.
2. WHEN o teto de um eixo cai abaixo do limiar exigido por uma ação do catálogo (Fase 21) THEN
   essa ação SHALL sair do conjunto candidato via `MilestoneEligibilityFilter` já existente —
   nenhum segundo filtro de "deficiência" paralelo.
3. WHEN a consequência funcional é removida (condição remissiva, cenário declara cura) THEN o
   teto SHALL ser restaurado conforme a regra declarada — nunca automaticamente sem uma regra
   explícita de recuperação.

**Independent Test**: condição com consequência funcional em motricidade grossa reduz o teto
daquele eixo — ações que exigiam limiar acima do novo teto desaparecem do conjunto candidato do
NPC, verificável pelo mesmo mecanismo de filtro da Fase 21.

---

### P1: Cultura decide a reação, condição não carrega reação embutida

**User Story**: Como quem quer culturas distintas reagindo diferente à mesma condição, quero que
a reação (acolher/esconder/excluir/descartar) venha inteiramente dos valores culturais — a
condição em si nunca carrega uma reação padrão.

**Why P1**: Mesma regra já usada pra potência na Fase 16 — reação é sempre função de quem
observa, nunca do que é observado.

**Acceptance Criteria**:

1. WHEN uma condição é observada por uma cultura THEN a reação (acolher/esconder/excluir/
   descartar) SHALL ser calculada a partir dos valores culturais correntes (igualitarismo/
   tradição/religiosidade/valorização da ciência) — nunca lida de um campo fixo na própria
   condição.
2. WHEN um par base/tratamento na mesma seed opõe duas culturas com valores opostos (ex.:
   igualitária vs. tradicionalista) observando a mesma condição THEN a distribuição de reações
   SHALL divergir na direção prevista pelos valores declarados.
3. WHEN o esquema de `Condition` é inspecionado THEN nenhum campo de "reação padrão" SHALL
   existir nele — reação é sempre calculada no ponto de observação, nunca armazenada na
   condição.

**Independent Test**: mesma condição observada por 2 culturas com valores opostos — distribuição
de reação diverge na direção prevista; inspeção de esquema confirma ausência de campo de reação
na condição.

---

### P1: Moralidade emergente, nunca campo — corrupção modifica sistemas concretos

**User Story**: Como quem quer "gente ruim" sem um botão de maldade, quero que comportamento
moral seja sempre lido do event log (empatia/altruísmo/impulsividade/criação/circunstância), e
que corrupção por artefato/entidade module sistemas concretos (agressividade, paranoia,
dependência, percepção) — nunca um campo que implica `IsEvil`.

**Why P1**: É a garantia central da fase — sem ela, moralidade emergente é só um nome bonito
sobre um escalar disfarçado.

**Acceptance Criteria**:

1. WHEN o esquema de dados do NPC é inspecionado por reflexão THEN nenhum escalar de
   alinhamento/karma/bondade SHALL existir — teste de arquitetura reprova qualquer campo desse
   tipo, e reprova também se algum campo novo do esquema ficar sem classificação explícita
   (moral ou não-moral).
2. WHEN "gente ruim" é avaliado THEN a avaliação SHALL ser um padrão lido do event log
   (histórico de ações) — nunca uma consulta a um campo direto.
3. WHEN corrupção por artefato/entidade (Fase 16) afeta um NPC THEN ela SHALL modificar sistemas
   concretos já existentes (agressividade, paranoia, dependência, percepção) — nunca escrever um
   campo `Corruption`/`IsEvil` que decida comportamento moral por si só.

**Independent Test**: enumeração por reflexão do esquema do NPC não encontra nenhum campo de
alinhamento; NPC corrompido tem métricas concretas (agressividade etc.) alteradas, mas nenhum
campo consultável responde diretamente "esse NPC é mau".

---

### P1: Canal de sorte explícito, auditável e desligável

**User Story**: Como quem quer o improvável sem apagar causalidade, quero que o termo de sorte
seja um canal nomeado com peso declarado no cenário, stream próprio no RNG, e cauda pelo perfil
`Raro` — nunca ruído espalhado por dez sistemas.

**Why P1**: É o mecanismo que torna "criado num lar terrível e saiu bom" possível e testável sem
quebrar a Fase 7.

**Acceptance Criteria**:

1. WHEN o resultado moral/comportamental de um NPC é calculado THEN o modelo SHALL incluir
   exatamente 3 termos nomeados (`w_gene`, `w_env`, `w_sorte`), cada um consultável
   separadamente — nunca um único escalar opaco somando tudo sem rastreabilidade.
2. WHEN o termo de sorte é calculado THEN ele SHALL consumir um stream de RNG próprio nomeado
   (ADR-0005) e usar o perfil `Raro` (ADR-0011, cauda longa) — nunca compartilhar stream com
   outro sistema.
3. WHEN 20 seeds são rodadas com `w_sorte` no peso default documentado THEN SHALL existir pelo
   menos 1 NPC criado em ambiente hostil cujo resultado moral contradiz o previsto pelo
   ambiente — zero ocorrências reprova (canal morto).
4. WHEN a mesma amostra de 20 seeds é medida THEN a taxa de contradição NÃO SHALL exceder a
   faixa registrada em `tests/baselines/` — taxa acima da faixa também reprova (sorte virou
   ruído branco, apagando a causalidade da Fase 7).
5. WHEN `w_sorte` é zerado (braço de controle) THEN a contagem de contradições SHALL ser
   exatamente zero nas mesmas 20 seeds.
6. WHEN `w_sorte` é zerado THEN o hash canônico SHALL mudar em relação ao mundo com `w_sorte`
   no peso default, medido em 10 anos simulados — prova de que a sorte entrou na conta.

**Independent Test**: 20 seeds com `w_sorte` default — pelo menos 1 contradição, dentro da faixa
de baseline; mesmas 20 seeds com `w_sorte=0` — zero contradições; hash muda entre os dois
braços.

---

### P1: Orientação sexual como atributo, divulgação como fingimento desmentível

**User Story**: Como quem quer diversidade real e consequência de risco, quero que orientação
sexual seja um atributo independente de cultura, com estado de divulgação (assumido/oculto/
negado) que evolui com tolerância local — "negado" é fingimento ativo, desmentível por prova.

**Why P1**: Deixa o gancho de chantagem pronto pra Fase 23, sem implementar a mecânica de
segredo aqui.

**Acceptance Criteria**:

1. WHEN um NPC é criado THEN sua orientação sexual SHALL ser atribuída como um valor
   independente da cultura em que nasceu — nenhuma cultura força uma distribuição de orientação
   diferente da declarada no cenário geral.
2. WHEN o estado de divulgação de um NPC muda THEN ele SHALL transitar entre
   assumido/oculto/negado em função da tolerância local, do vínculo de quem sabe, e de eventos
   de exposição — nunca uma mudança sem causa rastreável.
3. WHEN um NPC está no estado "negado" THEN ele SHALL estar ativamente executando fingimento (o
   NPC sabe sua própria orientação — é fato consultável no seu domínio interno) — nunca uma
   crença/incerteza sobre si mesmo.
4. WHEN um evento de exposição ou prova desmente um NPC "negado" THEN o estado SHALL transicionar
   (pra oculto ou assumido, conforme regra do cenário) — deixando o gancho de chantagem/risco
   pronto pra Fase 23 consumir (sem implementar a mecânica de chantagem aqui).
5. WHEN um par base/tratamento na mesma seed opõe tolerância cultural oposta THEN a taxa de
   "assumido" SHALL divergir na direção prevista em 18/20 seeds, E a distribuição de orientação
   em si SHALL ser byte-idêntica nos dois braços — os dois asserts juntos, nenhum sozinho prova
   a separação cultura↔orientação.

**Independent Test**: par base/tratamento tolerância oposta — taxa de "assumido" diverge na
direção prevista (18/20 seeds) e distribuição de orientação é idêntica nos dois braços.

---

### P1: Cortejo respeita orientação e divulgação, com motivo nomeado

**User Story**: Como quem revisita compatibilidade de cortejo (Fase 7), quero que orientação e
estado de divulgação entrem junto de valores/temperamento na atração, com motivo nomeado quando
o cortejo é rejeitado por incompatibilidade — mesmo mecanismo do `Incesto`.

**Why P1**: Sem isso, cortejo ignora orientação e produz pares inconsistentes com o próprio
modelo desta fase.

**Acceptance Criteria**:

1. WHEN a compatibilidade de cortejo entre dois NPCs é avaliada THEN orientação e estado de
   divulgação SHALL entrar no cálculo junto de valores/temperamento já existentes (Fase 7) —
   nunca um filtro adicional desconectado do modelo de atração.
2. WHEN um cortejo é rejeitado por incompatibilidade de orientação THEN o motivo SHALL ser
   nomeado explicitamente (mesmo mecanismo já usado pelo motivo `Incesto`) — nunca uma rejeição
   silenciosa sem causa consultável.
3. WHEN o mundo é auditado por 10 anos simulados (assert a cada tick) THEN zero pares formados
   SHALL violar a orientação declarada dos dois NPCs — nenhuma exceção.
4. WHEN um cenário de controle apresenta dois NPCs compatíveis em tudo MENOS orientação THEN o
   cortejo entre eles SHALL ser rejeitado com o motivo nomeado (prova positiva — sem isso, o
   assert negativo do AC anterior passaria mesmo se ninguém nunca se encontrasse, a mesma
   armadilha já identificada na Fase 7).

**Independent Test**: par de NPCs compatíveis em tudo menos orientação — cortejo rejeitado com
motivo nomeado; auditoria de 10 anos não encontra nenhum par formado violando orientação
declarada.

## Edge Cases

- WHEN uma condição de origem "acaso" é declarada THEN ela SHALL usar o mesmo canal de sorte
  (`w_sorte`, stream próprio, perfil `Raro`) desta fase — nunca um terceiro mecanismo de acaso
  paralelo.
- WHEN um NPC tem múltiplas condições concorrentes afetando o mesmo `DevelopmentAxis` THEN o
  teto resultante SHALL ser resolvido deterministicamente (regra declarada no cenário: mínimo
  entre as reduções, ou soma limitada — decidido em Design) — nunca ordem de aplicação
  ambígua.
- WHEN um NPC "negado" nunca é exposto/desmentido durante toda a vida simulada THEN ele SHALL
  permanecer "negado" indefinidamente — nenhuma transição automática por tempo puro.
- WHEN a doença tem incubação declarada e o NPC morre de outra causa durante a incubação THEN o
  caso SHALL ser registrado como não-desenvolvido (nunca conta como caso letal da doença) —
  causa de morte é a real, não a doença incubando.

## Requirement Traceability

| ID | Story | Status |
| --- | --- | --- |
| IMP-01 | P1: Condição — origem/curso como dado de cenário | Pending |
| IMP-02 | P1: Condição — origem genética é predisposição extrema, mesmo mecanismo | Pending |
| IMP-03 | P1: Condição — esquema sem campo fora do modelo declarado | Pending |
| IMP-10 | P1: Doença — eixos declarados como dado de cenário | Pending |
| IMP-11 | P1: Doença — curso individual por rolagem com resistência como modificador | Pending |
| IMP-12 | P1: Doença — todo caso tem cadeia rastreável | Pending |
| IMP-13 | P1: Doença — instâncias são subconjunto do catálogo carregado | Pending |
| IMP-14 | P1: Doença — reintrodução só por evento de cenário explícito | Pending |
| IMP-20 | P1: Deficiência — reduz teto do eixo via mecanismo da Fase 21 | Pending |
| IMP-21 | P1: Deficiência — ações saem do alcance via filtro existente, sem segundo filtro | Pending |
| IMP-22 | P1: Deficiência — restauração só por regra explícita | Pending |
| IMP-30 | P1: Cultura — reação calculada dos valores culturais, nunca campo fixo | Pending |
| IMP-31 | P1: Cultura — par base/tratamento diverge na direção prevista | Pending |
| IMP-32 | P1: Cultura — esquema de condição sem campo de reação padrão | Pending |
| IMP-40 | P1: Moralidade — nenhum escalar de alinhamento no esquema (reflexão) | Pending |
| IMP-41 | P1: Moralidade — "gente ruim" é padrão lido do log | Pending |
| IMP-42 | P1: Moralidade — corrupção modifica sistemas concretos, nunca Corruption/IsEvil | Pending |
| IMP-50 | P1: Sorte — 3 termos nomeados, consultáveis separadamente | Pending |
| IMP-51 | P1: Sorte — stream próprio, perfil Raro | Pending |
| IMP-52 | P1: Sorte — pelo menos 1 contradição em 20 seeds no default | Pending |
| IMP-53 | P1: Sorte — taxa dentro da faixa de baseline | Pending |
| IMP-54 | P1: Sorte — zero contradições com w_sorte=0 | Pending |
| IMP-55 | P1: Sorte — hash muda entre w_sorte default e zero | Pending |
| IMP-60 | P1: Orientação — atributo independente de cultura | Pending |
| IMP-61 | P1: Divulgação — transição com causa rastreável | Pending |
| IMP-62 | P1: Divulgação — "negado" é fingimento ativo, fato conhecido pelo NPC | Pending |
| IMP-63 | P1: Divulgação — exposição/prova desmente, gancho pronto pra Fase 23 | Pending |
| IMP-64 | P1: Divulgação — par tolerância oposta, taxa diverge E distribuição idêntica | Pending |
| IMP-70 | P1: Cortejo — orientação/divulgação entram no cálculo de compatibilidade | Pending |
| IMP-71 | P1: Cortejo — rejeição por orientação tem motivo nomeado | Pending |
| IMP-72 | P1: Cortejo — zero pares violando orientação em 10 anos | Pending |
| IMP-73 | P1: Cortejo — prova positiva de rejeição com motivo, evita armadilha da Fase 7 | Pending |

**Coverage**: 31 total. Todas as decisões de Assumptions confirmadas — sem bloqueio pra Design.

## Success Criteria

- [ ] O improvável acontece e não é comum: ≥1 contradição em 20 seeds no default, dentro da
      faixa de baseline; zero com `w_sorte=0`.
- [ ] A cultura muda a divulgação, não a orientação: par base/tratamento diverge em "assumido"
      (18/20 seeds) e distribuição de orientação é byte-idêntica.
- [ ] Cortejo respeita orientação e divulgação: zero violações em 10 anos + prova positiva de
      rejeição com motivo nomeado.
- [ ] Contágio conserva a cadeia: todo caso tem fonte rastreável; instâncias são subconjunto do
      catálogo, 10/100 anos.
- [ ] Nenhum campo de moralidade: reflexão sobre o esquema do NPC não encontra escalar de
      alinhamento/karma/bondade, sem campo novo sem classificação.
- [ ] A sorte entrou na conta: zerar `w_sorte` muda o hash canônico em 10 anos.
- [ ] `dotnet test` completo sem regressão nas suítes `Population*`/`Family*`/`Behavior*`/
      `Society*`.
