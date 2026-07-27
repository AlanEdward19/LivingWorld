# Fase 4 — Necessidades e rotina — Specification

## Problem Statement

O NPC hoje (Fase 3) é um registro que envelhece, nasce e morre por tabela de vida — não
decide nada. Fase 4 dá a ele um motor de decisão determinístico: medidores de necessidade
que decaem por tick, um utility AI que escolhe a ação de maior nota entre candidatas
ponderadas por personalidade e contexto, uma rotina diária que a urgência sobrepõe, e
histerese para não alternar de ação a cada tick. Sem isso, objetivo #1 (100 anos coerentes)
não tem comportamento individual — só demografia agregada.

## Goals

- [ ] NPC vivo escolhe uma ação a cada tick relevante, com nota rastreável (não decisão
      oculta em método privado).
- [ ] Fome/sede/sono decaem por tick a taxa do cenário e, em 0, disparam consequência
      observável (objetivo ativo; fome sustentada mata em prazo derivado do cenário).
- [ ] Rotina diária por profissão e estágio de vida roda por padrão; utility AI sobrepõe
      só quando algo urgente supera o bônus de continuidade.
- [ ] Toda decisão é determinística e entra no hash canônico (desligar o sistema muda o
      hash).

## Out of Scope

| Feature | Reason |
| --- | --- |
| Produção, salário, preço, compra de comida | Fase 5 — aqui comida é recurso de cenário disponível ou não |
| Aprendizado/melhoria em ações, progressão de habilidade | Fase 6 |
| Relações sociais reais (amizade, confiança, atração) | Fase 7 — `Socialize` nesta fase só decai/satisfaz o medidor mínimo social, sem grafo de relação |
| Emprego real (contratação, demissão, múltiplos empregos) | Fase 5 — profissão nesta fase é atributo estático do NPC, não vínculo econômico |
| IA de grupo/coordenação entre NPCs (ex.: fila, negociação de recurso disputado) | Fora do objetivo #1; cada NPC decide isoladamente |

---

## Assumptions & Open Questions

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Profissão vira campo estático do NPC | `Npc.Profession` (id do `PopulationCatalog.ProfessionIds`), sorteado na criação por peso do cenário, sem produção/salário | Decisão do usuário (discuss) — task 6 exige rotina *por profissão*; catálogo já existe (AD-025), faltava só o vínculo | y |
| Estágio de vida | Enum derivado de `AgeYears` via limiares do cenário (`ChildMaxAge`, `AdultMaxAge` — acima é `Elder`), não hardcoded | Mesmo padrão de `LifeTable`/`PopulationRules`: limiar é dado, nunca constante em C# (R3) | y — assumption, não perguntado ao usuário |
| Catálogo de ações candidatas | `Eat, Sleep, Work, Socialize, Travel, Idle` | Decisão do usuário (discuss) | y |
| Moradia/local atual | `Npc.Residence` (nullable `HouseholdId` ou local dedicado) + `Npc.CurrentLocation` (`CellCoord`), com estado `HomelessSince` explícito quando sem residência | Decisão do usuário (discuss) | y |
| Mecanismo de histerese liga/desliga | Flag booleana em `PopulationRules` (`HysteresisEnabled`), lida pelo cenário — não constante de teste isolada | Critério exige "ligável/desligável por flag de teste"; seguir o padrão existente de tudo vir do cenário (nunca literal em C#) | n — assumption, não perguntado |
| Personalidade nasce com o NPC | 10 traços 0-100 sorteados por `WorldRng` do stream do NPC na criação (mesmo stream de nascimento), sem herança genética ainda | Fase 7 é quem faz hereditariedade (`docs/domain/genetics-and-family.md`); aqui só precisa existir para modular peso | n — assumption |
| Necessidades cobertas | Fome, sede, sono e "mínimo social" (`Socialize`) — as 4 citadas na task 1; demais necessidades do `npc.md` (segurança, saúde, pertencimento, prestígio, afeto, diversão, propósito) ficam fora | Task 1 lista exatamente essas 4; expandir o catálogo completo de `npc.md` nesta fase é escopo maior que o roadmap pede | n — assumption |
| Sem-teto (`Homeless`) ainda dorme, com penalidade | Dormir fora de residência é permitido mas satisfaz sono a taxa reduzida (parâmetro de cenário `HomelessSleepEfficiency`), nunca bloqueia o tick | Task 9 diz "dormir exige estar nela (ou substituto declarado)" — substituto declarado é dormir no local atual com eficiência menor, evita NPC preso em loop de decisão sem alternativa | n — assumption |
| Desempate de utility AI | Maior nota vence; empate exato desempata por menor `ActionId` (catálogo de ações tem id estável), nunca por ordem de iteração | Já dito explicitamente no roadmap (task 4) | y (roadmap) |
| Teto de passos de seleção | Parâmetro `MaxActionSelectionSteps` do cenário; violar aborta com `TickBudgetExceededException`-like nomeando NPC + ações empatadas | Mesmo padrão de `TickBudgetExceededException` já existente (Fase 1) | n — assumption |

**Open questions:** none — todas resolvidas por discussão ou registradas acima como assumption com rationale.

---

## User Stories

### P1: Medidores de necessidade com decaimento e consequência ⭐ MVP

**User Story**: Como motor de simulação, preciso que cada NPC vivo tenha fome/sede/sono/social
decaindo por tick a taxa do cenário, para que a urgência exista antes de qualquer decisão.

**Why P1**: Sem medidor não há entrada para o utility AI — é a base de tudo mais nesta fase.

**Acceptance Criteria**:

1. NEEDS-01: WHEN um tick Hourly processa um NPC vivo THEN o motor SHALL decrementar
   fome/sede/sono/social pela taxa declarada em `PopulationRules` (não constante em C#).
2. NEEDS-02: WHEN uma necessidade seria decrementada abaixo de 0 (ou incrementada acima de
   100 por uma ação de satisfação) THEN o motor SHALL fazer clamp em `[0,100]` **e**, se o
   clamp ocorreu em 0, SHALL disparar o objetivo correspondente no mesmo tick (nunca só
   silenciar o valor).
3. NEEDS-03: WHEN fome permanece em 0 por `X = ceil(100 / taxaDecaimentoFome)` ticks
   consecutivos sem alimentação THEN o NPC SHALL morrer em `[X, X+1]` ticks, com
   `causa == Starvation` no event log, datável por tick.
4. NEEDS-04: WHEN o utility AI é desligado por flag do cenário THEN o `Hash(world)` em 10
   anos SHALL diferir do mundo com utility AI ligado, mesma seed (prova que decisão entra
   na conta).

**Independent Test**: Rodar cenário com 1 NPC sem acesso a comida por `X+1` ticks e checar
morte com causa `Starvation`; rodar par ligado/desligado do utility AI e comparar hash.

---

### P2: Utility AI — necessidade → objetivo → ação escolhida

**User Story**: Como NPC, quero que minha maior necessidade vire objetivo e que a ação de
maior utilidade (necessidade × contexto × personalidade) seja escolhida, para que minha
urgência determine meu comportamento sem script fixo.

**Why P2**: É o núcleo comportamental da fase; depende de P1 (medidores) já existir.

**Acceptance Criteria**:

1. NEEDS-05: WHEN uma necessidade ultrapassa o limiar declarado no cenário THEN o motor
   SHALL expor um objetivo ativo e inspecionável (não estado interno de método privado).
2. NEEDS-06: WHEN o utility AI pontua ações candidatas (`Eat, Sleep, Work, Socialize,
   Travel, Idle`) THEN a nota de cada uma SHALL ser `utilidadeBase(necessidade, contexto) *
   pesoPersonalidade`, e a ação de maior nota SHALL vencer; empate exato desempata por
   menor `ActionId`.
3. NEEDS-07 (fome vence trabalho, com controle): WHEN mesmo cenário/seed roda com fome=90
   vs fome=10, comida a 1 local de distância, turno de trabalho aberto THEN o NPC SHALL
   escolher `Eat` no braço 90 e `Work` no braço 10, em 10/10 seeds.
4. NEEDS-08 (direção por traço, tabela de casos): WHEN mesma seed roda com um traço de
   personalidade em 20 vs 80, para cada um dos 10 traços de `npc.md` THEN a ação escolhida
   SHALL corresponder à linha da tabela de casos (`[traço, cenário, açãoBaixo, açãoAlto]`)
   em 10/10 seeds; o teste SHALL falhar se algum dos 10 traços não tiver linha.
5. NEEDS-09 (terminação): WHEN a seleção de ação roda THEN o motor SHALL convergir em até
   `MaxActionSelectionSteps` (parâmetro do cenário) passos; ao fim do tick nenhum NPC vivo
   SHALL ficar sem ação escolhida; WHEN utilidades formam ciclo patológico (cenário
   adversarial) THEN o motor SHALL abortar nomeando NPC e ações empatadas, nunca laçar.

**Independent Test**: Cenário com 2 braços de fome (90/10) mesma seed → ação distinta em
10/10 seeds; tabela de 10 traços rodada em 20/80 → ação prevista em 10/10 seeds cada;
cenário adversarial de utilidade cíclica → exceção nomeada, sem timeout.

---

### P2: Rotina diária, sobreposição e histerese

**User Story**: Como NPC, sigo uma rotina padrão por profissão e estágio de vida na maior
parte do tempo, mas a rotina cede a algo urgente — e não fico alternando de ação a cada tick
quando duas notas empatam de perto.

**Why P2**: Sem rotina todo NPC seria puro utility AI (caro e sem previsibilidade); sem
histerese o utility puro geraria *thrashing* observável no hash e no comportamento.

**Acceptance Criteria**:

1. NEEDS-10: WHEN nenhuma necessidade ultrapassa o limiar de urgência THEN o NPC SHALL
   seguir a rotina diária declarada para `(Profession, LifeStage, hora)`, com duração
   máxima declarada por ação do catálogo.
2. NEEDS-11: WHEN uma necessidade ultrapassa o limiar de urgência durante a rotina THEN o
   utility AI SHALL sobrepor a rotina (a ação urgente vence mesmo fora do horário padrão).
3. NEEDS-12 (histerese com controle): WHEN um par de execuções roda com/sem histerese,
   mesma seed, 20 seeds THEN `trocas_com < trocas_sem` SHALL valer em 20/20 seeds; o teto
   absoluto de trocas por dia é o percentil 99 das 20 seeds, gravado em
   `tests/baselines/action-switches.json` (nenhum número mágico no texto do critério).
4. NEEDS-13 (sem deadlock de rotina): WHEN um NPC permanece na mesma ação além da duração
   máxima declarada dela THEN o assert (rodando a cada tick, 10 anos) SHALL falhar; o teste
   SHALL falhar também se alguma ação do catálogo não declarar duração máxima.

**Independent Test**: Par com/sem histerese, 20 seeds, `trocas_com < trocas_sem` em 20/20;
10 anos de assert por tick sem violação de duração máxima.

---

### P2: Deslocamento e moradia

**User Story**: Como NPC, preciso me mover entre locais consumindo tempo real de custo, e
ter uma residência onde dormir — ou um estado explícito de "sem residência" quando não
tenho.

**Why P2**: Task 8/9 do roadmap; sem custo de deslocamento a rotina "ir trabalhar" seria
teletransporte, e sem residência explícita, sono e moradia colidiriam num `null` silencioso.

**Acceptance Criteria**:

1. NEEDS-14: WHEN um NPC decide se mover entre células distintas THEN o motor SHALL
   consumir `>= 1` tick segundo `MovementCost.Between` (Fase 2); enquanto desloca, o NPC
   SHALL não executar a ação de destino no mesmo tick em que decidiu ir.
2. NEEDS-15: WHEN um NPC dorme THEN SHALL estar em `Residence` (satisfação plena de sono)
   ou, se `Residence is null` (`Homeless`), SHALL dormir no `CurrentLocation` com eficiência
   reduzida (`HomelessSleepEfficiency` do cenário) — nunca bloqueia o tick nem lança exceção
   por falta de residência.
3. NEEDS-16: WHEN um NPC não tem residência THEN o estado SHALL ser explícito
   (`Residence is null` + timestamp `HomelessSince`), nunca um `null` sem sinalização
   equivalente a "não implementado".

**Independent Test**: NPC em local A decide ir a B — tick(s) intermediário(s) sem ação de
destino, chegada só após custo consumido; NPC sem `Residence` dorme com eficiência menor,
sem exceção; consulta de NPCs `Homeless` retorna o conjunto esperado.

---

## Edge Cases

- WHEN `taxaDecaimentoFome` do cenário é 0 THEN a necessidade nunca decai e o NPC nunca
  morre de fome — comportamento válido (cenário pode desligar fome), não erro.
- WHEN duas ações empatam exatamente na nota THEN desempate é por `ActionId`, nunca por
  ordem de iteração da coleção candidata.
- WHEN o NPC está `Homeless` e a rotina manda dormir THEN dorme no `CurrentLocation`
  (NEEDS-15), não trava aguardando residência.
- WHEN o cenário de teste declara utilidades cíclicas (ação A > B > C > A) THEN a seleção
  aborta no teto declarado, nomeando NPC e ações — nunca laça o tick.
- WHEN um NPC morre no meio do deslocamento THEN o evento de morte (Fase 3) processa antes
  de qualquer ação de destino ser aplicada — NPC morto não chega a lugar nenhum.

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| NEEDS-01 | P1: Medidores | Design | Pending |
| NEEDS-02 | P1: Medidores | Design | Pending |
| NEEDS-03 | P1: Medidores | Design | Pending |
| NEEDS-04 | P1: Medidores | Design | Pending |
| NEEDS-05 | P2: Utility AI | Design | Pending |
| NEEDS-06 | P2: Utility AI | Design | Pending |
| NEEDS-07 | P2: Utility AI | Design | Pending |
| NEEDS-08 | P2: Utility AI | Design | Pending |
| NEEDS-09 | P2: Utility AI | Design | Pending |
| NEEDS-10 | P2: Rotina | Design | Pending |
| NEEDS-11 | P2: Rotina | Design | Pending |
| NEEDS-12 | P2: Rotina | Design | Pending |
| NEEDS-13 | P2: Rotina | Design | Pending |
| NEEDS-14 | P2: Deslocamento/moradia | Design | Pending |
| NEEDS-15 | P2: Deslocamento/moradia | Design | Pending |
| NEEDS-16 | P2: Deslocamento/moradia | Design | Pending |

**Coverage:** 16 total, 16 mapped to design (pending tasks breakdown), 0 unmapped.

---

## Success Criteria

- [ ] `bash scripts/verify.sh` em 0 (check-docs + build + lint + test).
- [ ] Todos os critérios de `docs/roadmap/phase-04-needs.md` provados por teste automatizado
      (nenhum "por inspeção").
- [ ] `STATE.md` atualizado com handoff pra Fase 5 e novos `AD-NNN` desta fase.
