# Fase 16.1 — Motor de Poder Genérico (Especificação)

## Problem Statement

A Fase 16 entregou a arquitetura certa no papel (`ADR-0010`: poder é modificador sobre um
sistema que já existe, nunca um caso especial) mas a implementação ficou presa a um
vocabulário fechado: `ExtraordinaryInvocationEngine.PrepareEffects`/`PrepareCosts` são um
`switch` C# com ~10 chaves reconhecidas (`npc.{health,hunger,thirst,sleep,social}`,
`movement.flight`, `movement.speed-multiplier`, `construct.create`, e os dois adicionados
nesta sessão: `npc.teleport`, `npc.force-action`). O usuário trouxe uma lista de ~300 poderes
de referência (imortalidade, precognição, leitura de mente, transferência de vida, clima,
transmutação, gravidade, tempo, genética/evolução de poder, etc.) e pediu um motor onde
qualquer um desses seja **descritível sem recompilar uma categoria nova por poder**, sempre
ancorado numa mecânica real da simulação (existente ou construída agora) — nunca uma ação
mágica solta.

## Goals

- [ ] Generalizar o motor de efeito/custo de um switch fechado pra um **registro de
      mecânicas** (`IExtraordinaryMechanic`), onde adicionar uma mecânica nova é registrar
      uma classe, não editar um switch central — mantendo C# fortemente tipado (decisão do
      usuário: registro, não DSL/script interpretado).
- [ ] **Ao final desta fase, deve ser possível criar QUALQUER tipo de poder** — movimento,
      dano, interação com objeto, atributo físico, mental, social, econômico, ou qualquer
      outra categoria da lista de referência do usuário — sem exceção, com uma única exclusão
      deliberada: poderes de tempo/viagem no tempo (ver Out of Scope — Fase 18). Isso inclui
      construir do zero as mecânicas de base que faltam hoje (Força → limite de carga,
      velocidade de coleta/construção; Percepção → alcance de detecção, reação/decisão mais
      rápida; e Combate NPC-vs-NPC) — todas viram requisito P2 desta fase, não backlog à
      parte.
- [ ] Cobrir todas as mecânicas de base que a lista de poderes exige e o motor não tem hoje
      (detalhado nas User Stories P2) — cada uma com resultado real e testado, não só
      documentado como gap.
- [ ] Provar generalidade com uma amostra representativa (~15-20 poderes) cobrindo as
      categorias da lista do usuário, não os ~300 individualmente rastreados.
- [ ] Preservar toda garantia da Fase 16 original: determinismo por seed, conservação
      econômica (`ADR-0010`), custo pago mesmo em falha, `Extraordinary.Enabled=false` zera
      o sistema.

## Out of Scope

| Item | Razão |
| --- | --- |
| Implementar os ~300 poderes da lista individualmente | Motor genérico + amostra prova cobertura; poder específico é dado de cenário, não requisito rastreado (ver P1). |
| "Power Evolution" (árvore de evolução + mistura genética de poderes entre pais) | Documentado como P3 nesta spec (decisão do usuário), design registrado mas implementação fica pra depois — é um sistema de progressão/herança à parte, não um efeito. |
| **Poderes de tempo/viagem no tempo** (parar o tempo, rebobinar, loop temporal, ver/enviar informação ao passado, convocar versão passada/futura) | **ÚNICA exclusão da fase, por decisão explícita do usuário** — vira o tema central da Fase 18 (Timelines), que já existe no roadmap pra isso. Tudo que não é tempo entra nesta fase, sem exceção — Força/Percepção/Combate inclusos (ver User Stories P2). |
| DSL/linguagem de script pra descrever poder em runtime sem C# | Decisão explícita do usuário: registro C# tipado, não interpretador — ver Assumptions. |
| Reescrever `manifestationCondition`/`AcquisitionRules` do zero | Ambos já são suficientemente genéricos (grammar livre); esta spec generaliza `effects`/`costs`, não esses dois eixos (que já não são switch fechado). |

---

## Conceitos Fundamentais Necessários (pré-requisitos de mecânica, não mecânicas em si)

**Correção (2026-08-24, apontada pelo usuário):** a v1 desta spec generalizava o motor de
efeito/custo, mas não dizia o que fazer com **voo e supervelocidade já existentes** (hoje
`movement.flight`/`movement.speed-multiplier` são chaves especiais dentro do próprio switch
que esta fase existe pra eliminar) nem listava, à parte, os **conceitos de domínio** (não
mecânicas de poder — coisas que a simulação em si não modela) que vários poderes da lista
exigem como fundação. Ambos os buracos são corrigidos aqui.

Mecânica de poder (`IExtraordinaryMechanic`) e conceito de domínio são coisas diferentes:
mecânica é COMO um poder se pluga no motor; conceito de domínio é O QUE existe na simulação
pra a mecânica ler/escrever. Sem o conceito, a mecânica não tem alvo — mesma armadilha já
identificada antes nesta conversa pra Força/Percepção (motor sem regra de base pra modificar).

| Conceito novo | Por que falta hoje | Poderes da lista que dependem dele | Vira história |
| --- | --- | --- | --- |
| **Gravidade pessoal** (campo de gravidade por NPC, não só um par de flags `CanFly`/`SpeedMultiplier` soltos em `ExtraordinaryLocomotion`) | Motor já SIMULA o efeito de voar, mas não como gravidade — é um flag booleano dedicado. "Manipular gravidade ao redor" (o próprio enquadramento do usuário pro voo) não existe como conceito, só o resultado final dele. | Voo gravitacional, controle gravitacional (aumentar/reduzir/lateral, poços gravitacionais, tornar pesado/leve, desviar projétil), construtor impossível (peso de material) | PWR-70 (recria voo/velocidade via gravidade+cinética) |
| **Temperatura/clima local** (valor de temperatura por célula/região, hoje inexistente — `MapGenerator`/biomas têm altitude e tipo de terreno, nunca temperatura) | Nenhum sistema de clima/temperatura na simulação — bioma é só um id colorido (confirmado em sessão anterior: "não há semântica de bioma real ainda"). | Controle do clima local, sopro congelante, resistência ambiental (frio/calor/pressão/radiação), absorção solar, biologia solar | PWR-74 |
| **Fauna** (animal como entidade simulada mínima — hoje zero: confirmado em recon anterior desta mesma conversa, "flora/fauna não existem como agentes simulados", só `CropSystem` como estoque econômico) | Sem NENHUM animal simulado, "mestre dos animais"/"poderes animais"/"portador da peste" não têm o que controlar, comunicar ou infectar. | Mestre dos animais, comunicação/dominação/empatia animal, transformação animal (parcial), portador da peste (precisa de vetor, animal é o mais natural) | PWR-77 |
| **Memória/cognição privada** (registro do que UM NPC especificamente presenciou/sabe, distinto de `Personality` — que é só 10 traços numéricos — e do sistema de crenças/rumor já existente da Fase 10, que é público/social, não pessoal) | Confirmado nesta conversa: `memories: string[]` existe no contrato da API mas é placeholder — nenhum campo equivalente em `Npc` no domínio. `beliefs` (Fase 10, rumor/relato) é o mais próximo, mas é conhecimento PÚBLICO propagado, não memória PESSOAL nem segredo. | Leitor de mentes (segredos/mentiras), memória perfeita, roubo/apagar/implantar memória, necromancia social (info que morreu com alguém), consciência coletiva | PWR-80 |

Cada linha vira uma história P2 própria abaixo (a coluna "Vira história" já cita o
identificador). Nenhuma delas é reescrita do zero de um sistema gigante — cada uma é
escopada no MENOR conceito que já desbloqueia os poderes citados, seguindo a mesma disciplina
de "modificador sobre sistema existente" do `ADR-0010` (reusar o que já existe: `CropSystem`
pra fauna não precisa duplicar economia; `Fact`/eventos causais pra memória não precisa
duplicar o log; footprint/colisão já validada pra gravidade não precisa duplicar
pathfinding).

---

## Assumptions & Open Questions

| Assumption / decisão | Escolha | Racional | Confirmado? |
| --- | --- | --- | --- |
| Arquitetura do motor | Registro C# de mecânicas (`IExtraordinaryMechanic`), não DSL/script | Usuário escolheu explicitamente — preserva determinismo/tipagem que `ADR-0005` exige; script interpretado seria projeto à parte com risco de determinismo maior | s |
| Amplitude do P1 | Motor genérico + amostra representativa (~15-20 poderes), não os ~300 individualmente | Usuário confirmou explicitamente | s |
| Power Evolution | Entra como P3 documentado nesta spec, não implementado | Usuário confirmou explicitamente | s |
| Gaps de mecânica ausente | Cada um vira requisito P2 com critério de aceite real, não só nota de gap | Usuário confirmou explicitamente ("necessário implementar cada uma antes de finalizar essa fase") | s |
| **Manipulação de tempo "forte" (parar o tempo, rebobinar, loop temporal)** | Fora do fechamento da Fase 16.1 — endereçado na Fase 18 (Timelines), onde dilatação de tempo/rewind já é o tema central | O motor de tick é global e síncrono (`WorldClock`/scheduler); "parar o tempo" pra 1 NPC enquanto o resto do mundo continua exige dilatação de tempo por entidade — isso é redesenho de scheduler, do tamanho de uma fase inteira, não modificador de poder. O que **é** fechável na 16.1 (envelhecimento/senescência, cadência de decisão) já é PWR-20..23. | **Confirmado pelo usuário — 2026-08-24** |
| **Transmutação/duplicação de matéria vs. conservação econômica** | Confirmado: poder de transmutação só cria/destrói valor através do canal já auditado (`WorldEventKind.Minted`/`Destroyed`), nunca fora de um evento causal registrado | `ADR-0010`/critério de verificação original da Fase 16 exige conservação: "nenhum poder cria valor fora dos campos monotônicos". Transmutação livre (ferro→ouro sem custo) violaria isso por design — a lista do usuário até descreve a consequência pretendida (inflação/colapso), o que só é seguro se o "mint" for auditável, não invisível. Vira requisito P2 (`PWR-35..38`). | **Confirmado pelo usuário — 2026-08-24** |
| Força/Percepção/Combate | **Correção (2026-08-24): entram nesta fase, não são backlog à parte.** Usuário foi explícito: "TUDO que é necessário para poderes entra nesta fase sem exceção". Ordem de construção já combinada anteriormente (carga → coleta/construção → percepção → reação → combate por último) vira a ordem de implementação em Design/Tasks, não uma exclusão de escopo. | s |
| **Precognição/profecia sem viagem no tempo** | Entra nesta fase como leitura probabilística (nunca muta o mundo, nunca rebobina/pausa) — não conta como a exclusão de "tempo forte" | Usuário pediu "adicione tudo" numa lista de 12 gaps sem responder essa ambiguidade específica (eu tinha perguntado se conta como tempo-excluído ou mecânica própria). Assumido que NÃO é tempo (só lê um resultado provável, nunca altera o relógio nem o passado) — mesma lógica de `Resolver`/Sorte já usada. | **Suposição do agente — não confirmada explicitamente, sinalizar se estiver errado** |
| **Instanciação de NPC / possessão / vínculo / dimensional / fantasma** (as 5 mecânicas de maior risco arquitetural do lote de 12 adicionado) | Documentadas com AC completo, mas SHALL entrar na ordem de implementação só depois das mecânicas mais simples (mesma disciplina de "combate por último" já aplicada a Força) | Não pedido explicitamente pelo usuário, é julgamento do agente pra não colocar o trabalho de maior risco (criar/matar NPC, redirecionar decisão social, mutar identidade) na frente do que é mais barato e teria menos superfície de regressão | s (ordem, não escopo — nada foi excluído) |

**Open questions:** uma (precognição = suposição do agente, ver acima, não uma pergunta
literal ao usuário — mas fica marcada como não-confirmada explicitamente até ele validar).
As demais 5 perguntas/correções anteriores (tempo forte, transmutação, Força/Percepção/
Combate, e agora o lote de 12 gaps adicionais) foram todas resolvidas com o usuário em
2026-08-24.

---

## User Stories

### P1: Registro de mecânicas substitui o switch fechado ⭐ MVP

**User Story**: Como quem desenha poderes pra este mundo, quero que adicionar uma mecânica
nova ao motor seja registrar uma classe (não editar um switch central cada vez maior), pra
o motor crescer sem virar um arquivo ingovernável.

**Why P1**: É a fundação — nenhuma mecânica nova (P2) ou poder de amostra é possível sem
isso existir primeiro.

**Acceptance Criteria**:

1. WHEN um descritor declara um token de efeito/custo (`<mecânica>.<chave>:<magnitude...>`)
   THEN o motor SHALL resolver via um `IExtraordinaryMechanic` registrado pra aquele
   namespace, nunca por um `switch` central crescendo por poder.
2. WHEN uma mecânica nova é adicionada THEN a mudança SHALL se limitar a uma nova classe
   registrada (+ registro no composition root) — nenhuma edição no laço de
   `ExtraordinaryInvocationEngine.Invoke`/`Prepare` em si.
3. WHEN um token declara uma chave de mecânica não registrada THEN o motor SHALL falhar com
   a mesma mensagem de contrato de hoje (`"Effects: alvo não suportado '<chave>'"` /
   equivalente pra custo) — nenhuma regressão de segurança.
4. WHEN o motor resolve um efeito/custo THEN o resultado SHALL continuar 100%
   determinístico pra mesma seed/entrada (nenhuma mecânica introduz `DateTime.Now`,
   `Random` não semeado, ou ordem de iteração não determinística).
5. WHEN uma invocação declara custo insuficiente, alvo morto/ausente, ou modo/confiabilidade
   inválidos THEN o motor SHALL rejeitar **antes** de aplicar qualquer efeito (atomicidade
   preservada — mesma garantia da Fase 16 original).

**Independent Test**: registrar as mecânicas já existentes (`npc.*` stats, `movement.*`,
`construct.create`, `npc.teleport`, `npc.force-action`) através do novo registro e confirmar
que toda a suíte `ExtraordinaryInvocationEngineTests`/`ExtraordinaryLocomotionTests` passa
sem alterar nenhuma asserção de comportamento — a migração é invisível de fora.

---

### P1: Primitiva de seletor de área/região (multi-alvo) ⭐ MVP

**User Story**: Como quem desenha uma aura de liderança, um controle de clima local ou um
alcance de percepção, quero declarar um poder que afeta TODOS os NPCs num raio/região do
portador, não um único `TargetId`, pra não precisar reinventar "quem é afetado" em cada
mecânica nova.

**Why P1**: Autocorreção — as histórias de Percepção, Temperatura e Fauna (escritas antes
desta) já assumiam informalmente um "raio"/"região" sem essa noção existir formalmente no
contrato de invocação (`ExtraordinaryInvocation.TargetId` é um único `NpcId` hoje). Sem essa
primitiva, cada mecânica de área reimplementaria sua própria lógica de seleção — exatamente
o tipo de duplicação que o registro de mecânicas (primeira história P1) existe pra evitar.

**Acceptance Criteria**:

1. WHEN um poder declara um seletor de área (`area:radius:<n>` ou `area:region:<id>`) em vez
   de um `TargetId` único THEN o motor SHALL resolver o conjunto de NPCs afetados (todos
   dentro do raio do portador, ou todos numa região/cidade declarada) e aplicar o mesmo
   efeito/custo a cada um, individualmente e deterministicamente (ordem por `NpcId`).
2. WHEN o poder declara `area:radius:<n>` THEN o raio SHALL ser medido a partir da posição
   atual do portador (`CurrentLocation`), recalculado a cada invocação — nunca fixado no
   momento da aquisição do poder.
3. WHEN nenhum NPC está dentro da área declarada THEN a invocação SHALL suceder sem efeito
   nenhum aplicado (não é erro — mesma semântica de "poder válido, zero alvos", não falha).
4. WHEN um efeito de área também tem custo declarado THEN o custo SHALL ser cobrado uma
   única vez do portador (não multiplicado pelo número de alvos atingidos), salvo declaração
   explícita em contrário no descritor.

**Independent Test**: poder de área com raio 3 aplicado num cenário com NPCs dentro e fora
do raio — só os de dentro recebem o efeito, custo é debitado uma vez do portador,
determinístico pra mesma seed/posições.

---

### P1: Primitiva de transferência (dois alvos) ⭐ MVP

**User Story**: Como quem desenha um poder de "transferência de vida"/"drenagem vital"/"roubo
de habilidade", quero declarar um efeito que debita de uma pessoa e credita em outra na MESMA
invocação, pra não precisar simular isso como dois poderes separados sem relação causal.

**Why P1**: Sem esta primitiva, ~15 poderes da lista (transferência de vida, drenagem vital,
roubo de habilidade, vínculo de destino, doação/roubo de velocidade) não são exprimíveis —
hoje o motor só sabe "efeito no alvo" e "custo no portador", nunca os dois amarrados como uma
troca.

**Acceptance Criteria**:

1. WHEN um descritor declara um efeito de transferência (`transfer.<atributo>:<magnitude>`)
   THEN o motor SHALL debitar `magnitude` de uma parte (portador OU alvo, declarado) e
   creditar a mesma quantia (ou uma fração declarada) na outra, na mesma transação atômica.
2. WHEN a parte doadora não tem saldo suficiente pro atributo declarado THEN a invocação
   SHALL falhar por completo (nenhum crédito parcial) — mesma regra de atomicidade de custo
   já vigente.
3. WHEN a transferência atinge um destino que já está no teto do atributo (ex.: saúde já em
   100) THEN o excedente SHALL ser descartado (clamp, mesma regra de `ClampNeed` já usada),
   nunca acumular acima do teto nem falhar a invocação por isso.
4. WHEN o mesmo par origem/destino é usado em `PrepareCosts` (portador) e neste novo efeito
   (alvo) simultaneamente THEN a ordem de aplicação SHALL ser determinística (custo sempre
   antes do efeito de transferência, igual à ordem já estabelecida custo→efeito).

**Independent Test**: um poder `transfer.health:20` do portador pro alvo debita exatamente
20 de saúde do portador (respeitando `ClampNeed`) e credita exatamente 20 no alvo (ou menos,
se bateu no teto) — par de mundos controle/tratado confirma conservação do total transferido,
igual ao padrão `Paired_control_accounts_for_every_debited_resource_unit` já existente.

---

### P2: Mecânica — Não-envelhecimento / senescência controlável

**User Story**: Como quem cria um poder de "imortalidade imperfeita" ou "regeneração
extrema", quero que a taxa de envelhecimento de um NPC seja de fato lida pelo sistema de
mortalidade, pra "não envelhece, mas ainda pode morrer" funcionar de ponta a ponta.

**Why P2**: `PowerDescriptor.SenescenceRateMultiplier` e
`ExtraordinaryCarrierState.SenescenceRateMultiplier` **já existem** — foram declarados na
Fase 16 original mas `MortalitySystem`/`MortalityPlanner` (que rolam idade de morte)
**nunca leem esse valor**. É o gap mais barato da lista: dado já existe, falta o consumidor.

**Acceptance Criteria**:

1. WHEN `MortalitySystem.SchedulePlannedDeath` rola a idade de morte de um NPC portador de
   poder ativo com `SenescenceRateMultiplier < 1` THEN a idade de morte planejada SHALL ser
   proporcionalmente adiada (multiplicador aplicado à progressão de idade biológica usada
   por `MortalityPlanner.RollDeathAge`, não ao relógio do mundo).
2. WHEN `SenescenceRateMultiplier == 0` THEN o NPC SHALL nunca ter morte por idade agendada
   enquanto o poder estiver manifestado — mas SHALL continuar sujeito a
   `MortalitySystem`/`NeedsDecaySystem` por fome, e a qualquer efeito/custo de outro poder
   que reduza `Health` a zero (imortalidade é "não envelhece", nunca "invulnerável").
3. WHEN o poder deixa de estar manifestado (condição de manifestação some) THEN a próxima
   rolagem de morte SHALL usar o multiplicador vigente no momento da rolagem (nunca
   retroativo a uma morte já agendada sob o multiplicador antigo, mesma semântica de "estado
   observado no momento", sem reagendar eventos passados).
4. WHEN dois poderes ativos declaram `SenescenceRateMultiplier` diferentes no mesmo NPC
   THEN o motor SHALL continuar usando o mínimo entre eles (mesma regra que
   `ExtraordinaryStateSystem.Resolve` já aplica pra outros eixos agregados).

**Independent Test**: par de mundos controle/tratado com o mesmo seed — NPC tratado com
`SenescenceRateMultiplier=0` nunca recebe evento `MortalitySystem` por idade em N anos
simulados; NPC controle (sem o poder) morre por idade dentro do `LifeTable` normal no mesmo
período.

---

### P2: Mecânica — Sorte / probabilidade determinística

**User Story**: Como quem cria um poder de "sorte" ou "azar direcionado", quero que ele
enviese resultados de resolução (sucesso/falha de ações, eventos aleatórios) sem quebrar
reprodutibilidade por seed.

**Why P2**: Lista do usuário pede sorte/azar/alteração de probabilidade como categoria
inteira — hoje não há gancho nenhum pra isso; o motor de resolução (`Resolver.Resolve`)
já existe e já é seedado, só falta um modificador declarado alimentando seus parâmetros.

**Acceptance Criteria**:

1. WHEN um NPC tem um poder ativo com efeito `luck.capacity-bonus:<n>` THEN qualquer
   `Resolver.Resolve` que use aquele NPC como portador/alvo da rolagem SHALL somar `n` à
   `capacity` antes de resolver — nunca substituir o stream de RNG nem o cálculo de
   dificuldade em si.
2. WHEN um poder declara `luck.curse:<n>` mirando outro NPC THEN as próximas resoluções
   daquele alvo, por uma janela declarada (ticks), SHALL subtrair `n` da capacidade dele.
3. WHEN a mesma seed e os mesmos poderes ativos são usados em duas execuções THEN o
   resultado SHALL ser byte-idêntico (nenhuma fonte de aleatoriedade nova fora do stream
   nomeado já existente).
4. WHEN o multiplicador de sorte levaria capacidade negativa THEN o motor SHALL clampar em
   zero, nunca inverter o sinal da rolagem de forma não documentada.

**Independent Test**: dois NPCs idênticos (mesma seed, mesmos atributos), um com
`luck.capacity-bonus:10`, resolvendo a mesma ação simulada N vezes — taxa de sucesso do
sortudo é estatisticamente maior, e a sequência exata de resultados é reproduzível entre
execuções com a mesma seed.

---

### P2: Mecânica — Leitura/alteração de mente (percepção social, não física)

**User Story**: Como quem cria um poder de "leitor de mentes", "empatia sobrenatural" ou
"controle emocional", quero consultar ou alterar o estado interno (personalidade,
necessidades, relação) de outro NPC através de um poder, não só via comando de
administração.

**Why P2**: `WorldAuthoringCommands.RewritePersonality`/`BreakRelationships` já mutam esse
estado — hoje só pela aba de administração (comando manual), nunca como efeito de poder
invocado. Ler personalidade/necessidade já é possível (dado existe); mutar já existe como
comando. Falta só o efeito de poder chamar o mesmo caminho.

**Acceptance Criteria**:

1. WHEN um poder declara efeito `mind.read` THEN a invocação SHALL expor (via
   `ExtraordinaryInvocationResult` ou log causal) os campos observáveis do alvo já públicos
   no domínio (`Personality`, necessidades, `Household`/`Spouse`) — SEM inventar dado novo
   (segredo/mentira não é modelado hoje; ver Out of Scope).
2. WHEN um poder declara efeito `mind.alter-trait:<traço>:<delta>` THEN o motor SHALL
   aplicar o delta ao traço de personalidade do alvo através do mesmo caminho de
   `WorldAuthoringCommands.RewritePersonality` (validado, causal), nunca escrevendo
   `Personality` diretamente por fora desse contrato.
3. WHEN a alteração de personalidade é temporária (duração declarada) THEN o motor SHALL
   reverter o traço ao valor original quando a manifestação cessar — requer registrar o
   valor pré-alteração no estado do portador/alvo (novo campo, ver Design).
4. WHEN o alvo já é portador do mesmo tipo de alteração (dois poderes competindo pelo mesmo
   traço) THEN a resolução SHALL ser determinística (última invocação aplicada vence, ordem
   por `InvocationId`) — nunca resultado dependente de ordem de iteração não determinística.

**Independent Test**: poder `mind.alter-trait:agreeableness:+30` some o delta ao traço,
`RewritePersonality` do alvo reflete o novo valor, e ao cessar a manifestação o traço volta
ao valor anterior — testável par a par como as demais mecânicas.

---

### P2: Mecânica — Transferência de vida / drenagem vital

**User Story**: Como quem cria "transferência de vida" ou "drenagem vital", quero que um
poder mova saúde ou anos de expectativa de vida de uma pessoa pra outra.

**Why P2**: Já coberto pela primitiva de transferência (P1) pro eixo `Health`. O gap real
aqui é **anos de vida** — não existe hoje um jeito de mover expectativa de vida entre NPCs
(a morte por idade é rolada uma vez, `SchedulePlannedDeath`, e não tem um "saldo de anos"
manipulável depois).

**Acceptance Criteria**:

1. WHEN um poder declara `transfer.lifespan-years:<n>` THEN o motor SHALL reagendar o evento
   de morte por idade já agendado da parte doadora pra `n` anos mais cedo, e o da parte
   receptora pra `n` anos mais tarde — nunca agendar no passado (mesma regra de
   `SchedulePlannedDeath` hoje: se cairia no passado, agenda pro próximo tick válido).
2. WHEN a parte doadora não tem `n` anos de sobra até sua morte já agendada THEN a invocação
   SHALL falhar (não pode doar mais do que tem).
3. WHEN a morte por idade de qualquer uma das partes já foi processada (evento consumido)
   THEN a transferência SHALL falhar explicitamente ("NPC ausente ou morto"), nunca tentar
   reagendar um evento que não existe mais.

**Independent Test**: doador e receptor com mortes agendadas conhecidas (seed fixa); após
`transfer.lifespan-years:10`, doador morre 10 anos antes do originalmente rolado, receptor
10 anos depois — soma total de "anos de vida no sistema" preservada (conservação, mesmo
espírito do teste de recurso).

---

### P2: Mecânica — Transmutação de matéria (via canal auditado, nunca invisível)

**User Story**: Como quem cria um poder de "transmutação" ou "duplicação de matéria", quero
que ele converta um recurso em outro (ou gere/destrua estoque) de um jeito que a economia
sinta de verdade (inflação, colapso), sem quebrar a garantia de conservação que o resto do
motor já exige.

**Why P2**: Usuário confirmou explicitamente a rota: reusar o canal de cunhagem já existente
(`WorldEventKind.Minted`/`Destroyed`) em vez de criar/destruir recurso por fora de um evento
causal — assim a "consequência econômica" pretendida pelo poder (inflação, colapso) fica
visível e rastreável nos mesmos testes de conservação da Fase 16 original, em vez de ser um
buraco silencioso na contabilidade.

**Acceptance Criteria**:

1. WHEN um poder declara `matter.transmute:<recursoOrigem>:<recursoDestino>:<taxa>` THEN o
   motor SHALL debitar o recurso de origem do estoque do portador (household) e creditar o
   recurso de destino na proporção `taxa`, emitindo `WorldEventKind.Destroyed` (origem) e
   `WorldEventKind.Minted` (destino) na mesma invocação — nunca um crédito sem o débito
   correspondente auditado.
2. WHEN o portador não tem estoque suficiente do recurso de origem THEN a invocação SHALL
   falhar por completo (mesma regra de atomicidade de custo/efeito já vigente) — nenhuma
   criação de valor parcial.
3. WHEN o teste de conservação de mundo (par controle/tratado, já existente na Fase 16
   original) roda com este poder ativo THEN a soma de `Minted`+`Destroyed` logados SHALL
   explicar 100% da mudança líquida de valor monetário/estoque — nenhuma mutação de sistema
   fora do que os eventos declaram (mesmo critério "nenhum efeito fora do declarado" já
   testado na Fase 16 original).
4. WHEN `taxa` implica ganho de valor (ex.: ferro→ouro) THEN o motor SHALL aplicar exatamente
   a taxa declarada no cenário (sem limite embutido no motor) — escassez/balanceamento
   inflacionário é decisão de cenário, não gate de arquitetura (mesma regra já registrada em
   `ADR-0010` pro eixo de escassez).

**Independent Test**: par de mundos controle/tratado — mundo tratado com um poder
`matter.transmute:iron:gold:1` ativo mostra exatamente 1 evento `Destroyed` (iron) + 1
`Minted` (gold) por invocação, e o sensor de conservação da Fase 16 original continua verde
(a mudança de estoque bate 100% com os eventos logados).

---

### P2: Mecânica — Limite de carga (Força, base)

**User Story**: Como quem cria um poder de "força sobre-humana" ou "construtor impossível",
quero que Força signifique algo mensurável — quanto o NPC consegue carregar — pra o poder
multiplicar um número real, não um conceito sem lastro.

**Why P2**: Hoje carregar é binário (`Npc.CarriedResourceId`/`CarriedQuantity`, sem limite de
peso) — não existe "quanto" carregar, só "se" carrega. É o primeiro degrau da sequência de
construção já combinada (carga → coleta → percepção → reação → combate) porque as outras
mecânicas de Força dependem de um número de capacidade existir primeiro.

**Acceptance Criteria**:

1. WHEN um NPC tenta `PickUp` além da sua capacidade de carga THEN o sistema SHALL rejeitar
   o excedente (capacidade vira um teto real, não documentação) — capacidade base vem de um
   atributo novo (`CarryCapacity`, ver Design pra onde vive: `Npc` ou derivado de `Vitality`).
2. WHEN um poder declara `attribute.strength:<multiplicador>` THEN a capacidade de carga do
   portador SHALL escalar por esse multiplicador enquanto o poder estiver manifestado.
3. WHEN o poder deixa de estar manifestado THEN a capacidade SHALL voltar ao valor base
   imediatamente (mesma semântica "sem estado gravado" de `ExtraordinaryLocomotion`).

**Independent Test**: NPC com capacidade base carrega até o teto e é bloqueado acima dele;
o mesmo NPC com `attribute.strength:3` ativo carrega 3× mais antes de ser bloqueado.

---

### P2: Mecânica — Velocidade de coleta/construção por força

**User Story**: Como quem cria "força sobre-humana", quero que ela também acelere coleta de
recursos e construção — não só carregar peso — pra "um superforte substitui dezenas de
trabalhadores" fazer sentido na economia.

**Why P2**: Produção/coleta/construção hoje só aceleram por `Skill`/`RateGene` (aprendizado),
nunca por força física. Vira um SEGUNDO multiplicador aplicado sobre a mesma taxa, não uma
substituição do sistema de skill existente.

**Acceptance Criteria**:

1. WHEN um poder declara `attribute.strength:<multiplicador>` THEN a taxa de produção/coleta
   (`ProductionSystem`/`SkillPracticeSystem`) e o consumo de recurso do
   `ConstructionSystem` do portador SHALL escalar por esse multiplicador, combinado
   (multiplicativo) com o multiplicador de `Skill`/`RateGene` já existente — nunca
   substituindo-o.
2. WHEN o poder cessa THEN a taxa SHALL voltar a refletir só `Skill`/`RateGene`, sem resíduo.
3. WHEN o multiplicador de força se combina com uma taxa de skill já no teto do sistema
   (se houver teto) THEN o resultado SHALL respeitar o mesmo teto — força não abre exceção
   nos limites já validados do sistema de produção.

**Independent Test**: par controle/tratado — NPC com `attribute.strength:2` produz/constrói
na taxa base × skill × 2; NPC controle (mesma skill, sem o poder) produz na taxa base × skill.

---

### P2: Mecânica — Percepção / alcance de detecção

**User Story**: Como quem cria "supervelocidade" (percepção) ou poderes de vigilância/aviso,
quero que o NPC "perceba" outro NPC ou perigo num raio maior que o normal, pra decisões de
segurança/fuga/encontro social reagirem a isso de verdade.

**Why P2**: Não existe conceito de alcance de percepção/detecção hoje — nenhum sistema
decide "o que está perto o suficiente pra ser notado". Esta mecânica cria o conceito E o
consumidor mínimo (não fica inerte): `BehaviorDecisionSystem` passa a considerar NPCs/perigo
dentro do raio de percepção ao decidir fuga/abordagem social, em vez de só reagir ao que já
está no mesmo tile/adjacente.

**Acceptance Criteria**:

1. WHEN um NPC tem um poder ativo com efeito `attribute.perception:<raio-em-tiles>` THEN o
   sistema SHALL considerar outros NPCs/eventos de perigo dentro desse raio (não só
   adjacentes) como candidatos pra decisão de comportamento (fuga, abordagem social).
2. WHEN nenhum poder de percepção está ativo THEN o raio de detecção SHALL continuar o
   comportamento atual (adjacência/mesmo tile) — nenhuma regressão de comportamento pra NPC
   sem o poder.
3. WHEN dois NPCs com raios de percepção diferentes avaliam a mesma cena THEN cada um SHALL
   usar seu próprio raio (percepção é por-portador, nunca global).

**Independent Test**: NPC com `attribute.perception:8` reage a um perigo a 6 tiles de
distância; NPC controle (sem o poder), no mesmo cenário/seed, só reage quando o perigo chega
à adjacência.

---

### P2: Mecânica — Reação / decisão mais rápida

**User Story**: Como quem cria "supervelocidade" (reflexos/pensamento acelerado), quero que o
NPC reavalie e decida mais rápido que o normal, pra ele reagir a mudanças antes de outros
NPCs no mesmo tick.

**Why P2**: Depende de Percepção (P2 anterior) existir — "reagir mais rápido" só é observável
se houver algo a perceber antes dos outros. `BehaviorDecisionSystem` decide uma vez por ciclo
normal hoje; este poder faz o portador reavaliar mais vezes por unidade de tempo simulado.

**Acceptance Criteria**:

1. WHEN um NPC tem um poder ativo com efeito `attribute.reaction-speed:<multiplicador>` THEN
   `BehaviorDecisionSystem` SHALL reavaliar a decisão desse NPC `multiplicador`× mais vezes
   por hora simulada que o normal (ex.: reage a uma ameaça 1 tick depois dela aparecer, em
   vez de esperar o próximo ciclo normal de decisão).
2. WHEN combinado com Percepção (raio maior) THEN o NPC SHALL efetivamente decidir e agir
   antes de um NPC controle no mesmo cenário/seed — mesma ameaça, resposta antes.
3. WHEN o poder cessa THEN a cadência de decisão SHALL voltar ao normal sem resíduo.

**Independent Test**: par controle/tratado, mesma ameaça aparecendo no mesmo tick — NPC com
`attribute.reaction-speed:2` inicia a resposta (fuga/ação) em metade do tempo (em ticks) que
o NPC controle leva pra reagir à mesma ameaça.

---

### P2: Mecânica — Combate NPC-vs-NPC

**User Story**: Como quem cria poderes de dano físico (força sobre-humana em combate,
invulnerabilidade, regeneração de combate), quero que dois NPCs possam de fato entrar em
conflito com resultado determinístico, pra "poder de combate" significar algo real na
simulação — não só um número que nunca é usado.

**Why P2**: Não existe NENHUM mecanismo de conflito/violência NPC-vs-NPC hoje — é o item mais
sensível e o último da sequência de construção combinada (depende de Força/carga já existir
como atributo pra ter o que comparar). Reusa o mesmo padrão de resolução seedada já usado em
poderes (`Resolver.Resolve(difficulty, capacity, variance, rng)`), nunca um sistema de
combate paralelo.

**Acceptance Criteria**:

1. WHEN um poder declara efeito `combat.strike:<magnitude-base>` mirando outro NPC THEN o
   motor SHALL resolver o confronto via `Resolver.Resolve` (capacidade do atacante — incluindo
   `attribute.strength` se ativo — vs. defesa do alvo, mesma família de cálculo já usada em
   `ResolveDeclaredOutcome`), aplicando dano ao alvo (`npc.health` negativo, caminho já
   existente) proporcional ao resultado (sucesso/parcial/falha).
2. WHEN o confronto ocorre THEN o motor SHALL logar um evento causal dedicado (novo
   `WorldEventKind`, ex. `CombatResolved`) com atacante, alvo e resultado — nunca disfarçado
   de um `ExtraordinaryEffectApplied` genérico, pra permitir reação cultural (`ADR-0010`:
   consequência social vem da cultura, não do poder) e histórico causal de verdade.
3. WHEN a mesma seed e os mesmos atributos são usados em duas execuções THEN o resultado do
   confronto SHALL ser byte-idêntico (mesma garantia de determinismo dos demais poderes).
4. WHEN `Extraordinary.Enabled == false` THEN `combat.strike` SHALL ser inatingível (mesma
   garantia zero-state) — combate por poder nunca funciona com o sistema de potência
   desligado, ainda que a mecânica em si (uma vez construída) possa servir de base pra
   combate não-extraordinário no futuro (fora desta spec).

**Independent Test**: par controle/tratado — atacante com `attribute.strength` mais alto
vence com taxa estatisticamente maior contra o mesmo alvo, e a sequência exata de resultados
é reproduzível pra mesma seed (mesmo padrão estatístico já usado no teste de Sorte).

---

### P2: Conceito + Mecânica — Gravidade pessoal (recria voo e velocidade a partir dela)

**User Story**: Como quem cria poderes de voo, controle gravitacional ou "tornar alguém
pesado/leve", quero que gravidade seja um conceito real por NPC (não um flag `CanFly`
especial), pra voo virar UM CASO do conceito de gravidade, não uma exceção só dele.

**Why P2**: Corrige a lacuna apontada pelo usuário — hoje `movement.flight`/
`movement.speed-multiplier` são as duas últimas chaves ainda especiais dentro do próprio
motor que esta fase existe pra eliminar (`ExtraordinaryLocomotion.Resolve` lê o efeito
direto do descritor, não um conceito de domínio). Recriar via gravidade também libera, de
graça, "tornar outro pesado" (hoje impossível — o perfil só existe pro dono do poder, nunca
aplicado a um alvo).

**Acceptance Criteria**:

1. WHEN um poder declara `gravity.self:<multiplicador>` (0 = sem peso, 1 = normal, >1 =
   mais pesado) no PRÓPRIO portador THEN `ExtraordinaryLocomotion.Resolve` SHALL derivar
   `CanFly`/`SpeedMultiplier` a partir desse multiplicador (ex.: `gravity.self` abaixo de um
   limiar declarado habilita voo; acima de 1 reduz o orçamento de células por tick) — mesma
   interface pública (`ExtraordinaryLocomotionProfile`), implementação migrada pra ler
   gravidade em vez das duas chaves antigas.
2. WHEN um poder declara `gravity.target:<multiplicador>` mirando outro NPC THEN o alvo
   SHALL ter seu próprio orçamento de movimento (`ExtraordinaryLocomotion.Advance` do alvo,
   não do portador) reduzido/aumentado pelo multiplicador enquanto a invocação estiver ativa
   — capacidade nova (hoje só o dono de um poder de movimento é afetado, nunca um alvo
   externo).
3. WHEN `movement.flight`/`movement.speed-multiplier` aparecem num descritor pré-existente
   (dado de cenário já salvo) THEN o motor SHALL continuar aceitando-os como sinônimo de
   `gravity.self` (compatibilidade retroativa) — nenhum mundo salvo quebra na migração.
4. WHEN um NPC tem `gravity.target` ativo sobre ele por um poder alheio E `gravity.self` do
   próprio poder simultaneamente THEN o motor SHALL compor os dois multiplicadores
   (multiplicativo) de forma determinística — nunca escolher um e ignorar o outro
   silenciosamente.

**Independent Test**: mesma suíte `ExtraordinaryLocomotionTests` já existente passa 100%
sem alterar asserção nenhuma (migração invisível — AC3); um novo teste com
`gravity.target:3` mirando outro NPC reduz o orçamento de movimento dele mensuravelmente.

---

### P2: Conceito — Temperatura / clima local

**User Story**: Como quem cria "controle do clima local", "sopro congelante" ou "resistência
ambiental", quero que temperatura seja um valor real por célula/região, pra esses poderes
terem o que ler e mudar.

**Why P2**: Confirmado nesta conversa — bioma hoje é só um id colorido, sem semântica de
clima nenhuma. Sem um valor de temperatura por célula, nenhum poder desta categoria tem alvo.

**Acceptance Criteria**:

1. WHEN o mapa é gerado THEN cada célula SHALL ter um valor de temperatura base
   determinístico (derivado do bioma/altitude já existentes — reusa dado já gerado, não
   duplica `MapGenerator`), consultável como qualquer outro campo de célula.
2. WHEN um poder declara `environment.temperature:<região>:<delta>:<duração>` THEN a
   temperatura das células dentro da região declarada SHALL ser ajustada por `delta` pela
   duração declarada, revertendo ao valor base ao expirar.
3. WHEN `CropSystem`/produção agrícola consultam condição de cultivo THEN a temperatura da
   célula SHALL poder influenciar o resultado (gancho mínimo — não precisa reformular a
   fórmula agrícola inteira nesta fase, só garantir que o valor está disponível pra quem
   quiser consumir, hoje ou depois).
4. WHEN nenhum poder de clima está ativo THEN a temperatura SHALL permanecer no valor base
   determinístico — nenhuma variação espontânea/RNG não semeada.

**Independent Test**: célula sob `environment.temperature` alterado reporta o valor ajustado
enquanto ativo e volta ao base determinístico após a duração expirar, mesma seed reproduz o
mesmo valor.

---

### P2: Conceito — Fauna (animal como entidade simulada mínima)

**User Story**: Como quem cria "mestre dos animais" ou "portador da peste", quero que animal
seja uma entidade simulada mínima (posição, espécie, vivo/morto), pra esses poderes terem o
que dominar, comunicar ou infectar.

**Why P2**: Confirmado (recon desta mesma conversa, também registrado em
`project_dynamic_city_growth`/notas de worldgen): fauna não existe como agente simulado hoje,
só `CropSystem` como estoque econômico. Sem isso, toda a categoria "poderes animais" da
lista não tem alvo.

**Acceptance Criteria**:

1. WHEN o mundo é simulado THEN SHALL existir um tipo `Animal` mínimo (id, espécie, posição,
   vivo/morto) — reusando a mesma infraestrutura de posição/movimento já validada pra `Npc`
   (não duplica pathfinding/footprint), sem necessidade de precisar de toda a IA
   comportamental de um NPC (fauna não precisa de personalidade, profissão, família).
2. WHEN um poder declara `fauna.dominate:<raioOuId>` THEN o(s) animal(is) alvo SHALL seguir
   o portador (mover em direção a ele/na direção que ele indicar) enquanto o poder estiver
   manifestado.
3. WHEN um poder declara `fauna.infect-vector:<doença>` (portador da peste) THEN animais
   dentro do raio de contato do portador SHALL ser marcados como vetor — gancho mínimo de
   transmissão (não precisa modelar epidemiologia completa nesta fase, só o vínculo
   causal auditável portador→animal→exposição).
4. WHEN `Extraordinary.Enabled == false` THEN nenhum efeito de fauna SHALL ter caminho de
   execução alcançável — mesma garantia zero-state das demais mecânicas.

**Independent Test**: mundo com N animais simulados, um NPC com `fauna.dominate` ativo faz
os animais no raio se moverem em direção a ele em ticks sucessivos, de forma determinística
pra mesma seed.

---

### P2: Conceito — Memória / cognição privada (consulta ao log de fatos, por NPC, com esquecimento seletivo)

**User Story**: Como quem cria "leitor de mentes", "memória perfeita" ou "roubo de memória",
quero que exista uma noção de "o que ESTE NPC especificamente presenciou/sabe" distinta de
personalidade (traços) e de crença pública (rumor já propagado), pra esses poderes terem o
que ler, copiar ou apagar.

**Why P2**: Confirmado nesta conversa — `memories` no contrato da API é placeholder sem
equivalente no domínio; `beliefs`/rumor (Fase 10) é conhecimento PÚBLICO propagado, não
memória PESSOAL. A rota mais barata (reusa o que já existe, não duplica o log causal
imutável) é derivar memória de uma CONSULTA ao log de `Fact`/`WorldEventKind` já existente,
filtrado pelos fatos em que este NPC foi participante — com uma lista de "esquecidos" por
NPC (metadado pequeno, nunca muta o esqueleto Tier A imutável).

**Acceptance Criteria**:

1. WHEN um poder declara `mind.read-memory` mirando outro NPC THEN o motor SHALL expor os
   `Fact`s em que o alvo foi participante (nascimento, casamento, mortes de parentes,
   eventos extraordinários testemunhados), filtrados pela lista de "esquecidos" desse NPC —
   nunca inventando um fato que não está no log causal real.
2. WHEN um poder declara `mind.erase-memory:<factId>` THEN o `factId` SHALL ser adicionado à
   lista de esquecidos do alvo (metadado por NPC) — o `Fact` em si permanece imutável no
   esqueleto Tier A (nunca é deletado/alterado), só deixa de ser consultável PRA AQUELE NPC.
3. WHEN um poder declara `mind.implant-memory:<factId>` (implantação de memória falsa)
   THEN o alvo SHALL passar a "recordar" um `factId` real do log em que NÃO era participante
   original — implantação é sempre de um fato que aconteceu de verdade em algum lugar do
   mundo (nunca invenção de um evento que nunca ocorreu), preservando integridade causal.
4. WHEN `Extraordinary.Enabled == false` THEN nenhum efeito de memória SHALL ter caminho de
   execução alcançável.

**Independent Test**: NPC com um `Fact` de nascimento próprio no log; `mind.erase-memory`
sobre esse `factId` faz `mind.read-memory` parar de listá-lo; o `Fact` original continua
presente e inalterado no log causal do mundo (consultável por qualquer outro caminho que já
lê `Fact` diretamente).

---

### P2: Mecânica — Ciclo de poder passivo/contínuo

**User Story**: Como quem cria uma aura de liderança, regeneração contínua ou "ninguém pode
mentir perto de mim", quero que um poder `Passive` produza efeito sozinho a cada tick
enquanto manifestado, sem precisar de uma invocação explícita toda vez.

**Why P2**: Auditoria confirmou incerteza real — `PowerDescriptor.Mode="Passive"` já existe
no contrato e `IsAvailable` já aceita `Passive` pra `Origin=Triggered`, mas não há
confirmação de que ALGUM sistema hoje efetivamente reinvoca poderes passivos a cada tick.
Sem isso, toda a categoria de auras/efeitos contínuos (a maior fatia numérica da lista do
usuário) não tem como funcionar de verdade.

**Acceptance Criteria**:

1. WHEN um NPC é portador de um poder `Mode="Passive"` manifestado THEN um sistema dedicado
   (`ExtraordinaryPassiveTickSystem` ou equivalente) SHALL invocá-lo automaticamente a cada
   tick elegível (mesma cadência de `ExtraordinaryStateSystem`, `Hourly`), com
   `Origin=Triggered`, sem exigir chamada manual/autorada.
2. WHEN o poder passivo tem custo declarado THEN o custo SHALL ser cobrado a cada
   reinvocação (não é "grátis por já estar manifestado") — se o portador não tem saldo, a
   reinvocação daquele tick falha silenciosamente (log causal, sem exceção), sem revogar o
   poder.
3. WHEN a condição de manifestação deixa de valer THEN a reinvocação automática SHALL parar
   no mesmo tick — nenhum efeito passivo sobrevive à queda de manifestação.
4. WHEN `Extraordinary.Enabled == false` THEN o sistema de tick passivo SHALL não fazer
   nenhum trabalho (mesma garantia zero-state).

**Independent Test**: NPC com poder passivo de área (`area:radius` + `mind.alter-trait`)
manifestado por N ticks aplica o efeito a cada tick sem invocação manual; ao custo faltar,
a reinvocação daquele tick é pulada sem revogar o poder; ao desmanifestar, para
imediatamente.

---

### P2: Mecânica — Vulnerabilidade/resistência mecânica (tipo de efeito casado contra fraqueza declarada)

**User Story**: Como quem cria um poder com fraqueza específica ("kryptonita", "prata", "luz
solar"), quero que essa fraqueza mude o resultado mecânico de verdade quando alguém a explora,
não só apareça como texto narrativo no log.

**Why P2**: Confirmado — `intrinsicVulnerabilities`/tags de modo de falha são hoje só
narrativas (não afetam cálculo, exceto o único caso já mecânico de `carrier.health:` em modo
de falha). Sem um casamento mecânico tipo-a-tipo, toda a categoria de "fraqueza específica"
da lista (Superman/kryptonita, lobisomem/prata, vampiro/luz solar, etc.) é só flavor text.

**Acceptance Criteria**:

1. WHEN um efeito/custo declara um `tipo` (ex.: `combat.strike:sunlight:<magnitude>`) E o
   alvo tem esse `tipo` na lista de `intrinsicVulnerabilities` THEN a magnitude aplicada
   SHALL ser multiplicada por um fator declarado no cenário (ex.: 2×) — mecânico, não só
   logado.
2. WHEN o `tipo` do efeito não casa com nenhuma vulnerabilidade do alvo THEN a magnitude
   SHALL ser aplicada normalmente (sem bônus nem penalidade) — vulnerabilidade é específica,
   nunca genérica.
3. WHEN um alvo tem `intrinsicVulnerabilities` mas o efeito não declara `tipo` nenhum THEN
   o comportamento SHALL ser idêntico ao de hoje (nenhuma regressão pra poderes sem tipo
   declarado).

**Independent Test**: mesmo `combat.strike` com e sem `tipo` casando a vulnerabilidade do
alvo produz dano diferente (multiplicado) só no caso de casamento — par controle/tratado
confirma o fator exato declarado.

---

### P2: Mecânica — Habilidade (Skill) como efeito de poder

**User Story**: Como quem cria "mimetismo", "prodígio" ou "roubo de habilidade", quero ler,
copiar ou acelerar o `Skill` de um NPC através de um poder.

**Why P2**: `Npc.Skills`/`SkillSet` já existe como dado real (aprendizado via
`SkillPracticeSystem`) — só falta o efeito de poder que lê/copia/acelera esse valor. Reusa a
primitiva de transferência (P1) pro eixo `skill.<id>`, e o registro de mecânicas pro
multiplicador de aprendizado.

**Acceptance Criteria**:

1. WHEN um poder declara `skill.copy:<skillId>` mirando outro NPC THEN o motor SHALL copiar
   o valor de `Skill` daquele id do alvo pro portador (nunca inventar um id de skill que o
   alvo não tem) — cópia, não transferência (alvo mantém o valor, salvo declaração explícita
   de "roubo" via a primitiva de transferência já existente).
2. WHEN um poder declara `skill.learn-rate:<multiplicador>` THEN a progressão de `Skill` do
   portador em `SkillPracticeSystem` SHALL escalar por esse multiplicador enquanto
   manifestado — mesmo padrão de "segundo multiplicador" já usado em força/coleta.
3. WHEN o poder cessa THEN `skill.learn-rate` SHALL parar de se aplicar imediatamente (sem
   resíduo); `skill.copy` já aplicado permanece (é cópia pontual, não contínua).

**Independent Test**: NPC com `skill.learn-rate:5` progride 5× mais rápido na mesma prática
que um NPC controle; `skill.copy:<id>` copia o valor exato do alvo no momento da invocação.

---

### P2: Mecânica — Fertilidade modificável

**User Story**: Como quem cria "fertilidade sobrenatural" ou "maldição de infertilidade",
quero que a taxa de fertilidade de um NPC (própria ou de outro) seja um valor que um poder
pode multiplicar.

**Why P2**: `NatalitySystem` já tem uma taxa de fertilidade/concepção — falta o gancho de
poder lendo/multiplicando esse valor, mesmo padrão já usado em senescência/skill/força.

**Acceptance Criteria**:

1. WHEN um poder declara `attribute.fertility:<multiplicador>` no próprio portador ou num
   alvo THEN a taxa de concepção usada por `NatalitySystem` pra aquele NPC SHALL escalar por
   esse multiplicador enquanto manifestado.
2. WHEN `attribute.fertility:0` (maldição de infertilidade) THEN `NatalitySystem` SHALL
   nunca conceber com aquele NPC como parte enquanto o poder estiver ativo.
3. WHEN o poder cessa THEN a taxa SHALL voltar ao valor base sem resíduo.

**Independent Test**: par controle/tratado — NPC com `attribute.fertility:0` nunca gera
concepção em N anos simulados; NPC controle concebe na taxa normal do `NatalitySystem`.

---

### P2: Conceito — Flora (par de Fauna, plantas como entidade mínima)

**User Story**: Como quem cria "comunicação com plantas" ou "crescimento instantâneo",
quero que planta seja uma entidade simulada mínima (não só estoque agrícola), pra esses
poderes terem o que afetar.

**Why P2**: Mesma lacuna já identificada pra Fauna — `CropSystem` é só estoque econômico,
nunca organismo individual. Sem isso, a categoria "poderes ecológicos/plantas" não tem alvo,
mesmo raciocínio já aplicado à Fauna (`PWR-77..79`).

**Acceptance Criteria**:

1. WHEN o mundo é simulado THEN SHALL existir um tipo `Plant` mínimo (id, espécie, posição,
   estágio de crescimento) — reusa a mesma infraestrutura espacial já validada, não duplica
   `CropSystem` (que continua sendo o estoque econômico; `Plant` é a entidade física
   individual que um poder pode mirar).
2. WHEN um poder declara `flora.growth-rate:<multiplicador>` numa área (reusa o seletor de
   área, primitiva P1) THEN as `Plant`s dentro da área SHALL avançar de estágio de
   crescimento proporcionalmente mais rápido.
3. WHEN `Extraordinary.Enabled == false` THEN nenhum efeito de flora SHALL ter caminho de
   execução alcançável.

**Independent Test**: área com `flora.growth-rate:5` ativo tem plantas avançando de estágio
5× mais rápido que uma área controle, determinístico pra mesma seed.

---

### P2: Mecânica — Instanciação de NPC via poder (clone, divisão, reencarnação, ressurreição)

**User Story**: Como quem cria "clone", "divisão" ou "reencarnação", quero que um poder possa
criar um NPC novo no mundo (ou reativar um morto), pra essas categorias inteiras da lista
terem uma implementação real, não só narrativa.

**Why P2**: Nenhum efeito hoje cria uma entidade `Npc` nova — só `NatalitySystem` (nascimento
normal) instancia NPC. É o gap de maior risco desta fase (mexe em identidade/população), por
isso vem depois das mecânicas mais simples na ordem de construção.

**Acceptance Criteria**:

1. WHEN um poder declara `npc.clone` THEN o motor SHALL instanciar um novo `Npc` com
   identidade própria (novo `NpcId`, sem parentesco/`Household` herdado automaticamente —
   decisão de cenário, não do motor), reusando a mesma personalidade/aparência do portador
   no momento da clonagem (cópia, não referência compartilhada).
2. WHEN um poder declara `npc.split-on-death` THEN, ao processar `NpcDeath.Apply` para o
   portador, o motor SHALL instanciar N novos NPCs (declarado no descritor) antes de marcar
   o portador como morto — evento causal dedicado registra a origem (qual morte gerou quais
   clones).
3. WHEN um poder declara `npc.reincarnate` THEN, ao morrer, o portador SHALL ter uma fração
   declarada de `Skills`/traços de `Personality` transferida pro próximo NPC nascido no
   mundo via `NatalitySystem` (nunca cria um nascimento fora do fluxo normal — só influencia
   um nascimento que já ia acontecer).
4. WHEN qualquer uma dessas mutações ocorre THEN SHALL ser logada com `WorldEventKind` novo
   e dedicado (nunca disfarçada de `ExtraordinaryEffectApplied` genérico) — auditável como
   `Birth`/`Death` já são.

**Independent Test**: `npc.clone` produz um segundo `Npc` com personalidade idêntica e
`NpcId` distinto no mesmo tick; `npc.split-on-death` produz exatamente N novos NPCs no
momento da morte do portador, nunca antes nem depois.

---

### P2: Mecânica — Identidade/controle prolongado (possessão contínua, troca de corpo, metamorfismo)

**User Story**: Como quem cria "possessão", "troca de corpo" ou "metamorfo", quero controlar
as decisões de outro NPC por um período (não só uma ação), trocar qual corpo minha
identidade ocupa, ou assumir a aparência observável de outra pessoa.

**Why P2**: `npc.force-action` (já implementado) só força UMA ação pontual — possessão
precisa de controle CONTÍNUO sobre múltiplas decisões pelo tempo da manifestação. Troca de
corpo/metamorfismo mexem com "qual identidade está associada a qual corpo/aparência", que
não existe como conceito hoje (aparência é só cosmética: escala/tint/trilha).

**Acceptance Criteria**:

1. WHEN um poder declara `control.possess` mirando outro NPC THEN, enquanto manifestado,
   `BehaviorDecisionSystem` SHALL delegar as decisões daquele alvo pro portador (ou pra
   regras declaradas pelo portador) — ações continuam registradas socialmente como sendo do
   alvo possuído (nunca do portador), preservando a integridade causal já exigida por
   `ADR-0010`.
2. WHEN um poder declara `control.body-swap` entre portador e alvo THEN `Personality` e
   estado observável de identidade (não a posição física/`Household`) SHALL trocar entre os
   dois NPCs, pelo tempo declarado — reversível ao cessar.
3. WHEN um poder declara `appearance.impersonate:<npcId>` THEN o portador SHALL ser
   observável (rótulo/identidade visual) como o NPC alvo enquanto manifestado — sem alterar
   `NpcId`/identidade real (metamorfismo é cosmético/social, nunca troca de identidade de
   fato — isso é `body-swap`).
4. WHEN a manifestação cessa (qualquer uma das três) THEN o estado original SHALL ser
   restaurado sem resíduo.

**Independent Test**: NPC possuído executa a sequência de ações declarada pelo portador
enquanto o log causal continua atribuindo essas ações ao possuído, não ao portador; ao
cessar, o possuído volta a decidir sozinho via `BehaviorDecisionSystem` normal.

---

### P2: Mecânica — Vínculo/pacto duradouro entre duas partes

**User Story**: Como quem cria "juramento mágico" ou "vínculo de destino", quero que dois
NPCs fiquem ligados por um acordo com consequência real se quebrado, ou passem a
compartilhar sorte/dano/saúde/longevidade continuamente — diferente de uma transferência
pontual.

**Why P2**: A primitiva de transferência (P1) é instantânea (uma vez, na invocação). Vínculo
é uma relação PERSISTENTE entre duas partes, reavaliada a cada tick enquanto durar — precisa
do ciclo de poder passivo (já adicionado acima) como base, aplicado a um PAR de NPCs em vez
de um raio.

**Acceptance Criteria**:

1. WHEN um poder declara `bond.share:<atributo>` entre duas partes THEN, a cada tick
   elegível (reusa o ciclo passivo), o motor SHALL igualar (ou aplicar a proporção
   declarada) o atributo declarado entre as duas partes — ex.: dano num afeta o outro na
   proporção declarada.
2. WHEN um poder declara `bond.oath:<consequência>` e uma das partes viola a condição
   declarada (checada via `ManifestationCondition` já genérico) THEN a consequência
   declarada SHALL ser aplicada automaticamente à parte que violou — sem intervenção manual.
3. WHEN qualquer uma das partes morre THEN o vínculo SHALL ser desfeito automaticamente
   (nenhum vínculo sobrevive a um participante inexistente).

**Independent Test**: par vinculado por `bond.share:health` — dano aplicado a um se reflete
no outro na proporção declarada, a cada tick, até um dos dois morrer (vínculo desfeito).

---

### P2: Conceito — Estado de alma/fantasma pós-morte

**User Story**: Como quem cria "ver fantasmas", "conversar com mortos" ou "criar fantasmas",
quero que um NPC morto possa continuar existindo num estado limitado (não vivo, mas
consultável/interagível), pra a categoria "poderes relacionados à morte" ter alvo.

**Why P2**: Hoje `Npc.IsAlive=false` é terminal — não existe um estado intermediário
consultável. Menor extensão possível: um NPC morto com este poder ativo (declarado em vida,
ou concedido por outro poder no momento da morte) permanece consultável num modo restrito.

**Acceptance Criteria**:

1. WHEN um NPC portador de `soul.persist-as-ghost` morre THEN o `Npc` SHALL permanecer
   consultável (posição última conhecida, `Personality`, `Skills`) com um estado
   `IsGhost=true` — nunca reaparece como vivo, nunca participa de sistemas que exigem
   `IsAlive` (produção, movimento normal, necessidades).
2. WHEN um poder declara `mind.commune` mirando um NPC com `IsGhost=true` THEN o motor SHALL
   permitir a mesma leitura de `mind.read-memory` (mecânica já existente) sobre ele.
3. WHEN nenhum poder de fantasma foi concedido antes da morte THEN o NPC morto SHALL
   permanecer terminal como hoje (nenhuma regressão — fantasma é opt-in, nunca padrão).

**Independent Test**: NPC com `soul.persist-as-ghost` continua consultável (nome,
personalidade) após `IsAlive=false`; um NPC controle (sem o poder) some da consulta normal
após morrer, como hoje.

---

### P2: Conceito — Espaço dimensional (bolso/portal)

**User Story**: Como quem cria "portal" ou "bolso dimensional", quero guardar objetos/NPCs
fora do mapa normal, ou ligar duas células distantes pra travessia instantânea nos dois
sentidos.

**Why P2**: Não existe conceito de espaço fora do grid normal do mapa nem ligação
bidirecional entre duas células — teleporte (já implementado) é ponto-a-ponto único, não uma
ligação persistente reutilizável.

**Acceptance Criteria**:

1. WHEN um poder declara `dimension.pocket-store` sobre um recurso/objeto do portador THEN
   o item SHALL sair do estoque/mapa normal e ficar associado ao portador num espaço não
   endereçável por posição (consultável só via o próprio poder), sem contar como perdido
   pra conservação econômica (é guardado, não destruído — cai fora dos testes de
   conservação, mas nunca "some" sem rastro causal).
2. WHEN um poder declara `dimension.portal:<célulaA>:<célulaB>` THEN qualquer NPC que entrar
   em `célulaA` SHALL ser teleportado (reusa a mecânica de teleporte já existente) pra
   `célulaB`, e vice-versa, enquanto o portal existir.
3. WHEN o poder que criou o portal cessa THEN o portal SHALL deixar de funcionar
   imediatamente (célula volta a ser passagem normal).

**Independent Test**: NPC entrando em `célulaA` com portal ativo aparece em `célulaB` no
mesmo tick; ao desativar o poder, a mesma célula não teleporta mais ninguém.

---

### P2: Mecânica — Precognição probabilística (sem viagem no tempo)

**User Story**: Como quem cria "profeta" ou "precognição curta", quero ver resultados
prováveis de uma decisão ou evento futuro sem alterar o mundo real, pra decidir com essa
informação — sem que isso seja viagem no tempo de verdade.

**Why P2**: Assumido como fora da exclusão de "tempo forte" — precognição aqui é LEITURA
probabilística (não rebobina, não pausa, não muta o mundo), então cabe nesta fase; **decisão
registrada como suposição** (usuário pediu "adicione tudo" sem responder esse ponto
especificamente — ver Assumptions).

**Acceptance Criteria**:

1. WHEN um poder declara `foresight.preview:<evento>` THEN o motor SHALL rodar a mesma
   resolução determinística que seria usada se o evento ocorresse agora (reusa
   `Resolver.Resolve`/sistemas existentes) NUM ESCOPO DE LEITURA — nenhuma mutação real do
   `WorldState` acontece, o resultado é só reportado (log causal de "previsão", nunca um
   `Fact` como se tivesse ocorrido de verdade).
2. WHEN a mesma seed e estado são usados THEN a prévia SHALL ser idêntica ao resultado real
   se o evento fosse de fato disparado no mesmo tick (a previsão não é um jogo de adivinhação
   solto — é o mesmo cálculo, só não commitado).
3. WHEN o evento previsto de fato ocorre depois (em outro tick) THEN o resultado real PODE
   divergir da prévia (o mundo mudou entre a previsão e o evento) — a previsão nunca é uma
   garantia retroativamente forçada.

**Independent Test**: `foresight.preview` sobre uma resolução simulada reporta exatamente o
mesmo resultado que `Resolver.Resolve` produziria naquele tick/seed, sem qualquer `Fact`
novo no log causal do mundo.

---

### P3: Power Evolution — árvore de progressão + mistura genética (documentado, não implementado)

**User Story**: Como quem desenha o sistema de poderes a longo prazo, quero que um poder
comece "fraco" (ex.: `kinetic_affinity=0.2`) e evolua em estágios ao longo da vida do NPC
(infância→adolescência→adulto→experiência extrema), e que filhos de portadores de poderes
diferentes possam herdar uma combinação nova (não uma cópia de um dos pais), pra criar
emergência genealógica de verdade.

**Why P3**: Sistema de progressão + herança é ortogonal ao motor de efeito/custo desta spec
— depende dele existir primeiro (evolução muda QUAIS efeitos um poder aplica ao longo do
tempo; combinação genética decide QUAL novo `PowerDescriptor` nasce). Não é implementado
nesta fase; fica registrado aqui pra não se perder e pra Design já deixar o modelo de dados
aberto o suficiente (não fechar `PowerDescriptor` de um jeito que impeça isso depois).

**Acceptance Criteria** (documentadas, não implementadas nesta fase):

1. WHEN um `PowerDescriptor` declara estágios de evolução (idade/experiência → efeitos
   diferentes) THEN o sistema SHALL trocar o conjunto de efeitos ativos do portador
   conforme o estágio corrente — feature futura, sem AC de teste nesta fase.
2. WHEN dois portadores de poderes diferentes têm um filho THEN o sistema SHALL poder gerar
   um `PowerDescriptor` novo combinando eixos dos dois poderes originais (ex.: piroquinese +
   telepatia → combustão emocional) — feature futura, sem AC de teste nesta fase.

**Independent Test**: N/A — fica pra spec própria (16.2 ou posterior) quando priorizado.

---

## Edge Cases

- WHEN um poder declara mais de uma mecânica no mesmo token (ex.: efeito ambíguo entre
  `npc.` e `transfer.`) THEN o motor SHALL escolher o prefixo mais específico
  registrado e falhar explicitamente se dois registros colidirem no mesmo prefixo
  (erro de configuração de cenário, não de invocação).
- WHEN uma mecânica nova precisa de estado persistente no NPC (ex.: valor de personalidade
  pré-alteração, PWR mente) THEN esse estado SHALL viver no
  `ExtraordinaryCarrierState`/estrutura equivalente já associada ao poder — nunca um campo
  solto novo direto em `Npc` sem necessidade (menor blast radius em construtor).
- WHEN `Extraordinary.Enabled == false` THEN nenhuma mecânica nova (P2) SHALL ter caminho de
  execução alcançável — mesma garantia zero-state da Fase 16 original, testada de novo pra
  cada mecânica nova.

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| PWR-01 | P1: Registro de mecânicas | Design | Pending |
| PWR-02 | P1: Registro de mecânicas (sem editar loop central) | Design | Pending |
| PWR-03 | P1: Registro de mecânicas (falha segura) | Design | Pending |
| PWR-04 | P1: Registro de mecânicas (determinismo) | Design | Pending |
| PWR-05 | P1: Registro de mecânicas (atomicidade preservada) | Design | Pending |
| PWR-06 | P1: Seletor de área/região — resolve conjunto de alvos determinístico | Design | Pending |
| PWR-07 | P1: Seletor de área — raio recalculado a cada invocação | Design | Pending |
| PWR-08 | P1: Seletor de área — zero alvos não é erro | Design | Pending |
| PWR-09 | P1: Seletor de área — custo cobrado uma vez, não por alvo | Design | Pending |
| PWR-10 | P1: Transferência (dois alvos) | Design | Pending |
| PWR-11 | P1: Transferência (saldo insuficiente falha tudo) | Design | Pending |
| PWR-12 | P1: Transferência (clamp no teto) | Design | Pending |
| PWR-13 | P1: Transferência (ordem determinística custo→transfer) | Design | Pending |
| PWR-20 | P2: Senescência consumida pela mortalidade | Design | Pending |
| PWR-21 | P2: Senescência zero nunca agenda morte por idade | Design | Pending |
| PWR-22 | P2: Senescência reage à manifestação sem retroagir | Design | Pending |
| PWR-23 | P2: Senescência agregada por mínimo entre poderes | Design | Pending |
| PWR-24 | P2: Sorte — bônus de capacidade | Design | Pending |
| PWR-25 | P2: Sorte — maldição direcionada | Design | Pending |
| PWR-26 | P2: Sorte — determinismo por seed | Design | Pending |
| PWR-27 | P2: Sorte — clamp em zero | Design | Pending |
| PWR-28 | P2: Mente — leitura sem inventar dado novo | Design | Pending |
| PWR-29 | P2: Mente — alteração de traço via comando existente | Design | Pending |
| PWR-30 | P2: Mente — reversão ao cessar manifestação | Design | Pending |
| PWR-31 | P2: Mente — resolução determinística de conflito | Design | Pending |
| PWR-32 | P2: Transferência de anos de vida — reagendamento | Design | Pending |
| PWR-33 | P2: Transferência de anos de vida — saldo insuficiente | Design | Pending |
| PWR-34 | P2: Transferência de anos de vida — morte já consumida | Design | Pending |
| PWR-35 | P2: Transmutação — débito+crédito auditados (Minted/Destroyed) | Design | Pending |
| PWR-36 | P2: Transmutação — saldo insuficiente falha tudo | Design | Pending |
| PWR-37 | P2: Transmutação — conservação explicada 100% pelos eventos | Design | Pending |
| PWR-38 | P2: Transmutação — taxa é decisão de cenário, não gate | Design | Pending |
| PWR-50 | P2: Limite de carga — teto real de capacidade | Design | Pending |
| PWR-51 | P2: Limite de carga — multiplicador de força | Design | Pending |
| PWR-52 | P2: Limite de carga — volta ao base sem manifestação | Design | Pending |
| PWR-53 | P2: Coleta/construção — segundo multiplicador (força × skill) | Execute | Done |
| PWR-54 | P2: Coleta/construção — reverte sem resíduo | Execute | Done |
| PWR-55 | P2: Coleta/construção — respeita teto já validado | Execute | Done |
| PWR-56 | P2: Percepção — raio de detecção considerado na decisão | Execute | Done |
| PWR-57 | P2: Percepção — sem regressão pra NPC sem o poder | Execute | Done |
| PWR-58 | P2: Percepção — raio por-portador, nunca global | Execute | Done |
| PWR-59 | P2: Reação — reavaliação de decisão multiplicada | Execute | Done |
| PWR-60 | P2: Reação — combinação com percepção reage antes | Execute | Done |
| PWR-61 | P2: Reação — reverte sem resíduo | Execute | Done |
| PWR-62 | P2: Combate — resolução via Resolver seedado + dano aplicado | Execute | Done |
| PWR-63 | P2: Combate — evento causal dedicado (reação cultural) | Execute | Done |
| PWR-64 | P2: Combate — determinismo por seed | Execute | Done |
| PWR-65 | P2: Combate — inatingível com Extraordinary desligado | Execute | Done |
| PWR-70 | P2: Gravidade — voo/velocidade derivados de `gravity.self` | Execute | Done |
| PWR-71 | P2: Gravidade — `gravity.target` afeta orçamento de movimento do alvo | Execute | Done |
| PWR-72 | P2: Gravidade — compatibilidade retroativa (`movement.*` como sinônimo) | Execute | Done |
| PWR-73 | P2: Gravidade — composição determinística self+target | Execute | Done |
| PWR-74 | P2: Temperatura — valor base por célula | Execute | Done |
| PWR-75 | P2: Temperatura — poder ajusta região por duração declarada | Execute | Done |
| PWR-76 | P2: Temperatura — gancho de consumo (agricultura) + sem RNG espontâneo | Execute | Done |
| PWR-77 | P2: Fauna — entidade `Animal` mínima (posição/espécie/vivo) | Execute | Done |
| PWR-78 | P2: Fauna — domínio/vetor de infecção | Execute | Done |
| PWR-79 | P2: Fauna — inatingível com Extraordinary desligado | Execute | Done |
| PWR-80 | P2: Memória — leitura filtrada por esquecidos, do log real | Execute | Done |
| PWR-81 | P2: Memória — apagar é metadado, nunca muta o Fact imutável | Execute | Done |
| PWR-82 | P2: Memória — implantar é sempre um Fact real, nunca invenção | Execute | Done |
| PWR-83 | P2: Memória — inatingível com Extraordinary desligado | Execute | Done |
| PWR-90 | P2: Passivo — reinvocação automática por tick | Execute | Done |
| PWR-91 | P2: Passivo — custo cobrado a cada reinvocação, falha silenciosa | Execute | Done |
| PWR-92 | P2: Passivo — para no mesmo tick em que a manifestação cai | Execute | Done |
| PWR-93 | P2: Vulnerabilidade — tipo casado multiplica magnitude | Execute | Done |
| PWR-94 | P2: Vulnerabilidade — sem casamento aplica normal | Execute | Done |
| PWR-95 | P2: Vulnerabilidade — sem tipo declarado é idêntico a hoje | Execute | Done |
| PWR-96 | P2: Skill — copiar valor de outro NPC | Design | Pending |
| PWR-97 | P2: Skill — multiplicador de taxa de aprendizado | Design | Pending |
| PWR-98 | P2: Skill — reverte multiplicador sem resíduo | Design | Pending |
| PWR-99 | P2: Fertilidade — multiplicador de taxa de concepção | Design | Pending |
| PWR-100 | P2: Fertilidade — zero nunca concebe enquanto ativo | Design | Pending |
| PWR-101 | P2: Flora — entidade `Plant` mínima | Execute | Done |
| PWR-102 | P2: Flora — taxa de crescimento por área | Execute | Done |
| PWR-103 | P2: Flora — inatingível com Extraordinary desligado | Execute | Done |
| PWR-104 | P2: Instanciação — `npc.clone` cria identidade nova | Execute | Done |
| PWR-105 | P2: Instanciação — `npc.split-on-death` gera N ao morrer | Execute | Done |
| PWR-106 | P2: Instanciação — `npc.reincarnate` influencia próximo nascimento | Execute | Done |
| PWR-107 | P2: Instanciação — evento causal dedicado, sempre auditável | Execute | Done |
| PWR-108 | P2: Identidade — possessão contínua preserva atribuição causal | Execute | Done |
| PWR-109 | P2: Identidade — troca de corpo (personalidade) reversível | Execute | Done |
| PWR-110 | P2: Identidade — metamorfismo é cosmético, nunca troca `NpcId` | Execute | Done |
| PWR-111 | P2: Identidade — restaura estado original ao cessar | Execute | Done |
| PWR-112 | P2: Vínculo — `bond.share` iguala atributo a cada tick | Execute | Done |
| PWR-113 | P2: Vínculo — `bond.oath` aplica consequência automática na violação | Execute | Done |
| PWR-114 | P2: Vínculo — desfeito automaticamente se uma parte morre | Execute | Done |
| PWR-115 | P2: Alma — `IsGhost` consultável após a morte, opt-in | Execute | Done |
| PWR-116 | P2: Alma — sem o poder, comportamento terminal como hoje | Execute | Done |
| PWR-117 | P2: Dimensional — bolso guarda sem contar como perda econômica | Execute | Done |
| PWR-118 | P2: Dimensional — portal liga duas células nos dois sentidos | Execute | Done |
| PWR-119 | P2: Dimensional — portal desativa com o poder | Execute | Done |
| PWR-120 | P2: Precognição — prévia reusa resolução real, sem mutar o mundo | Execute | Done |
| PWR-121 | P2: Precognição — determinística pra mesma seed/estado | Execute | Done |
| PWR-122 | P2: Precognição — resultado real pode divergir depois (não é garantia) | Execute | Done |
| PWR-40 | P3: Power Evolution (documentado) | — | Deferred |
| PWR-41 | P3: Mistura genética de poderes (documentado) | — | Deferred |

**Coverage:** 97 total, 95 mapeados pra Design (P1+P2), 2 deferidos por decisão explícita (P3).
**Única exclusão real da fase:** poderes de tempo/viagem no tempo — endereçados na Fase 18
(Timelines), decisão do usuário, 2026-08-24.

---

## Success Criteria

- [ ] **Qualquer poder de movimento, dano, interação com objeto, atributo físico, mental,
      social ou econômico é descritível só como dado de cenário** (`PowerDescriptor` +
      mecânicas registradas), sem exceção — única categoria fora desta fase é tempo/viagem no
      tempo (Fase 18).
- [ ] Suíte `Extraordinary*Tests` inteira (existente + nova) verde, incluindo mutação
      comportamental (mesma disciplina de `validation.md` da Fase 16 original).
- [ ] Uma amostra de ~15-20 poderes da lista do usuário, cobrindo cada categoria principal
      (imortalidade, sorte, mente/memória, transferência, teleporte/velocidade/força/
      percepção/combate, gravidade, temperatura/clima, fauna), descrita só como dado de
      cenário (`PowerDescriptor`), sem nenhuma linha de C# nova além das mecânicas/conceitos
      P1/P2 já registrados.
- [ ] Voo e supervelocidade — já funcionais desde a Fase 16 original — passam a ser CASOS do
      conceito de gravidade/cinética (`gravity.self`), não mais chaves especiais soltas no
      motor, com a suíte de locomoção existente inalterada (migração invisível).
- [ ] As 12 lacunas adicionais identificadas numa auditoria própria do agente contra os ~300
      poderes de referência (seletor de área, ciclo passivo, vulnerabilidade mecânica,
      skill, fertilidade, flora, instanciação de NPC, identidade/possessão, vínculo/pacto,
      alma/fantasma, dimensional/portal, precognição) — cada uma com AC testável, não só
      documentada.
- [ ] `dotnet test` completo (não só o filtro de Extraordinary) sem regressão.
- [x] Decisões em aberto (tempo forte; transmutação vs. conservação; Força/Percepção/Combate
      dentro ou fora da fase) resolvidas com o usuário em 2026-08-24 — ver Assumptions.
