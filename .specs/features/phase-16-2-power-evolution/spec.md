# Fase 16.2 — Power Evolution (Progressão + Mistura Genética de Poderes)

## Problem Statement

A Fase 16.1 (`phase-16-1-power-engine`) generaliza COMO um poder se pluga na simulação
(registro de mecânicas), mas deliberadamente deixou de fora, como P3 documentado (`PWR-40..41`),
o sistema de **Power Evolution**: um poder que começa fraco e evolui em estágios ao longo da
vida do portador, e uma regra de **mistura genética** onde o filho de dois portadores de
poderes diferentes pode herdar os dois poderes completos, só um deles, ou uma mistura dos
dois — nunca uma cópia garantida e única de um resultado fixo. Esta spec retoma esse P3 e o
transforma em feature própria, completa (todas as combinações do espaço de herança, não uma
amostra), agora que o motor de mecânicas (pré-requisito declarado) está desenhado.

## Goals

- [ ] Um `PowerDescriptor` pode declarar estágios de evolução — o conjunto de efeitos ativos do
      portador muda conforme idade E/OU contador de uso avançam, sem precisar de um poder novo
      por estágio.
- [ ] Dois portadores de poderes diferentes, ao terem um filho, produzem um resultado
      determinístico escolhido entre **todo o espaço de possibilidades de herança**: o filho
      herda os DOIS poderes completos e independentes, herda só UM dos dois (do pai A ou do
      pai B), ou herda uma MISTURA recombinada dos dois — todas as três formas são caminhos
      válidos e testados, não um único modelo fixo.
- [ ] Poder resultante da mistura PODE somar/potencializar magnitude acima do que qualquer um
      dos pais tinha isoladamente — não há teto artificial de balanceamento nesta fase
      (decisão explícita do usuário: "pode somar").
- [ ] Cobertura completa: o motor de progressão/mistura funciona pra QUALQUER `PowerDescriptor`
      já registrado (qualquer mecânica da Fase 16.1), não uma amostra pequena — mesmo padrão
      de "sem exceção" já usado como critério de fechamento da 16.1.
- [ ] Toda evolução/combinação continua determinística por seed (mesma garantia da Fase 16/16.1)
      e nunca introduz um efeito que a mecânica registrada (16.1) não sabe interpretar.

## Out of Scope

| Item | Razão |
| --- | --- |
| Novas mecânicas de efeito/custo | Já é escopo fechado da `phase-16-1-power-engine` — esta fase consome o registro existente, nunca adiciona namespace novo de token. |
| Poderes de tempo/viagem no tempo | Mesma exclusão da Fase 16.1 — fica pra Fase 18 (Timelines). |
| Evolução/mistura fora do contexto de poder (ex.: evolução de espécie/genética geral de NPC sem poder envolvido) | Fora do problema declarado — esta fase é só sobre `PowerDescriptor`. |
| Herança de mais de 2 "pais" (poliamoria genética de poder) | Não pedido; `NatalitySystem` hoje só modela 2 pais por concepção — fora de escopo até ser levantado. |
| Teto anti-inflação geração-a-geração | Decisão explícita do usuário ("pode somar") — magnitude pode crescer livremente entre gerações; se isso virar um problema de balanceamento real, é ajuste de regra de cenário, não gate de arquitetura (mesmo espírito já registrado em `ADR-0010` pro eixo de escassez). |

## Assumptions & Decisions (confirmadas com o usuário — 2026-08-25)

| Decisão | Escolha confirmada | Racional |
| --- | --- | --- |
| Gatilho de estágio | **Ambos** — idade biológica (mesma fonte de `MortalityPlanner`) E contador de uso (nº de invocações bem-sucedidas); um `PowerDescriptor` pode declarar um estágio por idade, por uso, ou por ambos simultaneamente (autor do poder escolhe qual eixo(s) usar por estágio) | Usuário confirmou explicitamente: "Ambos" |
| Modelo de mistura genética | **Todas as 3 possibilidades do espaço de herança são caminhos válidos**, escolhidos deterministicamente (seed do mundo + `NpcId` do filho): (a) filho herda AMBOS os poderes dos pais, completos e independentes; (b) filho herda só UM dos dois poderes (do pai A ou do pai B), inalterado; (c) filho herda uma MISTURA recombinada por eixo (fonte/efeito/custo/condição/aquisição) dos dois poderes originais | Usuário confirmou explicitamente: "Siga sua proposta... contanto que exista possibilidade dele ter ambos poderes dos pais, 1 só deles, uma mistura, todas essas possibilidades" |
| Teto anti-inflação entre gerações | **Não existe** — resultado da mistura PODE ser mais forte que a soma dos pais | Usuário confirmou explicitamente: "Pode somar" |
| Escopo do MVP | **Completo** — motor funciona genericamente pra qualquer `PowerDescriptor`/mecânica já registrada na 16.1, testado contra um conjunto representativo amplo (todas as categorias de mecânica da 16.1: attribute/gravity/mind/luck/combat/transfer/etc.), não uma amostra reduzida de 2-3 poderes | Usuário confirmou explicitamente: "Completo por favor" |

**Todas as 4 decisões estão confirmadas** — nenhuma pendência bloqueando Design.

## User Stories

### P1: Estágios de evolução por idade e/ou uso

**User Story**: Como quem desenha um poder que começa fraco ("afinidade cinética" baixa) e
fica mais forte com o tempo/prática, quero declarar estágios no `PowerDescriptor` que trocam
o conjunto de efeitos ativos do portador conforme ele envelhece E/OU usa o poder, sem precisar
conceder um poder novo a cada estágio.

**Why P1**: É a metade mais simples/menos arriscada desta fase — não envolve dois portadores
nem geração de descritor novo, só reavaliação do descritor já existente.

**Acceptance Criteria**:

1. WHEN um `PowerDescriptor` declara uma lista de estágios (cada um com um limiar de idade,
   de contador-de-uso, ou de ambos, e um conjunto de efeitos) THEN o motor SHALL aplicar, a
   cada reavaliação de manifestação, o conjunto de efeitos do estágio mais alto cujo(s)
   limiar(es) declarado(s) o portador já atingiu — nunca um estágio futuro, nunca mais de um
   estágio simultâneo.
2. WHEN um estágio declara os dois eixos (idade E uso) THEN ambos SHALL ter sido atingidos
   pra aquele estágio contar como alcançado (AND, não OR) — autor do poder decide, por
   estágio, se usa só idade, só uso, ou os dois combinados.
3. WHEN o portador ainda não atingiu o limiar do primeiro estágio declarado THEN o poder
   SHALL permanecer com o conjunto de efeitos do estágio 0 (ou inativo, se nenhum estágio 0
   for declarado) — nunca falhar por "estágio não encontrado".
4. WHEN o contador de uso é (parte d)o gatilho declarado THEN ele SHALL incrementar
   exatamente uma vez por invocação bem-sucedida daquele poder (nunca por invocação falha) —
   reusa o mesmo log causal já usado por `UseFailed`/`EffectApplied`.
5. WHEN a mesma seed e o mesmo histórico de invocações/idade são usados em duas execuções
   THEN o estágio corrente SHALL ser byte-idêntico entre elas.

**Independent Test**: poder de exemplo com 3 estágios (um só por idade, um só por uso, um por
ambos) — um NPC criado com 10 anos manifesta o estágio 0; ao atingir 18 anos E 5 usos
simulados, o motor troca pro estágio que exige ambos, sem intervenção manual; um NPC que
atinge 18 anos mas não os 5 usos permanece no estágio anterior.

---

### P1: Mistura genética — espaço completo de herança (ambos / um / mistura)

**User Story**: Como quem quer que a genealogia do mundo produza poderes emergentes de
verdade, quero que um filho de dois portadores de poderes diferentes possa nascer de 3 formas
possíveis — com os dois poderes completos, com só um deles, ou com uma mistura recombinada dos
dois — escolhido deterministicamente, nunca um único modelo fixo.

**Why P1**: É o núcleo do valor pedido ("emergência genealógica de verdade") — sem cobrir as 3
formas, o resto da fase (só estágios) não entrega a diversidade genética que o usuário pediu.

**Acceptance Criteria**:

1. WHEN `NatalitySystem` processa o nascimento de um filho cujos dois pais são portadores de
   `PowerDescriptor`s diferentes no momento da concepção THEN o motor SHALL, com uma
   probabilidade declarada em regra de cenário (nunca 100% garantido — herança de poder é
   probabilística, mesmo espírito de `AcquisitionRules`), escolher deterministicamente
   (seed do mundo + `NpcId` do filho) UM dos 3 resultados possíveis: (a) ambos, (b) um só, ou
   (c) mistura — cada resultado com peso configurável em regra de cenário, nunca hardcoded
   como único caminho.
2. WHEN o resultado escolhido é "ambos" THEN o filho SHALL nascer portador dos dois
   `PowerDescriptor`s originais, completos e independentes (cada um manifesta/invoca
   normalmente, sem interferência um do outro).
3. WHEN o resultado escolhido é "um só" THEN o filho SHALL nascer portador de exatamente um
   dos dois `PowerDescriptor`s originais, inalterado (cópia fiel do pai escolhido — a
   escolha de QUAL pai também é determinística pela mesma semente).
4. WHEN o resultado escolhido é "mistura" THEN o motor SHALL gerar um `PowerDescriptor` novo
   recombinando eixos (fonte/efeito/custo/condição/aquisição) dos dois originais — cada eixo
   vem de um dos dois pais, ou de ambos quando os dois declaram o mesmo tipo de efeito
   (agregando magnitude, nunca descartando um lado silenciosamente); magnitude agregada PODE
   exceder o valor isolado de qualquer um dos pais (sem teto — decisão confirmada).
5. WHEN qualquer um dos 3 resultados é gerado/selecionado THEN o(s) `PowerDescriptor`(s)
   resultante(s) SHALL passar pela mesma validação de contrato já usada pra qualquer poder
   autorado manualmente (`Prepare`/`PrepareEffects`) — nunca um caminho de bypass só porque
   foi gerado, não digitado.
6. WHEN os dois pais NÃO são portadores de poder (ou só um é) THEN nenhuma herança SHALL
   ocorrer — os 3 resultados exigem os dois pais portadores.
7. WHEN a mesma seed e os mesmos dois pais são usados em duas execuções THEN o resultado
   (qual dos 3 caminhos, e o `PowerDescriptor` exato gerado) SHALL ser byte-idêntico.

**Independent Test**: par de pais com poderes A (`gravity.self`) e B (`luck.capacity-bonus`) —
rodar N nascimentos com seeds diferentes (mesmos pais) produz uma distribuição observável dos
3 resultados batendo com os pesos configurados; pra uma seed fixa específica, o resultado (e o
`PowerDescriptor` exato, se "mistura") é reproduzível entre execuções.

---

### P1: Cobertura completa — qualquer mecânica registrada participa

**User Story**: Como quem desenha o catálogo de poderes do mundo, quero que evolução por
estágio e mistura genética funcionem pra QUALQUER `PowerDescriptor`/mecânica já registrada na
Fase 16.1 (atributo, gravidade, mente, sorte, combate, transferência, etc.), não só um punhado
de exemplos — pra "qualquer poder pode evoluir/se misturar" ser verdade sem exceção.

**Why P1**: Decisão explícita do usuário ("Completo") — sem esta história, a fase entregaria
um motor genérico só provado em 2-3 casos, deixando em aberto se mecânicas mais complexas
(instanciação de NPC, controle/possessão, vínculo) participam de verdade.

**Acceptance Criteria**:

1. WHEN um `PowerDescriptor` de QUALQUER mecânica registrada na 16.1 (incluindo as de maior
   risco: `npc.clone`, `control.possess`, `bond.share`, `dimension.portal`) declara estágios
   THEN a progressão SHALL funcionar da mesma forma genérica que pra uma mecânica simples —
   nenhuma mecânica precisa de tratamento especial no motor de evolução.
2. WHEN dois `PowerDescriptor`s de mecânicas DIFERENTES entre si são combinados (resultado
   "mistura") THEN o motor SHALL produzir um descritor válido mesmo quando os eixos não têm
   equivalente direto entre as duas mecânicas (cada eixo sem equivalente simplesmente vem do
   pai que o declara, nunca gera erro por "mecânicas incompatíveis").
3. WHEN a suíte de teste desta fase roda THEN SHALL haver pelo menos um caso de teste (estágio
   OU mistura) cobrindo cada categoria de mecânica da 16.1 (atributo, gravidade, mente, sorte,
   combate, transferência, instanciação, controle, vínculo, dimensional, ambiental/fauna/
   flora) — representativo, não exaustivo poder-a-poder, mas sem categoria deixada de fora.

**Independent Test**: matriz de teste com um `PowerDescriptor` de amostra por categoria de
mecânica da 16.1 — cada um evolui por estágio corretamente, e cada par cruzado (2 categorias
diferentes) produz um resultado válido nos 3 caminhos de herança.

## Edge Cases

- WHEN um portador tem MÚLTIPLOS poderes com estágios declarados THEN cada poder SHALL
  reavaliar seu próprio estágio independentemente (nenhum poder força a reavaliação de outro).
- WHEN o `PowerDescriptor` resultante de uma mistura não passa na validação de contrato THEN
  o motor SHALL descartar o resultado e o filho SHALL nascer sem poder (falha segura — nunca
  aplicar um descritor inválido "mesmo assim").
- WHEN o resultado "ambos" é escolhido mas os dois poderes dos pais compartilham a MESMA
  mecânica/eixo (ex.: os dois pais têm `attribute.strength`, valores diferentes) THEN o filho
  SHALL manifestar os dois descritores originais separadamente (nenhuma fusão automática só
  porque "ambos" foi escolhido — fusão só ocorre no caminho "mistura").

## Requirement Traceability

| ID | Story | Status |
| --- | --- | --- |
| EVO-01 | P1: Estágios — aplica o estágio mais alto atingido | Pending |
| EVO-02 | P1: Estágios — estágio com idade+uso exige ambos (AND) | Pending |
| EVO-03 | P1: Estágios — estágio 0 antes do primeiro limiar, nunca falha | Pending |
| EVO-04 | P1: Estágios — contador de uso incrementa só em sucesso | Pending |
| EVO-05 | P1: Estágios — determinismo por seed/histórico | Pending |
| EVO-10 | P1: Herança — escolhe 1 dos 3 resultados deterministicamente, pesos configuráveis | Pending |
| EVO-11 | P1: Herança — resultado "ambos" preserva os 2 descritores completos/independentes | Pending |
| EVO-12 | P1: Herança — resultado "um só" copia fielmente o pai escolhido | Pending |
| EVO-13 | P1: Herança — resultado "mistura" recombina eixos, magnitude pode somar sem teto | Pending |
| EVO-14 | P1: Herança — todo resultado passa pela mesma validação de contrato | Pending |
| EVO-15 | P1: Herança — sem os dois pais portadores, nenhuma herança | Pending |
| EVO-16 | P1: Herança — determinismo por seed (resultado e descritor exatos) | Pending |
| EVO-20 | P1: Cobertura — estágio funciona em qualquer mecânica, inclusive as de maior risco | Pending |
| EVO-21 | P1: Cobertura — mistura entre mecânicas diferentes nunca gera erro de incompatibilidade | Pending |
| EVO-22 | P1: Cobertura — matriz de teste cobre cada categoria de mecânica da 16.1 | Pending |

**Coverage**: 15 total. Todas as decisões de Assumptions confirmadas — sem bloqueio pra Design.

## Success Criteria

- [ ] Poder de exemplo com estágios por idade, por uso, e por ambos evolui de forma
      determinística e testada.
- [ ] Par de portadores com poderes distintos produz, de forma determinística e com pesos
      configuráveis, um dos 3 resultados possíveis (ambos/um só/mistura) — todos os 3 testados.
- [ ] Resultado "mistura" pode ter magnitude agregada maior que qualquer um dos pais
      isoladamente (sem teto), validado pelo mesmo contrato de poder autorado manualmente.
- [ ] Cada categoria de mecânica da Fase 16.1 (atributo, gravidade, mente, sorte, combate,
      transferência, instanciação, controle, vínculo, dimensional, ambiental/fauna/flora) tem
      pelo menos 1 caso de teste de estágio e participa de pelo menos 1 caso de teste de
      mistura — sem exceção, cobertura completa conforme decisão do usuário.
- [ ] `dotnet test` completo sem regressão na suíte `Extraordinary*`/`Population*`/`Natality*`.
