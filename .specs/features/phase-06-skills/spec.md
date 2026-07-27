# Fase 6 — Habilidades e Aprendizado — Specification

## Problem Statement

NPCs hoje são intercambiáveis: todo agricultor produz igual, todo ferreiro rende igual,
profissão é sorteada no nascimento e nunca muda. Isso mata o payoff da Fase 5 (Economia) —
não existe "ferreiro melhor", só "ferreiro". Fase 6 dá a cada NPC uma trajetória própria:
habilidade que sobe por prática/ensino, modula produção e renda, e cria pressão real para
trocar de profissão ou formar aprendizes.

## Goals

- [ ] Cada NPC carrega 13 habilidades numéricas (piso 0, teto por cenário) que sobem por
      pelo menos 6 fontes distintas de ganho, cada uma com taxa e requisito próprios.
- [ ] Habilidade do trabalhador muda quantidade e qualidade da produção do `Workplace` —
      dono melhor produz mais, mensurável em 10/10 seeds pareadas.
- [ ] Genética entra como multiplicador de **taxa** de ganho, nunca do valor de habilidade —
      as duas correlações (habilidade não herdada, taxa de ganho herdada) provadas juntas.
- [ ] NPC pode trocar de profissão; a antiga habilidade estagna (não zera, não decai).

## Out of Scope

| Feature | Reason |
|---|---|
| Modelo genético completo (recombinação, seleção emergente, algoritmo genético do mundo) | `docs/domain/genetics-and-family.md` — pertence à Fase 7. Fase 6 só precisa de um valor de "gene de taxa" por NPC para multiplicar velocidade de ganho; ver Assunção A1. |
| Herança de outros atributos (físicos, personalidade, fertilidade) | Fase 7 — fora do recorte desta fase, que só cobre habilidade e o gene de taxa que a acelera. |
| Escolas como edifício de cidade, vagas por assentamento, currículo | Fase 8 (cidades) — aqui "escola" é só uma fonte de ganho com taxa e requisito, sem prédio nem capacidade. |
| Propriedade de negócio (NPC dono de `Workplace`, lucro, herança de oficina) | AD-044 já bloqueou isso na Fase 5; "oficina" nesta fase é o `Workplace` existente, sem grafo de dono novo. |
| Cliente web / UI de habilidade | Fora do caminho crítico (AD-007) — Fase 15. |
| Magia como habilidade com efeito mecânico distinto das demais 12 | Nesta fase magia é só mais um id do catálogo de 13 — efeito especial de magia é escopo de fase de poderes/divindade (16+), bloqueado por AD-010. |

---

## Assumptions & Open Questions

| Assumption / decision | Chosen default | Rationale | Confirmed? |
|---|---|---|---|
| A1. Como resolver "gene de taxa herdado" sem o modelo genético completo da Fase 7 | Campo `RateGene` por NPC, sorteado no nascimento com fórmula mínima de herança (`geneFilho = geneMãe*0,5 + genePai*0,5 + mutação`, mesmo espírito de `docs/domain/genetics-and-family.md` mas só para este campo), usando `WorldRng` no mesmo padrão de `Personality.RollFrom` (stream próprio do NPC). Fase 7 pode substituir/generalizar depois; não é decisão definitiva de arquitetura genética. | Decidido com o usuário nesta sessão (opção "gene de taxa simplificado agora") — sem isso o critério de correlação pai/filho do roadmap (200 nascimentos) não tem o que medir. | y |
| A2. `Npc.Profession` hoje não tem mutador (fixo desde `PopulationCatalog.RollProfession`) | Fase 6 adiciona `Npc.SwitchProfession(...)`, mesmo padrão de `Hire`/`Fire`/`JoinHousehold` — muda `Profession`, preserva o valor de habilidade da profissão antiga (nenhum reset) | Necessário pra task 7 do roadmap existir; não achado nenhum mutador equivalente no código atual (confirmado por scan). | n — assumido, sem gray area real: é pré-requisito mecânico direto da AC de troca de profissão, não uma escolha de produto. |
| A3. Habilidade "entra na conta" da produção como multiplicador combinado (quantidade e qualidade não são duas saídas separadas nesta fase) | `ProductionSystem` ganhou o multiplicador de output derivado da habilidade média dos trabalhadores presentes (SKILL-10, implementado e verificado). **O canal de preço via `MarketPricingSystem` (SKILL-11) NÃO foi implementado nesta fase** — achado pelo Verifier (`validation.md`): `MarketPricingSystem` não tem dependência de `SkillsRules`, preço só reage a oferta/demanda (mais estoque por habilidade maior tende a *baixar* preço pelo modelo de escassez existente, não a criar um "prêmio de qualidade"). Decisão do usuário nesta sessão: registrar como débito técnico e fechar a fase só com SKILL-10; SKILL-11 (preço reagindo à qualidade) fica para uma fase futura que volte a mexer em `EconomyRules`/`MarketPricingSystem` (candidata: Fase 8/9). | Implementar o canal de preço exigiria tocar `MarketPricingSystem` de novo (fora do diff desta fase) e não há requisito de roadmap que dependa disso para fechar Fase 6 — SKILL-10 sozinho já prova "ferreiro melhor produz mais e melhor" de forma observável (estoque/salário) | y — confirmado com o usuário: NÃO implementar agora, documentar como débito |
| A4. Frequência de tick do novo `SkillGainSystem`/fonte de ensino | `Daily`, mesmo padrão de `ProductionSystem`/`MarketPricingSystem` (AD-042: "registre na frequência mais barata que ainda produz o comportamento") — habilidade não precisa reagir por hora | Nenhuma fonte do roadmap exige resolução horária; ganho diário acumulado é suficiente para as curvas de 20 seeds/20 anos do critério | n — Design confirma ao decidir o slot exato em `ScenarioRunner.DefaultSystems()` |
| A5. Novos `ActionType` para treinamento deliberado/escola/observação | Deferido ao Design — pode reusar `Work`/`Socialize` com contexto (ex.: `Workplace` do tipo escola) ou introduzir 1-2 valores novos no enum fechado (`ActionCatalog` já reprova estaticamente ação sem duração declarada, rede de segurança existente) | `ActionType` é catálogo fechado do motor (não conteúdo de cenário) — decisão de quais ações novas existem é arquitetura, não requisito de produto | n |
| A6. Dimensões implícitas sem requisito nesta fase | Input validation/bounds → coberto (piso/teto declarado, SKILL-01). Failure/partial-failure, idempotência/retry, auth/rate-limit, dependência externa → **N/A**: `SkillGainSystem` é sistema de tick determinístico em processo único, sem I/O, sem API de jogador nesta fase (mesmo motivo de `ProductionSystem`) | Nenhuma dessas dimensões tem superfície nova além do que `ProductionSystem`/`EmploymentSystem` já cobrem | y |

**Open questions:** nenhuma — todas resolvidas ou logadas acima.

---

## User Stories

### P1: Habilidade sobe por prática e muda produção ⭐ MVP

**User Story**: Como designer do mundo, quero que cada NPC acumule habilidade numérica por
trabalhar na própria profissão, e que essa habilidade mude quanto e quão bem o `Workplace`
dele produz, para que "ferreiro melhor" seja um fato observável, não um rótulo.

**Why P1**: Sem isso nada mais na fase tem efeito mensurável — é o elo mínimo entre prática,
habilidade e economia.

**Acceptance Criteria**:

1. WHEN um `Npc` é criado THEN o sistema SHALL atribuir as 13 habilidades do catálogo
   (agricultura, caça, comércio, construção, medicina, combate, ensino, artesanato, política,
   liderança, pesquisa, tecnologia, magia) com valor inicial declarado no cenário, piso 0,
   sem exceder o teto declarado no cenário.
2. WHEN um `Npc` empregado completa um ciclo de trabalho na própria profissão THEN o sistema
   SHALL aumentar a habilidade correspondente pela curva de retornos decrescentes (P2 tem a
   curva como requisito isolado; aqui só a integração de gatilho).
3. WHEN a habilidade de um `Npc` já está no teto do cenário e ele pratica de novo THEN
   `Hash(world)` SHALL permanecer inalterado (ganho no teto é absorvido, nunca ultrapassa nem
   produz efeito colateral em outro campo).
4. WHEN `ProductionSystem` calcula a saída de um `Workplace` THEN o sistema SHALL escalar
   quantidade e preço-base (via qualidade, Assunção A3) pela habilidade média dos
   trabalhadores presentes, comparado à saída do mesmo `Workplace` com trabalhadores de
   habilidade menor, mesma seed, mesma entrada, mesmo número de trabalhadores.
5. WHEN o sistema de habilidades é desligado por flag de cenário de teste THEN `Hash(world)`
   após 10 anos de simulação SHALL divergir do `Hash(world)` com o sistema ligado (prova que
   habilidade entrou na conta, não é decoração).

**Independent Test**: Cenário com um `Workplace` e dois braços (trabalhador júnior vs sênior,
mesma seed) rodando N dias; comparar produção anual do `Workplace` — sênior produz mais em
10/10 seeds (SKILL-10, critério do roadmap).

---

### P2: Curva de retornos decrescentes como propriedade matemática isolada

**User Story**: Como quem calibra o balanceamento, quero uma curva de ganho testável sem
rodar simulação nenhuma, para que qualquer mudança de parâmetro seja validável em
milissegundos, não em 20 seeds de mundo inteiro.

**Why P2**: É pré-requisito técnico do P1 (task 2 do roadmap é isolada de propósito), mas o
mundo já funciona sem ela ser testada isoladamente primeiro — por isso não é P1 por si só.

**Acceptance Criteria**:

1. WHEN a função de ganho é avaliada para qualquer nível `n` em `1..1000` THEN
   `ganho(n+1) <= ganho(n)` SHALL valer sempre (retornos não crescem com o nível).
2. WHEN a curva é parametrizada por cenário (não por habilidade individual) THEN o mesmo
   parâmetro SHALL se aplicar às 13 habilidades — uma curva só, não treze.
3. WHEN a curva é chamada THEN ela SHALL ser função pura — mesma entrada sempre produz a
   mesma saída, sem ler `WorldState` nem depender de ordem de chamada.

**Independent Test**: Unit test puro da função, sem `ScenarioRunner`, sem seed (SKILL-02,
critério do roadmap: "unit test da curva, sem simulação e sem seed").

---

### P3: Fontes de ganho além da prática (treino, escola, pais, observação, tutoria)

**User Story**: Como designer do mundo, quero que habilidade também suba por treino
deliberado, escola, aprendizado com os pais, observação de quem trabalha perto e tutoria
mestre→aprendiz, cada uma com taxa e requisito próprios, para que a trajetória de um NPC
dependa de contexto social, não só do próprio emprego.

**Why P3**: O mundo já é demonstrável com só a prática no trabalho (P1); estas fontes
enriquecem a trajetória mas nenhuma delas sozinha destrava um novo comportamento econômico
observável — são multiplicadores de variedade, não de mecanismo.

**Acceptance Criteria**:

1. WHEN um `Npc` recebe treinamento deliberado (tempo + dinheiro dedicados) THEN o sistema
   SHALL aumentar a habilidade alvo por uma taxa própria dessa fonte, distinta da taxa de
   prática no trabalho.
2. WHEN um `Npc` frequenta escola (requisito: vaga disponível, conforme Fora do Escopo —
   sem prédio nesta fase) THEN o sistema SHALL aumentar habilidade por taxa própria.
3. WHEN uma criança convive com um dos pais que pratica uma profissão THEN o sistema SHALL
   aumentar a habilidade correspondente da criança por taxa própria de aprendizado
   parental.
4. WHEN um `Npc` está fisicamente próximo de outro `Npc` trabalhando (mesmo `Workplace` ou
   `CellCoord`) sem ser o mestre THEN o sistema SHALL aumentar a habilidade por observação,
   com taxa própria, menor que a de tutoria direta.
5. WHEN um mestre (`Npc` com habilidade alta na profissão e vínculo de tutoria declarado)
   ensina um aprendiz THEN a taxa de ganho do aprendiz SHALL depender de
   `min(habilidade do mestre, teto do cenário)` e da habilidade de **ensino** do mestre —
   mestre no topo da faixa do cenário produz aprendiz com habilidade maior que mestre no
   piso da faixa, mesma idade, mesmos genes, mesma seed, **20 seeds, 20/20**.

**Independent Test**: Cenário pareado mestre-topo vs mestre-piso (SKILL-08/SKILL-16,
critério do roadmap) — comparar habilidade final do aprendiz nos dois braços.

---

### P3: Genética como multiplicador de taxa (não de valor)

**User Story**: Como designer do mundo, quero que dois NPCs com prática idêntica mas genes
diferentes divirjam em habilidade, e que a habilidade em si nunca seja herdada — só a
predisposição de aprender rápido — para honrar o invariante de `genetics-and-family.md`
("genética não é destino").

**Why P3**: Reforça o tema do jogo (mérito por prática, não por sangue) mas o mundo já
funciona economicamente sem ela — é uma camada de nuance sobre P1, não o mecanismo central.

**Acceptance Criteria**:

1. WHEN um `Npc` nasce THEN o sistema SHALL sortear `RateGene` por
   `geneMãe*0,5 + genePai*0,5 + mutação` (Assunção A1), usando stream de RNG próprio do NPC
   (mesmo padrão de `Personality.RollFrom`), nunca 0 nem negativo.
2. WHEN dois NPCs têm genes de taxa diferentes e prática idêntica (mesma seed exceto o gene)
   THEN eles SHALL terminar com habilidades **diferentes** — 20 seeds, 20/20.
3. WHEN dois NPCs têm genes de taxa idênticos e prática idêntica THEN eles SHALL terminar
   **byte-idênticos** em habilidade — 20 seeds, 20/20.
4. WHEN se mede a correlação `habilidade(pai) ↔ habilidade(filho)` em 200 nascimentos THEN o
   IC95 SHALL conter 0 (habilidade não herdada).
5. WHEN se mede a correlação `RateGene(pai) ↔ RateGene(filho)` nos mesmos 200 nascimentos
   THEN o IC95 SHALL estar inteiramente acima de 0 (taxa herdada) — os dois asserts (4) e
   (5) juntos, nunca um sem o outro.

**Independent Test**: Harness de 200 nascimentos com genes/habilidade registrados por NPC,
duas correlações computadas sobre o mesmo dataset (SKILL-09, critério do roadmap).

---

### P3: Escolha e troca de profissão

**User Story**: Como designer do mundo, quero que um NPC possa trocar de profissão com
custo real (habilidade antiga estagna, não zera), escolhendo por habilidade atual,
personalidade e vagas abertas, para que trocar seja uma decisão, não um reset gratuito.

**Why P3**: Depende de P1 (habilidade precisa existir para pesar na escolha) e não é
necessário pra provar o elo prática→produção; é a peça que fecha o roadmap mas pode vir por
último sem bloquear as demais.

**Acceptance Criteria**:

1. WHEN `BehaviorDecisionSystem` avalia trocar de profissão THEN o sistema SHALL pontuar
   candidatas por habilidade atual do `Npc` na profissão candidata, traços de
   `Personality` (mesmo padrão de `PersonalityWeighting`) e vagas abertas (`EmploymentSystem`).
2. WHEN um `Npc` troca de profissão THEN `Npc.SwitchProfession(...)` SHALL preservar o valor
   de habilidade da profissão antiga sem zerá-lo (estagnação, não reset) e SHALL parar de
   receber ganho de prática nela até voltar.
3. WHEN um NPC trabalha 20 anos na mesma profissão comparado a um NPC de mesma idade e
   mesmos genes que troca a cada 2 anos (mesma seed nos dois braços) THEN o especialista
   SHALL terminar com habilidade maior na profissão final — 20 seeds, 20/20; a razão média
   vai para `tests/baselines/`, desvio >±30% abre alerta de revisão (não falha o gate).

**Independent Test**: Cenário pareado especialista vs trocador (SKILL-03/SKILL-15, critério
do roadmap).

---

## Edge Cases

- WHEN um `Npc` nunca trabalha (desempregado a vida toda) THEN nenhuma habilidade de
  profissão SHALL subir por prática (outras fontes — escola, pais, observação — continuam
  valendo se aplicáveis).
- WHEN o `Workplace` não tem trabalhador nenhum presente THEN o multiplicador de habilidade
  SHALL ser neutro (sem produção pra escalar — mesmo comportamento atual de `ProductionSystem`
  com `workersPresent == 0`).
- WHEN um mestre morre no meio da tutoria THEN o vínculo de tutoria do aprendiz SHALL ser
  encerrado sem exceção (mesmo padrão de `LeaveHousehold`/sweep referencial — nenhum ponteiro
  solto para `NpcId` morto).
- WHEN a curva de retornos decrescentes recebe nível 0 ou negativo (não deveria ocorrer dado
  o piso 0) THEN a função SHALL retornar ganho não-negativo sem lançar exceção — defesa de
  fronteira, não caminho esperado.

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
|---|---|---|---|
| SKILL-01 | P1: Habilidade sobe por prática | Tasks T1,T4,T6,T12,T13,T19 | ✅ Verified |
| SKILL-02 | P2: Curva de retornos decrescentes | Tasks T2 | ✅ Verified |
| SKILL-03 | P1/P3: Ganho por prática no trabalho + especialização compensa | Tasks T8,T14 | ✅ Verified |
| SKILL-04 | P3: Treinamento deliberado | Tasks T9 | ✅ Verified |
| SKILL-05 | P3: Escola | Tasks T9 | ✅ Verified |
| SKILL-06 | P3: Aprendizado com os pais | Tasks T9 | ✅ Verified |
| SKILL-07 | P3: Observação de quem trabalha perto | Tasks T9 | ✅ Verified |
| SKILL-08 | P3: Tutoria mestre→aprendiz | Tasks T9,T15 | ✅ Verified |
| SKILL-09 | P3: Gene de taxa — herdado, habilidade não | Tasks T5,T12,T16,T17 | ✅ Verified |
| SKILL-10 | P1: Habilidade → produção (quantidade) | Tasks T10,T18 | ✅ Verified |
| SKILL-11 | P1: Qualidade → preço via Fase 5 | — | ❌ Não implementado — débito técnico registrado na Assunção A3; canal de preço fica para fase futura (Fase 8/9), decisão do usuário |
| SKILL-12 | P1: Teto não move o mundo / flag off muda hash | Tasks T4,T19 | ✅ Verified |
| SKILL-13 | P3: Escolha de profissão por score | Tasks T11 | ✅ Verified |
| SKILL-14 | P3: Troca de profissão — estagnação | Tasks T7,T11 | ✅ Verified |
| SKILL-15 | P3: Cenário pareado especialista vs trocador | Tasks T14 | ✅ Verified |
| SKILL-16 | P3: Cenário pareado mestre-topo vs mestre-piso | Tasks T15 | ✅ Verified |

**ID format:** `SKILL-NN`

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 16 total, 15 verificados, 1 débito técnico documentado (SKILL-11) — ver
`validation.md` e Assunção A3.

---

## Success Criteria

- [ ] `bash scripts/verify.sh` limpo (build + lint + test em 0) com a Fase 6 integrada em
      `ScenarioRunner.DefaultSystems()`.
- [ ] As 8 sensores/critérios de verificação de `docs/roadmap/phase-06-skills.md` passam
      exatamente como redigidos (especialização, herança de gene vs habilidade, curva pura,
      teto neutro, mestre melhor, gene muda resultado, oficina rende mais, flag liga/desliga
      muda hash).
- [ ] Nenhuma habilidade herdada diretamente — só `RateGene` é herdado (par de correlações,
      IC95, 200 nascimentos).
- [ ] Golden hashes e baseline de população regenerados se o cenário default mudar de
      comportamento (mesmo padrão da Fase 5).
