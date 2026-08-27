# Fase 19 — Cosmos e contato — Specification

## Problem Statement

O mundo hoje para no degrau `global`. Esta fase estende a pilha de LOD (Fase 8/9) com dois degraus
acima — `planeta` e `sistema` — sem nenhuma máquina de simulação nova: corpos, órbitas e
civilizações distantes vivem em estatística até terem consequência real (agrícola, cultural,
econômica) ou até contato promovê-los a detalhe, exatamente como o LOD já faz dentro do planeta.
Alien e conquistador não são um tipo de entidade novo — são cultura em outro degrau tecnológico
(Fase 13) chegando por contato.

## Goals

- [ ] Degraus `sistema` e `planeta` no topo da pilha de LOD já existente (Fase 8/9) — mesma
      regra de conservação na promoção/desmaterialização, nenhuma segunda máquina de simulação.
- [ ] Corpos e órbitas (estrela, luas, planetas, elementos orbitais, recursos orbitais) vivem em
      estatística pura — um tick de sistema produz totais e datas, nunca pessoas.
- [ ] Calendário astronômico (estações, eclipses, cometas, conjunções) é derivado dos elementos
      orbitais por cálculo determinístico — nunca tabelado à mão.
- [ ] A mesma efeméride tem dois usos conforme quem olha: modificador objetivo de produção
      agrícola (Fase 5) e presságio cultural/legitimidade (Fase 10/17) — o fenômeno é sempre
      calculável pelo motor; o que a cultura *sabe prever* é filtro de conhecimento por cima
      (Fase 13), nunca um segundo sistema astronômico.
- [ ] Civilização distante existe como agregado estatístico desde o tick 0 (mesma disciplina de
      "agregado sempre existe, LOD só decide observação" da Fase 8/9) — contato promove a região
      ao detalhe, nunca cria população/cultura do nada.
- [ ] Alien e conquistador reusam 100% de cultura, tecnologia, economia, guerra e diplomacia já
      existentes — nenhuma entidade nova, nenhum handler exclusivo. O degrau tecnológico é
      módulo de conteúdo da Fase 13.
- [ ] Assimetria tecnológica é pressão calculada (valores culturais, coesão política, intenção de
      quem chegou), nunca desfecho roteirizado ou sorteado de tabela — inclui colapso demográfico
      por doença como mortalidade parametrizada de cenário.
- [ ] Colônia tem custo de viagem e atraso de comunicação derivados da distância orbital,
      modelado como fila de eventos com tick de entrega explícito (mesmo padrão do salto temporal
      da Fase 18) — autonomia e eventual independência são divergência cultural acumulada mais
      atraso, sem sistema político novo.

## Out of Scope

| Item | Razão |
| --- | --- |
| Galáxia e multiverso | Não há degrau acima de `sistema` — inventá-lo agora custaria sem pagar nada. |
| Trânsito entre linhas temporais | Fase 20. |
| Ramificação temporal em si | Fase 18 — esta fase não introduz salto no tempo. |
| Culto de carga como economia de crença completa | Fase 17 — aqui só é um desfecho possível de assimetria tecnológica, não a mecânica de culto. |
| Tecnologia alienígena como fonte de potência (`PowerDescriptor`) | Fase 16 — se um alien precisar de poder extraordinário, usa o motor já existente, não é definido aqui. |
| Prosa sobre o primeiro contato | Fase 12 (Narrativa). |
| Epidemiologia completa (modelo SIR, transmissão pessoa-a-pessoa, etc.) | Decisão explícita do usuário — colapso por doença é mortalidade parametrizada, não sistema de epidemia. |
| Entidade política/cultural nova para colônia independente | Decisão explícita do usuário — independência é divergência acumulada com limiar, cidade continua a mesma entidade de Fase 8. |

## Assumptions & Decisions (confirmadas com o usuário — 2026-08-25)

| Decisão | Escolha confirmada | Racional |
| --- | --- | --- |
| Origem de civilização distante | **Existe desde o tick 0, agregada** — mesma disciplina de LOD da Fase 8/9; contato promove, nunca cria | Usuário confirmou explicitamente (Recommended) — preserva conservação |
| Previsibilidade de eclipse/conjunção | **Propriedade do fenômeno + filtro cultural** — motor sempre calcula (determinístico); o que a cultura sabe prever é filtro de conhecimento (Fase 13) por cima do mesmo dado | Usuário confirmou explicitamente — mesmo dado, dois usos, sem duplicar sistema |
| Atraso de comunicação metrópole↔colônia | **Fila de eventos com tick de entrega explícito** — mesmo padrão de evento anexado com tick alvo já usado no salto temporal (Fase 18) | Usuário confirmou explicitamente (Recommended) |
| Colapso demográfico por doença no contato | **Mortalidade parametrizada pelo cenário** — taxa/curva declarada, nunca sistema de epidemiologia | Usuário confirmou explicitamente (Recommended) |
| Colônia independente | **Divergência acumulada com limiar, sem entidade política/cultural nova** — cidade continua a mesma entidade da Fase 8, só muda de afiliação/cultura dominante | Usuário confirmou explicitamente (Recommended) |

**Todas as 5 decisões estão confirmadas** — nenhuma pendência bloqueando Design.

## User Stories

### P1: Degraus `sistema`/`planeta` no topo da pilha de LOD, sem máquina nova

**User Story**: Como quem quer escalar o mundo além do planeta, quero que `sistema` e `planeta`
sejam só mais dois degraus na pilha de LOD já existente (Fase 8/9), com a mesma regra de
conservação, sem nenhum motor de simulação paralelo.

**Why P1**: É a fundação arquitetural — sem ela, todo o resto desta fase vira sistema duplicado.

**Acceptance Criteria**:

1. WHEN um corpo/região no degrau `sistema` ou `planeta` é promovido a detalhe THEN a mesma
   invariante de conservação já usada na promoção Fase 8 (soma do agregado + `COUNT(*)`
   promovido bate com o total, lido sem tocar a propriedade derivada) SHALL valer, um nível
   acima.
2. WHEN o degrau `sistema` é adicionado a um mundo sem nenhum evento de contato THEN o hash
   canônico SHALL permanecer byte-idêntico ao do mesmo mundo sem o degrau — a cada tick em 10
   anos no gate, 100 anos em nightly.
3. WHEN um evento de contato ocorre no mesmo cenário (braço oposto) THEN o hash canônico SHALL
   necessariamente mudar em relação ao braço sem contato — provando que o degrau participa da
   simulação quando tem consequência.
4. WHEN o mesmo `ISimulationSystem`/mecanismo de materialização já usado pra cidades (Fase 8) é
   reusado pro degrau `sistema`/`planeta` THEN nenhum novo tipo de "motor de simulação" SHALL
   ser introduzido — só novos degraus na mesma pilha.

**Independent Test**: mundo com degrau `sistema` habilitado, sem nenhum contato agendado no
cenário — hash idêntico ao baseline sem o degrau em 10 anos; mesmo mundo com um contato agendado
diverge do baseline.

---

### P1: Corpos e órbitas em estatística pura

**User Story**: Como quem desenha um sistema estelar, quero que estrela, luas, planetas,
elementos orbitais e recursos orbitais vivam em estatística agregada — um tick de sistema produz
totais e datas, nunca simula pessoas individuais.

**Why P1**: Preserva o teto de custo por NPC-tick (Fase 9) mesmo escalando pra sistema estelar.

**Acceptance Criteria**:

1. WHEN um corpo celeste é criado no degrau `sistema` THEN ele SHALL ser representado por
   elementos orbitais + agregados estatísticos (população, recursos, tecnologia) — nunca por
   `Npc` individuais nesse degrau.
2. WHEN um tick de sistema roda THEN o custo computacional SHALL ser função do número de corpos
   agregados, não do número de habitantes que qualquer corpo representa estatisticamente —
   mesmo espírito de custo-por-agregado já medido na Fase 9.
3. WHEN um corpo do sistema é promovido a detalhe (contato ou colonização) THEN a região
   promovida SHALL herdar população/recursos/tecnologia coerentes com o agregado de origem —
   nunca valores gerados do zero desconectados do total anterior.

**Independent Test**: sistema com 5 corpos agregados simulado por 100 anos — custo por tick não
escala com a soma dos totais populacionais declarados nos corpos.

---

### P1: Calendário astronômico derivado, com consequência dupla

**User Story**: Como quem quer que o céu tenha peso no mundo, quero que estações, eclipses,
cometas e conjunções sejam calculados a partir dos elementos orbitais (determinístico,
previsível pelo motor), e que a mesma efeméride sirva de modificador agrícola objetivo E de
presságio cultural — filtrado pelo conhecimento astronômico da cultura que observa.

**Why P1**: É o "gancho" que torna astronomia relevante em vez de decorativa.

**Acceptance Criteria**:

1. WHEN o calendário astronômico é consultado para qualquer tick THEN o motor SHALL derivar
   estações/eclipses/cometas/conjunções dos elementos orbitais declarados — nunca uma tabela
   escrita à mão por cenário.
2. WHEN um eclipse ou estação adversa cai na janela de colheita declarada no cenário THEN a
   produção agrícola (Fase 5) SHALL ser modificada objetivamente, independente de qualquer
   cultura "saber" ou não do fenômeno.
3. WHEN a mesma efeméride é consultada pela camada cultural (Fase 10/17) THEN a interpretação
   (presságio vs. evento previsto/ferramenta política) SHALL depender do filtro de conhecimento
   astronômico da cultura observadora (Fase 13) — nunca um segundo cálculo do fenômeno em si.
4. WHEN um par base/tratamento na mesma seed posiciona um eclipse (e, separadamente, uma
   estação adversa) na janela de colheita THEN a produção agrícola do braço tratado SHALL ser
   menor que a do base em 10/10 seeds, com diferença maior que o spread entre seeds do
   baseline.

**Independent Test**: par base/tratamento com eclipse forçado na janela de colheita — produção
menor no tratado, 10 seeds; consulta cultural do mesmo evento retorna presságio pra cultura sem
conhecimento astronômico e efeméride prevista pra cultura com conhecimento.

---

### P1: Contato promove civilização distante ao detalhe, sem criar população

**User Story**: Como quem quer contato alienígena emergente, quero que uma civilização distante
exista como agregado desde o tick 0 e que o evento de contato promova a região tocada ao mesmo
detalhe que qualquer região da Fase 8 — cultura, liderança e economia coerentes com o agregado de
origem.

**Why P1**: É a garantia de conservação — sem ela, contato "cria" população do nada.

**Acceptance Criteria**:

1. WHEN uma civilização distante é declarada no cenário THEN ela SHALL existir como agregado
   estatístico (população, tecnologia, expansão) desde o tick 0, mesmo sem nunca ter tido
   contato — mesma disciplina de "agregado sempre existe" da Fase 9.
2. WHEN o evento de contato ocorre THEN a região tocada SHALL ser promovida a detalhe (mesmo
   mecanismo de materialização da Fase 8), com cultura/liderança/economia derivadas
   coerentemente do agregado de origem — nunca geradas independentemente dele.
3. WHEN o round-trip de promover a detalhe e depois desmaterializar de volta é aplicado à região
   de contato THEN `Hash(world)` SHALL ficar byte-idêntico ao estado antes da promoção — totais
   de população, recurso e produção inclusos (mesmo critério de round-trip já usado na Fase 8).

**Independent Test**: civilização distante existente desde tick 0 sem contato por 50 anos —
evento de contato promove a região; round-trip promover→desmaterializar produz hash idêntico ao
pré-promoção.

---

### P1: Alien e conquistador não são tipo novo

**User Story**: Como quem quer aliens sem duplicar o motor, quero que uma civilização contatante
alcance exatamente os mesmos sistemas (cultura, tecnologia, economia, guerra, diplomacia) que uma
cultura nativa alcança — nenhum handler, tabela ou campo exclusivo de "alien".

**Why P1**: É a garantia central de reuso da fase — sem ela, "alien" vira um segundo motor de
sociedade.

**Acceptance Criteria**:

1. WHEN uma civilização contatante interage com o mundo THEN toda interação SHALL passar pelos
   mesmos handlers que uma cultura nativa em nível tecnológico equivalente usaria — nenhum
   campo/tabela/handler existe só para o caso "alien".
2. WHEN os sistemas alcançados por uma civilização contatante são enumerados por reflexão THEN o
   teste SHALL reprovar se ela tocar qualquer handler/tabela/campo que uma cultura nativa
   equivalente não toque — cobertura nos dois sentidos: um sistema alcançado por cultura nativa
   sem par testado do lado alien também reprova.
3. WHEN o degrau tecnológico de uma civilização contatante é declarado THEN ele SHALL vir do
   vocabulário de módulos de conteúdo da Fase 13 (ex.: pacote "futurista") — nunca um enum de
   tecnologia paralelo criado nesta fase.

**Independent Test**: enumeração por reflexão de handlers tocados por uma frota "futurista"
contatante vs. uma cultura nativa "medieval" — mesma superfície de sistemas, só o conteúdo
(valores/parâmetros) difere.

---

### P1: Assimetria tecnológica é pressão calculada, nunca roteirizada

**User Story**: Como quem quer contato emergente, quero que o desfecho de um encontro
tecnologicamente assimétrico (colapso cultural, culto de carga, conquista, tutela, extermínio,
adaptação) saia dos valores culturais, coesão política e intenção de quem chegou — nunca
sorteado de tabela fixa, nem roteirizado por cenário.

**Why P1**: É a promessa central de emergência da fase — sem ela, contato vira cutscene.

**Acceptance Criteria**:

1. WHEN um encontro tecnologicamente assimétrico ocorre THEN o desfecho SHALL ser calculado a
   partir de valores culturais existentes (Fase 10/13), coesão política existente (Fase 8) e
   parâmetros de intenção declarados pra civilização contatante — nunca por um sorteio de
   tabela fixa de "resultados de contato".
2. WHEN colapso demográfico por doença é um desfecho possível THEN ele SHALL ser aplicado como
   mortalidade parametrizada pelo cenário (taxa/curva declarada) — nunca exigir um sistema de
   epidemiologia (transmissão pessoa-a-pessoa) que o roadmap não tem em fase nenhuma.
3. WHEN o mesmo cenário de contato roda com parâmetros culturais/políticos diferentes (seeds ou
   configuração variada) THEN desfechos observavelmente diferentes SHALL ocorrer — provando que
   o resultado é função dos parâmetros, não hardcoded.

**Independent Test**: mesmo cenário de contato rodado com coesão política alta vs. baixa produz
desfechos distintos e mensuravelmente correlacionados (ex.: coesão alta reduz taxa de colapso
cultural); mortalidade por doença aplicada como parâmetro simples, sem exigir modelo de
transmissão.

---

### P1: Colônia com atraso de comunicação por fila de eventos

**User Story**: Como quem quer colônias autônomas de fato, quero que uma ordem da metrópole seja
um evento anexado com tick de entrega calculado pela distância orbital, e que a colônia decida
com o que já chegou até aquele tick — nunca com informação do futuro.

**Why P1**: É o mecanismo que torna autonomia colonial mensurável e testável (mesma família do
teste de conhecimento limitado da Fase 11).

**Acceptance Criteria**:

1. WHEN uma ordem é enviada da metrópole a uma colônia THEN ela SHALL ser um evento anexado com
   tick de entrega calculado pela distância orbital declarada — nunca visível à colônia antes
   desse tick.
2. WHEN o cenário planta uma ordem cujo tick de entrega ainda não passou THEN a decisão da
   colônia naquele tick SHALL ser byte-idêntica à de um braço de controle onde a ordem nunca foi
   enviada — qualquer divergência é informação viajando mais rápido que o atraso (mesma família
   do teste de conhecimento limitado da Fase 11).
3. WHEN a divergência cultural acumulada de uma colônia (função de tempo desde o último contato
   + atraso médio) ultrapassa um limiar declarado no cenário THEN a colônia SHALL ser marcada
   como independente — sem criar entidade política ou cultural nova: a cidade continua a mesma
   entidade da Fase 8, só muda de afiliação/cultura dominante registrada.

**Independent Test**: ordem plantada com tick de entrega futuro — decisão da colônia antes desse
tick idêntica ao braço sem ordem nenhuma; divergência cultural forçada acima do limiar marca a
colônia como independente sem alterar sua contagem/identidade de cidade.

## Edge Cases

- WHEN um corpo do sistema nunca é promovido a detalhe durante toda a simulação THEN ele SHALL
  permanecer puramente estatístico indefinidamente — nenhum custo de detalhe é pago sem evento
  de promoção.
- WHEN dois eventos de contato ocorrem na mesma região no mesmo tick (concorrência) THEN o motor
  SHALL resolver deterministicamente qual promove primeiro (mesma disciplina de ordenação
  determinística já usada em outras fases) — nunca condição de corrida silenciosa.
- WHEN uma colônia recebe múltiplas ordens com ticks de entrega diferentes THEN cada uma SHALL
  ficar invisível até seu próprio tick de entrega — entregas fora de ordem (ordem 2 chega antes
  da ordem 1 por rota mais curta, se o cenário permitir) são possíveis e não corrompem estado.
- WHEN a taxa de mortalidade por doença parametrizada é 0 (cenário sem esse desfecho habilitado)
  THEN nenhuma mortalidade adicional SHALL ocorrer por contato — parâmetro ausente nunca aplica
  default não-zero surpresa.

## Requirement Traceability

| ID | Story | Status |
| --- | --- | --- |
| COS-01 | P1: Degraus — conservação na promoção, um nível acima | Pending |
| COS-02 | P1: Degraus — sem contato, hash byte-idêntico ao baseline sem o degrau | Pending |
| COS-03 | P1: Degraus — com contato, hash diverge do braço sem contato | Pending |
| COS-04 | P1: Degraus — reusa materialização existente, nenhum motor novo | Pending |
| COS-10 | P1: Corpos — representados por elementos orbitais + agregados, nunca Npc individuais | Pending |
| COS-11 | P1: Corpos — custo por tick função do nº de agregados, não de população interna | Pending |
| COS-12 | P1: Corpos — promoção herda coerência do agregado de origem | Pending |
| COS-20 | P1: Calendário — derivado dos elementos orbitais, nunca tabelado | Pending |
| COS-21 | P1: Calendário — eclipse/estação modifica produção agrícola objetivamente | Pending |
| COS-22 | P1: Calendário — interpretação cultural filtrada por conhecimento (Fase 13), mesmo dado | Pending |
| COS-23 | P1: Calendário — par base/tratamento, produção menor no tratado, 10/10 seeds | Pending |
| COS-30 | P1: Contato — civilização distante agregada desde tick 0 | Pending |
| COS-31 | P1: Contato — promoção coerente com o agregado de origem | Pending |
| COS-32 | P1: Contato — round-trip promover/desmaterializar preserva hash | Pending |
| COS-40 | P1: Alien não é tipo novo — mesma superfície de handlers que cultura nativa | Pending |
| COS-41 | P1: Alien não é tipo novo — enumeração por reflexão, cobertura nos 2 sentidos | Pending |
| COS-42 | P1: Alien não é tipo novo — degrau tecnológico vem do vocabulário da Fase 13 | Pending |
| COS-50 | P1: Assimetria — desfecho calculado de valores/coesão/intenção, nunca tabela fixa | Pending |
| COS-51 | P1: Assimetria — doença é mortalidade parametrizada, nunca epidemiologia | Pending |
| COS-52 | P1: Assimetria — parâmetros diferentes produzem desfechos observavelmente diferentes | Pending |
| COS-60 | P1: Colônia — ordem é evento anexado com tick de entrega por distância orbital | Pending |
| COS-61 | P1: Colônia — decisão antes da entrega idêntica ao braço sem ordem (conhecimento limitado) | Pending |
| COS-62 | P1: Colônia — independência é divergência+limiar, sem entidade nova | Pending |

**Coverage**: 22 total. Todas as decisões de Assumptions confirmadas — sem bloqueio pra Design.

## Success Criteria

- [ ] Cosmos não vaza sem evento: hash byte-idêntico com/sem degrau `sistema` (sem contato), a
      cada tick em 10 anos no gate, 100 em nightly — e diverge com contato no cenário oposto.
- [ ] Conservação orbital: soma do agregado + `COUNT(*)` promovido bate com o total, sem tocar
      propriedade derivada, mesmo teste da Fase 8 um nível acima.
- [ ] Eclipse/estação mexem na colheita com controle pareado, 10/10 seeds, diferença maior que
      spread entre seeds do baseline.
- [ ] Contato promove e devolve sem perda: round-trip preserva hash, população, recurso e
      produção.
- [ ] Colônia decide com o que já chegou: decisão byte-idêntica entre braço com ordem futura e
      braço sem ordem nenhuma (mesma família da Fase 11).
- [ ] Alien não é tipo novo: enumeração por reflexão sem handler/tabela/campo exclusivo, nos dois
      sentidos de cobertura.
- [ ] `dotnet test` completo sem regressão nas suítes `Cities*`/`Economy*`/`History*`/`Society*`.
