# Fase 5 — Economia — Specification

## Problem Statement

A vila da Fase 4 tem NPCs que decidem, mas nenhuma consequência material liga trabalho a
comida. Fase 5 fecha essa lacuna: recurso vira estoque, estoque vira produção, produção
exige trabalhador, trabalhador ganha salário, escassez vira preço, e o NPC da Fase 4 (que já
morre de fome sem comida) passa a poder evitar isso comprando, produzindo ou empregando-se.
Dinheiro e recursos são inteiros e **conservados** — é a primeira cadeia causal emergente
verificável do projeto (colheita ruim → preço sobe → fome sobe), e sem ela o objetivo #1 (100
anos coerentes) não tem pressão econômica, só demografia e rotina.

## Goals

- [ ] Produção, estoque, emprego, salário e preço emergem de recurso + trabalho + oferta/demanda
      — nenhum preço ou salário é constante de cenário fixa, exceto os parâmetros que os geram.
- [ ] Toda transferência de dinheiro contra recurso é atômica: aplica tudo ou nada, e é
      auditável por injeção de falha em qualquer passo declarado.
- [ ] `soma de moedas do mundo` e `produzido == consumido + estocado + perdido` (por recurso)
      são invariantes exatos, verificáveis a cada tick.
- [ ] Escassez de um recurso (ex.: trigo) se propaga mensuravelmente até fome, com par
      base/tratamento na mesma seed (R4).

## Out of Scope

| Feature | Reason |
| --- | --- |
| Habilidade que aumenta produtividade, progressão de ofício | Fase 6 (roadmap explícito) |
| Rotas comerciais entre cidades e migração econômica | Fase 8 (roadmap explícito) — mercado é local, comerciante não viaja |
| Múltiplos mercados por cidade / mercado por cidade real | Cidade ainda não é entidade real (Fase 8); um único `Workplace` de mercado por assentamento é suficiente para fechar oferta/demanda local nesta fase |
| Propriedade de negócio por NPC (dono, lucro, herança) | `Workplace.Treasury` é o "empregador" (AD-044); dono humano/herança é modelo de propriedade mais amplo, fora do que o roadmap desta fase pede |
| Imposto recorrente / tesouro de governo / política fiscal | Task 10 cita "imposto, tesouro, saque" só como **exemplos nomeados** de evento raro que muda a massa monetária (cunhagem/destruição), não como sistema de tributação recorrente — governo/política é Fase 10+ |
| Poupança e dívida como comportamento do NPC | Citado em `economy.md` como conceito de domínio mais amplo; nesta fase o NPC só tem saldo (`Money`), sem decisão de poupar/tomar emprestado |
| Propriedade de terra/local por NPC (herança, venda de imóvel) | `economy.md` cita propriedade como elemento; aqui local pertence ao mundo (cenário), não a um NPC — venda de local é escopo maior |
| Ação de "roubo"/crime | Citado em `economy.md` como consequência de escassez; aqui a cadeia causal para no aumento de fome (verificável), sem modelar criminalidade |

---

## Assumptions & Open Questions

Resolvidas autonomamente (modo autônomo — sem usuário disponível para discussão); cada uma
virou `AD-NNN` em `docs/decisions-log.md`.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Id do local econômico | `WorkplaceId` novo (long monotônico) — não reusa `LocationId` (Guid, reservado à Fase 8 pela AD-024) | AD-039 — Guid.NewGuid() é banido em Domain/Simulation; reusar o placeholder futuro colidiria com um modelo diferente | n — assumption |
| Catálogo de "local" (casa/loja/celeiro/mercado/oficina) | Uma única entidade `Workplace` cobre produção+estoque+mercado; papel decidido pelo `LocationType` do catálogo do cenário (AD-043) | Mesmo espírito de `ProfessionType`/`LocationType` já existentes: id de catálogo decide o papel, não subclasse nova | n — assumption |
| Ação de compra | `ActionType` ganha `Buy` (task 4/8 exigem estoque em `Household`, que só uma transação de mercado preenche) | AD-040 — `ActionCatalog.Create` já reprova ação sem duração declarada, mesma rede de segurança de NEEDS-13 | n — assumption |
| Capacidade/perda de estoque | Por par `(ResourceType, LocationType)`, cenário-driven (`EconomyRules`/`EconomyCatalog`) | AD-041 — R3, mesmo padrão de `NeedsRules` | n — assumption |
| Frequência de tick dos sistemas novos | Produção/preço = `Daily`; salário = `Monthly`; cunhagem/destruição = evento nomeado, sem sistema periódico; consumo = dentro do `BehaviorDecisionSystem` (`Hourly`) já existente | AD-042 — "frequência mais barata que ainda produz o comportamento" (rules/simulation-determinism.md); reaproveita o hook de conclusão de ação em vez de nova varredura | n — assumption |
| Quem paga salário | O próprio `Workplace` (`Treasury : Money`, alimentada pela venda da própria produção) | AD-044 — evita modelar propriedade/dono; `Treasury` vazia é literalmente "empregador sem caixa" | n — assumption |
| Recurso de comida/água consumido por `Eat` | Declarado no cenário (`EconomyRules.FoodResourceId`/`WaterResourceId`), nunca hardcoded | AD-045 — R3; catálogo de 5 recursos é dado, não constante do motor | n — assumption |
| Vínculo produção ↔ geografia | Recipe de produção de um `Workplace` só produz recurso presente em `MapCell.Resources` da célula onde está (Fase 2, já existente) — local fora do recurso produz 0 mesmo com trabalhador | Reusa o dado geográfico já existente (Fase 2, task 2) em vez de inventar disponibilidade paralela; coerente com "trigo plantado" de `economy.md` | n — assumption |
| Profissões sem produção física (guarda, curandeiro, professor, comerciante) | Empregáveis e assalariadas normalmente; `Workplace` sem recipe declarada produz 0 unidades de recurso, mas ainda conta vaga/emprego/salário | Roadmap não exige que toda profissão produza recurso físico; comerciante em particular opera o `Workplace` de mercado (compra/venda), não uma linha de produção | n — assumption |
| Desempate/ordem de iteração em qualquer coleção nova (`Workplaces`, `Employees`) | Sempre por id ascendente (mesmo padrão de `Household.RemoveMember`, `NatalitySystem`) | rules/simulation-determinism.md — nunca ordem de `Dictionary`/`HashSet` | y (regra já estabelecida) |
| Passos da transação atômica enumerados "por reflexão" | `MarketTransaction` expõe `IReadOnlyList<TransactionStep>` construída explicitamente (array ordenado de records `(string Name, Func<...> Apply)`); o teste de fault-injection itera essa lista via reflexão sobre o membro público, não sobre métodos privados soltos | Mesmo padrão de `ActionCatalog.Create` (enumera `Enum.GetValues<ActionType>()`) e do sweep de `ReferentialIntegritySweep` (cobertura garantida por construção, não por lista mantida à mão) | n — assumption |

**Open questions:** none — todas resolvidas por assumption acima (modo autônomo, sem gray-area
pendente).

---

## User Stories

### P1: Recursos e estoque por local ⭐ MVP

**User Story**: Como motor de simulação, preciso que cada `Workplace` tenha estoque inteiro
por recurso, com capacidade e perda declaradas, para que produção e consumo tenham onde
acontecer sem "recurso solto no ar".

**Why P1**: Sem estoque nenhuma das tasks seguintes (produção, consumo, compra) tem onde
gravar efeito — é a base de dados de toda a fase.

**Acceptance Criteria**:

1. ECON-01: WHEN o cenário declara o catálogo de recursos (trigo, madeira, ferro, água, pedra
   no cenário medieval) THEN todo campo de quantidade de recurso SHALL ser `int`/`long`
   inteiro, nunca ponto flutuante.
2. ECON-02: WHEN um `Workplace` recebe unidades de um recurso (produção ou transação) THEN o
   motor SHALL somar até o limite de `EconomyRules.CapacityOf(resource, locationType)`; o
   excedente acima da capacidade SHALL ser registrado como perda (`WorldEventKind` dedicado ou
   contador auditável), nunca descartado em silêncio.
3. ECON-03: WHEN um tick de perda/deterioração roda (`Daily`) THEN cada unidade de estoque
   SHALL decair pela taxa declarada de `(ResourceType, LocationType)`; taxa 0 é um valor válido
   (recurso não perece, ex.: ferro/pedra).
4. ECON-04: WHEN qualquer id de `Workplace`/`ResourceType`/`LocationType` aparece em qualquer
   referência do mundo THEN o sweep referencial (Fase 3, task 12) SHALL cobrir o tipo novo —
   reprova se `WorkplaceId` não tiver resolver registrado.
5. ECON-05: WHEN o sistema de produção/estoque é desligado por flag do cenário
   (`EconomyRules.Enabled = false`) THEN `Hash(world)` em 10 anos SHALL diferir do mundo com a
   economia ligada, mesma seed (prova que a economia entra na conta).

**Independent Test**: Cenário com 1 `Workplace` recebendo mais recurso que sua capacidade —
excedente aparece como evento de perda, não desaparece do total contábil; par
ligado/desligado da economia com hash diferente.

---

### P1: Produção por local de trabalho

**User Story**: Como motor de simulação, preciso que cada `Workplace` converta trabalho +
entrada (quando declarada) em saída por ciclo de produção, com capacidade finita e sem
produção sem trabalhador, para que a cadeia produção→estoque→consumo exista.

**Why P1**: Task 2 do roadmap; depende só do estoque (história anterior) já existir.

**Acceptance Criteria**:

1. ECON-06: WHEN um `Workplace` com recipe declarada (`EconomyCatalog.RecipeOf(LocationType)`)
   tem `>= 1` NPC empregado e presente no tick de produção (`Daily`) THEN o motor SHALL
   consumir os `Inputs` (se houver, do próprio estoque do `Workplace`) e depositar os
   `Outputs` no estoque do mesmo `Workplace`, escalado pelo número de trabalhadores presentes,
   até `MaxWorkersPerCycle` (capacidade finita).
2. ECON-07: WHEN um `Workplace` com recipe declarada não tem nenhum trabalhador presente no
   tick de produção THEN a produção SHALL ser exatamente 0 — nunca produção "de fundo" sem
   trabalhador.
3. ECON-08: WHEN a recipe de um `Workplace` exige um recurso natural de célula
   (`MapCell.Resources`, Fase 2) que a célula onde ele está **não** possui THEN a produção
   daquele recurso SHALL ser 0, independente de trabalhador presente.

**Independent Test**: Par de `Workplace` idênticos, um com trabalhador presente e outro vazio
no mesmo tick — só o primeiro produz `> 0`; `Workplace` fora de célula com o recurso natural
exigido produz 0 mesmo com trabalhador.

---

### P1: Transação atômica dinheiro↔recurso e injeção de falha por passo

**User Story**: Como motor de simulação, preciso que toda compra/venda seja uma lista
ordenada de passos que aplica tudo ou nada, com um hook de injeção de falha por índice de
passo, para que a atomicidade seja testável mesmo quando passos novos forem adicionados no
futuro.

**Why P1**: É o critério mais citado do roadmap ("dois primeiros são os mais importantes") e
a precondição estrutural para task 8 (compra/venda) e para o critério de atomicidade.

**Acceptance Criteria**:

1. ECON-09: WHEN uma transação de compra/venda executa com sucesso THEN o motor SHALL debitar
   `Money` do comprador, creditar `Money` ao vendedor, debitar o recurso do estoque do
   vendedor e creditar o recurso ao estoque do comprador, todos os quatro efeitos aplicados **
   ou nenhum**.
2. ECON-10: WHEN os passos da transação são consultados THEN eles SHALL estar expostos como
   uma lista ordenada e enumerável (`MarketTransaction.Steps`, `1..N`) — um passo novo
   adicionado no futuro entra na enumeração automaticamente, sem precisar reescrever o teste
   de atomicidade.
3. ECON-11 (débito além do saldo, R1): WHEN o comprador não tem saldo suficiente THEN a
   transação SHALL retornar `Result.Fail` **e** o saldo do comprador e o estoque do vendedor
   SHALL ficar byte-idênticos ao estado imediatamente anterior à tentativa (nunca "saldo não
   fica negativo" — `Money` já garante isso desde a Fase 0).
4. ECON-12 (fault injection por passo): WHEN um hook de teste aborta a transação no passo `i`
   (para cada `i` em `1..N`) THEN `Hash(world)` após o abort SHALL ser idêntico ao hash
   imediatamente anterior à tentativa.
5. ECON-13 (cobertura por construção): WHEN um passo novo é adicionado a
   `MarketTransaction.Steps` sem caso de teste correspondente THEN o teste de fault-injection
   SHALL falhar automaticamente (itera `Steps.Count`, não uma lista de índices mantida à mão).

**Independent Test**: Transação com saldo insuficiente → `Failure` + hash inalterado; loop de
teste que aborta em cada passo `1..N` e compara hash antes/depois, todos idênticos; adicionar
um passo fictício sem tratamento correspondente derruba o teste (prova de cobertura).

---

### P1: Conservação de dinheiro e de recursos

**User Story**: Como motor de simulação, preciso que a soma de todo o dinheiro e o balanço de
todo recurso (produzido/consumido/estocado/perdido) sejam invariantes exatos a cada tick, para
que a economia nunca vaze ou invente valor.

**Why P1**: São os dois critérios que o roadmap chama de "mais importantes da fase" — se
caírem, nada mais importa.

**Acceptance Criteria**:

1. ECON-14 (conservação de dinheiro): WHEN qualquer tick roda THEN `soma de Money de todo
   NPC/Workplace do mundo` SHALL igualar exatamente `inicial + cunhado − destruído` (contador
   monotônico de cunhagem/destruição mantido em `WorldState`), checado **a cada tick** em 10
   anos (gate) e 100 anos (`Category=Scenario`, nightly).
2. ECON-15 (conservação de recurso): WHEN qualquer tick roda THEN, para cada `ResourceType`,
   `produzido acumulado == consumido acumulado + estocado atual + perdido acumulado`, exato,
   mesmo regime de horizonte do ECON-14.

**Independent Test**: Loop de 10 anos ticando o mundo, assert de conservação após cada tick
(reaproveita o padrão de teste de determinismo já existente); par de contadores
produzido/consumido/estocado/perdido auditáveis por recurso.

---

### P1: Consumo diário integrado à Fase 4

**User Story**: Como NPC, retiro do estoque acessível (minha residência) para saciar fome e
sede; sem estoque, a necessidade não é saciada — e a Fase 4 já sabe a consequência (fome
sustentada mata, NEEDS-03).

**Why P1**: Task 4 do roadmap; é o elo que faz a economia importar para o NPC que já existe
desde a Fase 4 — sem ele, produção e estoque seriam contabilidade sem efeito comportamental.

**Acceptance Criteria**:

1. ECON-16: WHEN um NPC completa a ação `Eat` (Fase 4, `ActionCatalog.MaxDurationHours`) THEN
   o motor SHALL debitar 1 unidade de `EconomyRules.FoodResourceId` e 1 de `WaterResourceId`
   do estoque do `Household` do NPC, **se disponível**, e só então restaurar
   `Hunger`/`Thirst` a 100 (mesmo efeito já existente da Fase 4).
2. ECON-17: WHEN o `Household` do NPC não tem o recurso de comida/água disponível no momento
   em que `Eat` completa THEN `Hunger`/`Thirst` SHALL permanecer sem restauração (a Fase 4 já
   trata fome sustentada em 0 — NEEDS-03) — nunca uma exceção nem um valor negativo.

**Independent Test**: NPC com `Household` estocado consome 1 unidade e satisfaz a necessidade;
NPC com `Household` sem estoque completa `Eat` sem restaurar `Hunger`, e morre de fome no
prazo já provado por NEEDS-03 se o quadro persistir.

---

### P2: Emprego com vagas finitas

**User Story**: Como NPC desempregado, posso ocupar uma vaga aberta em um `Workplace`; como
empregador, só posso ter tantos empregados quanto minhas vagas declaradas — contratação,
demissão e desemprego são eventos, nunca mudança de estado silenciosa.

**Why P2**: Task 5 do roadmap; depende de `Workplace` (já modelado nas histórias P1)
existir.

**Acceptance Criteria**:

1. ECON-18: WHEN um NPC desempregado é contratado por um `Workplace` com vaga livre THEN o
   motor SHALL registrar o vínculo (`Npc.Employer : WorkplaceId?`), decrementar a vaga
   disponível e emitir `WorldEventKind.Hired`.
2. ECON-19: WHEN um `Workplace` demite um empregado (evento nomeado — ex.: sem caixa
   sustentado, cenário de teste) ou o empregado morre THEN o vínculo SHALL ser removido dos
   dois lados **e** o evento (`Fired`/`Death`) SHALL ser gravado — nunca um `Npc.Employer`
   apontando para vaga já ocupada por outro.
3. ECON-20 (integridade de vaga, checado a cada tick em 10 anos): WHEN o mundo roda THEN todo
   NPC empregado SHALL apontar para um `Workplace` que existe **e** nenhum `Workplace` SHALL
   ter mais empregados que `MaxVacancies` declarado.

**Independent Test**: Cenário com vagas finitas e mais candidatos que vagas — nenhum
`Workplace` excede `MaxVacancies`; NPC morto some da lista de empregados do `Workplace` no
mesmo tick da morte.

---

### P2: Salário mensal

**User Story**: Como NPC empregado, recebo salário mensal do meu `Workplace`; se ele não tem
caixa, isso vira um evento nomeado — nunca uma exceção engolida nem um saldo negativo.

**Why P2**: Task 6 do roadmap; depende de emprego (história anterior) e de `Money`/transação
(históricos P1) existirem.

**Acceptance Criteria**:

1. ECON-21: WHEN o tick `Monthly` de salário roda THEN cada `Workplace` SHALL tentar debitar
   `WageOf(Profession)` (declarado no cenário) da própria `Treasury` e creditar ao `Npc.Wallet`
   de cada empregado, um por um, ordenado por `NpcId`.
2. ECON-22 (salário sem caixa é evento, R1 + teste de mutação de segurança): WHEN a `Treasury`
   do `Workplace` não tem saldo suficiente para um empregado THEN o motor SHALL emitir
   `WorldEventKind.WageUnpaid` **e** deixar `Treasury` e `Npc.Wallet` byte-idênticos ao estado
   anterior à tentativa (mesmo padrão de ECON-11); um teste desliga essa checagem por flag e
   exige que o critério falhe, provando que ele mede algo de verdade.

**Independent Test**: `Workplace` com caixa suficiente paga todos; `Workplace` com caixa zero
emite `WageUnpaid` para cada empregado, sem alterar saldo de ninguém; par com/sem a checagem
de saldo mostra o critério falhando quando desligada.

---

### P2: Mercado local e formação de preço

**User Story**: Como motor de simulação, preciso que o preço de cada recurso no `Workplace`
de mercado suba quando a oferta cai frente à demanda e desça quando sobra, dentro de uma
faixa declarada, para que escassez tenha consequência mensurável sem preço fixo.

**Why P2**: Task 7 do roadmap; alimenta o critério causal "escassez empurra o preço".

**Acceptance Criteria**:

1. ECON-23: WHEN o tick `Daily` de mercado roda THEN o preço de cada recurso no `Workplace` de
   mercado SHALL ser recalculado a partir de `EstoqueOfertado / DemandaEstimada` (fórmula e
   parâmetros — sensibilidade, piso, teto — declarados em `EconomyRules`, nunca literal em C#).
2. ECON-24: WHEN o preço recalculado ultrapassaria `EconomyRules.PriceFloor`/`PriceCeiling`
   THEN o motor SHALL fazer clamp na faixa declarada do cenário.
3. ECON-25 (causal com controle, R4): WHEN um par de execuções roda na mesma seed, tratamento
   = produção de trigo cortada pela metade a partir de `t0` THEN `preçoTrat[t] >
   preçoBase[t]` SHALL valer em **todo** tick de `[t0, t0+30]`, repetido em 10 seeds, exigindo
   10/10 — direção, sem magnitude nem prazo além do declarado.

**Independent Test**: Cenário base/tratamento (corte de produção) na mesma seed, 10 seeds,
preço do tratamento estritamente maior em todo tick da janela declarada, 10/10.

---

### P2: Cunhagem e destruição de dinheiro

**User Story**: Como motor de simulação, preciso que qualquer variação da massa monetária
total (fora de compra/venda/salário) tenha origem nomeada e rara, para que a conservação de
dinheiro (ECON-14) sempre feche a conta.

**Why P2**: Task 10 do roadmap; sem isso, ECON-14 não teria como contabilizar eventos que
genuinamente mudam a massa monetária (ex.: tesouro do cenário, saque de teste).

**Acceptance Criteria**:

1. ECON-26: WHEN o mundo cunha ou destrói dinheiro (evento nomeado — nunca dentro de uma
   transação de compra/venda/salário) THEN o motor SHALL incrementar/decrementar o contador
   monotônico correspondente em `WorldState` **e** gravar o evento (`WorldEventKind.Minted`/
   `Destroyed`) com a quantidade e a causa nomeada.
2. ECON-27: WHEN `ECON-14` (conservação) é verificado THEN o total SHALL bater exatamente
   contra `inicial + cunhado − destruído` — se um evento de cunhagem/destruição não
   incrementar o contador correspondente, o teste de conservação SHALL detectar a
   divergência (prova de que o mecanismo não é decorativo).

**Independent Test**: Cenário de teste cunha N unidades por evento nomeado — conservação
(ECON-14) segue batendo com o contador ajustado; desligar o incremento do contador (mutação de
teste) faz ECON-14 falhar.

---

### P2: Cenário base/tratamento como dado

**User Story**: Como autor de teste causal, preciso rodar o mesmo cenário com um multiplicador
de produção declarado como dado (não como novo cenário hardcoded), para que os testes de
ECON-25 e do critério de cadeia completa rodem par a par na mesma seed.

**Why P2**: Task 11 do roadmap; é infraestrutura de teste compartilhada por ECON-25 e pelo
critério "cadeia completa com controle" (quebra de safra → mais fome no tratamento).

**Acceptance Criteria**:

1. ECON-28: WHEN um cenário de teste declara `ProductionMultiplier` (por `ResourceType`,
   aplicado a partir de um tick `t0`) THEN o `ScenarioRunner`/harness de teste SHALL aceitar
   esse multiplicador como **dado do cenário**, nunca como um segundo cenário C# hardcoded
   duplicado — mesmo padrão já usado pela Fase 3 (par base/tratamento na mesma seed).
2. ECON-29 (cadeia causal completa, R4): WHEN o par base/tratamento roda com tratamento =
   quebra de safra (trigo cortado) THEN a contagem de NPCs com `Hunger` abaixo do limiar do
   cenário SHALL ser maior no tratamento em 10/10 seeds.

**Independent Test**: Harness aceita `ProductionMultiplier` sem duplicar `ScenarioRunner`;
par base/tratamento (quebra de safra) mostra contagem de NPCs famintos maior no tratamento em
10/10 seeds.

---

## Edge Cases

- WHEN `EconomyRules.SpoilagePerTick` de um recurso é 0 THEN o recurso nunca deteriora —
  comportamento válido (ex.: ferro/pedra), não erro.
- WHEN um `Workplace` recebe recurso além da capacidade declarada THEN o excedente é perda
  **registrada** (ECON-02), nunca silêncio nem exceção.
- WHEN dois NPCs tentam ocupar a mesma última vaga no mesmo tick THEN a ordem de resolução é
  por `NpcId` ascendente (nunca ordem de iteração de coleção não determinística).
- WHEN a transação de compra/venda falha em qualquer passo (saldo insuficiente, estoque
  insuficiente do vendedor, fault injection de teste) THEN **todos** os quatro efeitos
  (ECON-09) ficam sem aplicar — nunca aplicação parcial.
- WHEN um `Workplace` empregador é dissolvido/deixa de existir com empregados ainda
  vinculados THEN o vínculo é removido do lado do NPC (evento `Fired`), nunca um
  `Npc.Employer` órfão (mesmo padrão do sweep referencial, AD-031).
- WHEN o preço calculado por oferta/demanda cairia abaixo do piso ou subiria acima do teto
  declarado THEN o motor faz clamp (ECON-24), nunca preço negativo ou sem limite.
- WHEN a economia inteira está desligada (`EconomyRules.Enabled = false`) THEN nenhuma
  produção, consumo, emprego, salário ou transação roda — e o hash do mundo em 10 anos reflete
  isso (ECON-05).

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| ECON-01 | P1: Recursos e estoque | Design | Pending |
| ECON-02 | P1: Recursos e estoque | Design | Pending |
| ECON-03 | P1: Recursos e estoque | Design | Pending |
| ECON-04 | P1: Recursos e estoque | Design | Pending |
| ECON-05 | P1: Recursos e estoque | Design | Pending |
| ECON-06 | P1: Produção | Design | Pending |
| ECON-07 | P1: Produção | Design | Pending |
| ECON-08 | P1: Produção | Design | Pending |
| ECON-09 | P1: Transação atômica | Design | Pending |
| ECON-10 | P1: Transação atômica | Design | Pending |
| ECON-11 | P1: Transação atômica | Design | Pending |
| ECON-12 | P1: Transação atômica | Design | Pending |
| ECON-13 | P1: Transação atômica | Design | Pending |
| ECON-14 | P1: Conservação | Design | Pending |
| ECON-15 | P1: Conservação | Design | Pending |
| ECON-16 | P1: Consumo diário | Design | Pending |
| ECON-17 | P1: Consumo diário | Design | Pending |
| ECON-18 | P2: Emprego | Design | Pending |
| ECON-19 | P2: Emprego | Design | Pending |
| ECON-20 | P2: Emprego | Design | Pending |
| ECON-21 | P2: Salário | Design | Pending |
| ECON-22 | P2: Salário | Design | Pending |
| ECON-23 | P2: Mercado/preço | Design | Pending |
| ECON-24 | P2: Mercado/preço | Design | Pending |
| ECON-25 | P2: Mercado/preço | Design | Pending |
| ECON-26 | P2: Cunhagem/destruição | Design | Pending |
| ECON-27 | P2: Cunhagem/destruição | Design | Pending |
| ECON-28 | P2: Cenário base/tratamento | Design | Pending |
| ECON-29 | P2: Cenário base/tratamento | Design | Pending |

**ID format:** `ECON-NN`.

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 29 total, 29 mapped to design (pending tasks breakdown), 0 unmapped.

---

## Success Criteria

- [ ] `bash scripts/verify.sh` em 0 (check-docs + build + lint + test).
- [ ] Todos os critérios de `docs/roadmap/phase-05-economy.md` provados por teste
      automatizado (nenhum "por inspeção").
- [ ] Conservação de dinheiro e de recursos (ECON-14/15) passam a cada tick em 10 anos, sem
      exceção.
- [ ] `STATE.md` atualizado com handoff pra Fase 6 e novos `AD-NNN` desta fase (feito pelo
      dono do repositório após a execução).
