# Fase 16.2 — Power Evolution (Progressão + Mistura Genética de Poderes)

## Problem Statement

A Fase 16.1 (`phase-16-1-power-engine`) generaliza COMO um poder se pluga na simulação
(registro de mecânicas), mas deliberadamente deixou de fora, como P3 documentado (`PWR-40..41`),
o sistema de **Power Evolution**: um poder que começa fraco e evolui em estágios ao longo da
vida do portador, e uma regra de **mistura genética** onde o filho de dois portadores de
poderes diferentes pode herdar uma combinação nova, não uma cópia de um dos pais. Esta spec
retoma esse P3 e o transforma em feature própria, agora que o motor de mecânicas (pré-requisito
declarado) está desenhado.

## Goals

- [ ] Um `PowerDescriptor` pode declarar estágios de evolução — o conjunto de efeitos ativos do
      portador muda conforme idade/experiência avança, sem precisar de um poder novo por
      estágio.
- [ ] Dois portadores de poderes diferentes, ao terem um filho, podem gerar um `PowerDescriptor`
      novo combinando eixos dos dois poderes originais — nunca uma cópia determinística de um
      dos pais nem um resultado fora dos eixos já declarados por eles.
- [ ] Toda evolução/combinação continua determinística por seed (mesma garantia da Fase 16/16.1)
      e nunca introduz um efeito que a mecânica registrada (16.1) não sabe interpretar.

## Out of Scope

| Item | Razão |
| --- | --- |
| Novas mecânicas de efeito/custo | Já é escopo fechado da `phase-16-1-power-engine` — esta fase consome o registro existente, nunca adiciona namespace novo de token. |
| Poderes de tempo/viagem no tempo | Mesma exclusão da Fase 16.1 — fica pra Fase 18 (Timelines). |
| Evolução/mistura fora do contexto de poder (ex.: evolução de espécie/genética geral de NPC sem poder envolvido) | Fora do problema declarado — esta fase é só sobre `PowerDescriptor`. |

## Assumptions & Open Questions

| Assumption / decisão | Escolha proposta | Racional | Confirmado? |
| --- | --- | --- | --- |
| Gatilho de estágio | Idade biológica do portador (mesma fonte já usada por `MortalityPlanner`) + um contador de "uso" opcional (nº de invocações bem-sucedidas) como segundo eixo | Reusa dado que já existe (idade); contador de uso é o mínimo novo que cobre "poder que evolui com prática", sem inventar um sistema de XP genérico | **Não confirmado — proposta a validar com o usuário antes do Design** |
| Modelo de mistura genética | Função determinística (seed do mundo + `NpcId` do filho) que escolhe, por eixo do `PowerDescriptor` (fonte/efeito/custo/condição/aquisição), qual dos dois pais contribui — nunca média numérica de eixos incompatíveis (ex.: não faz sentido "misturar" `gravity.self` com `mind.read` numérica, mas dá pra herdar UM dos dois efeitos por eixo, ou compor os dois numa lista se ambos forem do mesmo tipo de efeito) | Preserva determinismo; evita gerar um `PowerDescriptor` semanticamente inválido (ex.: efeito que nenhuma mecânica registrada sabe interpretar) | **Não confirmado — é a decisão de maior risco desta fase, deve ser confirmada antes de Design** |
| Poder resultante da mistura pode ser mais forte que os dois pais somados? | Não — resultante é sempre uma RECOMBINAÇÃO dos eixos dos pais (nunca soma/potencialização); força bruta do resultado é limitada pelos valores já declarados nos poderes dos pais | Evita inflação de poder geração-a-geração sem limite (mesmo espírito de conservação já exigido em toda a Fase 16) | **Não confirmado** |
| Escopo do MVP | Motor de progressão+mistura genérico, aplicado a uma amostra pequena (2-3 poderes de exemplo com estágios, 1 par de poderes combinando), não uma árvore de evolução completa pra cada um dos ~300 poderes de referência | Mesmo padrão de escopo já aprovado na Fase 16.1 (motor genérico + amostra, não caso-a-caso) | Proposto por analogia à 16.1 — **confirmar com o usuário** |

**Nenhuma destas 4 decisões foi confirmada ainda** — a spec está deliberadamente parada aqui
pra não presumir a peça de maior risco (mistura genética) sem validação. Design não deve
começar antes dessas respostas.

## User Stories

### P1: Estágios de evolução por idade/uso

**User Story**: Como quem desenha um poder que começa fraco ("afinidade cinética" baixa) e
fica mais forte com o tempo/prática, quero declarar estágios no `PowerDescriptor` que trocam
o conjunto de efeitos ativos do portador conforme ele envelhece/usa o poder, sem precisar
conceder um poder novo a cada estágio.

**Why P1**: É a metade mais simples/menos arriscada desta fase — não envolve dois portadores
nem geração de descritor novo, só reavaliação do descritor já existente.

**Acceptance Criteria**:

1. WHEN um `PowerDescriptor` declara uma lista de estágios (cada um com um limiar de
   idade/contador-de-uso e um conjunto de efeitos) THEN o motor SHALL aplicar, a cada
   reavaliação de manifestação, o conjunto de efeitos do estágio mais alto cujo limiar o
   portador já atingiu — nunca um estágio futuro, nunca mais de um estágio simultâneo.
2. WHEN o portador ainda não atingiu o limiar do primeiro estágio declarado THEN o poder
   SHALL permanecer com o conjunto de efeitos do estágio 0 (ou inativo, se nenhum estágio 0
   for declarado) — nunca falhar por "estágio não encontrado".
3. WHEN o contador de uso é o gatilho declarado THEN ele SHALL incrementar exatamente uma vez
   por invocação bem-sucedida daquele poder (nunca por invocação falha) — reusa o mesmo
   log causal já usado por `UseFailed`/`EffectApplied`.
4. WHEN a mesma seed e o mesmo histórico de invocações são usados em duas execuções THEN o
   estágio corrente SHALL ser byte-idêntico entre elas.

**Independent Test**: poder de exemplo com 3 estágios por idade (`0`, `18`, `40` anos) — um
NPC criado com 10 anos manifesta o estágio 0; ao atingir 18 anos (simulado), o motor troca pro
conjunto de efeitos do estágio seguinte, sem intervenção manual.

---

### P1: Mistura genética entre dois portadores

**User Story**: Como quem quer que a genealogia do mundo produza poderes emergentes, quero
que um filho de dois portadores de poderes diferentes possa nascer com um `PowerDescriptor`
novo, recombinando eixos dos dois poderes originais — nunca uma cópia de um dos pais.

**Why P1**: É o núcleo do valor pedido ("emergência genealógica de verdade") — sem isso, o
resto da fase (só estágios) não entrega a parte mais pedida pelo usuário.

**Acceptance Criteria**:

1. WHEN `NatalitySystem` processa o nascimento de um filho cujos dois pais são portadores de
   `PowerDescriptor`s diferentes no momento da concepção THEN o motor SHALL, com uma
   probabilidade declarada em regra de cenário (nunca 100% garantido — herança de poder é
   probabilística, mesmo espírito de `AcquisitionRules`), gerar um `PowerDescriptor` novo pro
   filho recombinando eixos dos dois originais (ver Assumptions pro modelo exato de
   recombinação, a confirmar em Design).
2. WHEN o `PowerDescriptor` resultante é gerado THEN cada eixo (fonte/efeito/custo/condição/
   aquisição) SHALL vir de exatamente um dos dois pais (ou de ambos, só quando os dois
   declaram o MESMO tipo de efeito — nunca combinação semanticamente inválida que nenhuma
   mecânica registrada da Fase 16.1 sabe interpretar).
3. WHEN o `PowerDescriptor` resultante é aplicado THEN ele SHALL passar pela mesma validação
   de contrato já usada pra qualquer poder autorado manualmente (`Prepare`/`PrepareEffects`) —
   nunca um caminho de bypass só porque foi gerado, não digitado.
4. WHEN os dois pais NÃO são portadores de poder (ou só um é) THEN nenhuma mistura SHALL
   ocorrer — herança de poder exige os dois eixos presentes.
5. WHEN a mesma seed e os mesmos dois pais são usados em duas execuções THEN o
   `PowerDescriptor` resultante SHALL ser byte-idêntico.

**Independent Test**: par de pais com poderes A (`gravity.self`) e B (`luck.capacity-bonus`)
gerando um filho — mundo tratado com seed fixa produz um `PowerDescriptor` novo no filho que
referencia eixos de A e/ou B (nunca um terceiro eixo não declarado por nenhum dos dois);
repetir com a mesma seed produz resultado idêntico.

---

### P2: Limite anti-inflação de poder entre gerações

**User Story**: Como quem cuida do balanceamento de longo prazo do mundo, quero que a mistura
genética nunca produza um poder mais forte que a soma dos dois originais, pra evitar que N
gerações produzam um "super-poder" sem teto.

**Why P2**: Consequência direta da decisão de "recombinação, nunca soma" nas Assumptions —
precisa de um teste dedicado provando isso, não só a intenção declarada.

**Acceptance Criteria**:

1. WHEN um `PowerDescriptor` resultante de mistura é comparado, eixo a eixo, contra os dois
   originais dos pais THEN nenhuma magnitude declarada SHALL exceder o máximo já declarado
   pelos pais para aquele eixo (recombinação, nunca soma/potencialização).
2. WHEN um neto (filho de dois filhos-de-mistura) é gerado THEN a mesma regra SHALL valer
   recursivamente — o teto de magnitude nunca acumula geração após geração.

**Independent Test**: simular 3 gerações de mistura com a mesma seed — magnitude de qualquer
eixo no neto nunca excede o máximo já visto em qualquer ancestral direto.

## Edge Cases

- WHEN um portador tem MÚLTIPLOS poderes com estágios declarados THEN cada poder SHALL
  reavaliar seu próprio estágio independentemente (nenhum poder força a reavaliação de outro).
- WHEN o `PowerDescriptor` resultante de uma mistura não passa na validação de contrato (AC3
  da história de mistura) THEN o motor SHALL descartar o resultado e o filho SHALL nascer sem
  poder (falha segura — nunca aplicar um descritor inválido "mesmo assim").

## Requirement Traceability

| ID | Story | Status |
| --- | --- | --- |
| EVO-01 | P1: Estágios — aplica o estágio mais alto atingido | Pending |
| EVO-02 | P1: Estágios — estágio 0 antes do primeiro limiar, nunca falha | Pending |
| EVO-03 | P1: Estágios — contador de uso incrementa só em sucesso | Pending |
| EVO-04 | P1: Estágios — determinismo por seed/histórico | Pending |
| EVO-10 | P1: Mistura — gera descritor novo probabilisticamente | Pending |
| EVO-11 | P1: Mistura — cada eixo vem de um pai (ou ambos se mesmo tipo) | Pending |
| EVO-12 | P1: Mistura — passa pela mesma validação de contrato | Pending |
| EVO-13 | P1: Mistura — sem os dois pais portadores, nenhuma mistura | Pending |
| EVO-14 | P1: Mistura — determinismo por seed | Pending |
| EVO-20 | P2: Anti-inflação — magnitude nunca excede o máximo dos pais | Pending |
| EVO-21 | P2: Anti-inflação — regra vale recursivamente entre gerações | Pending |

**Coverage**: 11 total. **Bloqueado pra Design**: as 4 Assumptions não confirmadas acima —
especialmente o modelo exato de recombinação genética (maior risco desta fase), que precisa de
decisão do usuário antes de qualquer arquitetura ser desenhada.

## Success Criteria

- [ ] Poder de exemplo com 3+ estágios evolui de forma determinística e testada conforme
      idade/uso do portador avança.
- [ ] Par de portadores com poderes distintos gera um filho com `PowerDescriptor` novo,
      recombinando eixos dos pais, validado pelo mesmo contrato de poder autorado manualmente.
- [ ] Nenhuma magnitude de eixo herdado excede o máximo já declarado pelos ancestrais diretos,
      testado por pelo menos 3 gerações simuladas.
- [ ] `dotnet test` completo sem regressão na suíte `Extraordinary*`/`Population*`/`Natality*`.
