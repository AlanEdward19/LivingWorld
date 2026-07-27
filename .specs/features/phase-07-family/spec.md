# Fase 7 — Relações e Famílias — Specification

## Problem Statement

Hoje o "casamento" é um artefato de geração: `PopulationGenerator.PairIntoHouseholds` empareja
adultos por fila (não por afinidade nenhuma) e `NatalitySystem.FindPartner` escolhe qualquer
homem adulto do mesmo household como pai — não existe relação, atração, cortejo, casamento real
nem hereditariedade além de `RateGene` (taxa de habilidade, Fase 6). Sem isso, população não
forma linhagens plausíveis: casais são arranjo de fila, filhos não herdam nada além da taxa de
aprendizado, e não há mecanismo que impeça (ou explique) incesto. Fase 7 fecha o **objetivo
técnico #1**: 100 NPCs, 100 anos, sem LLM, formando famílias e produzindo linhagens rastreáveis
que não colapsam em clones nem viram eugenia disfarçada.

## Goals

- [ ] Relação assimétrica (A→B ≠ B→A) com 4 eixos numéricos que evolui por evento e decai sem
      contato — nunca uma flag booleana de "amizade".
- [ ] Cortejo com rejeição nomeada (`Incesto`, `ForaDaFaixaEtária`, `SemAfinidade`) — motivo
      auditável, não silencioso.
- [ ] Casamento cria household novo; reprodução agenda nascimento no scheduler (nunca varredura
      por tick); hereditariedade separa genético de ambiental por construção (campos distintos,
      nunca o mesmo campo).
- [ ] Diversidade genética da população, medida contra um controle de deriva neutra com a mesma
      seed, nunca cai abaixo do controle.
- [ ] Correlação genética×sucesso tem teto derivado do próprio mundo (canal ambiental desligado),
      não um limiar inventado — e o canal ambiental prova ser causal, não decorativo.

## Out of Scope

| Feature | Reason |
|---|---|
| Catálogo completo de atributos físicos/cognitivos de `npc.md` (força, agilidade, inteligência, memória, etc., 16 atributos) | Nenhum critério do roadmap exige todos — só a separação genético/ambiental e os testes de diversidade/correlação/contrafactual precisam existir. Assunção A1 introduz o mínimo necessário (2 campos novos), mesmo espírito de `RateGene` na Fase 6. Expandir o catálogo é candidato a fase futura. |
| Migração entre assentamentos, LOD, crescimento urbano | Fase 8 (roadmap, "Fora do escopo"). |
| Dinastias, sobrenome, propriedades/títulos transmitidos, memória histórica de linhagem | Fase 10 (roadmap). `genetics-and-family.md` já separa household (esta fase) de linhagem-como-história (Fase 10). |
| Qualquer participação de LLM em cortejo/diálogo de relação | Fase 11 (roadmap). |
| Parentesco além de primeiro grau no check de incesto (meio-irmãos, primos, avós) | Assunção A6 — só pai-filho e irmãos completos (ponteiros já existentes `MotherId`/`FatherId`); genealogia estendida exigiria grafo novo, sem critério do roadmap que peça isso. |
| Poligamia, divórcio, remarriage explícito como mecanismo dedicado | Assunção A9 — casamento é monogâmico 1:1; viuvez permite novo cortejo (não é bloqueio), mas não há ação de "divórcio". Roadmap não pede nenhum dos dois. |
| Cliente web / UI de relação ou árvore genealógica | Fora do caminho crítico (AD-007) — Fase 15. |

---

## Assumptions & Open Questions

| Assumption / decision | Chosen default | Rationale | Confirmed? |
|---|---|---|---|
| A1. Modelo de atributo genético/ambiental mínimo (sem o catálogo completo de `npc.md`) | Dois campos novos em `Npc`: `Vitality` (genético, `0..100`, herdado pela fórmula padrão, influencia resistência à mortalidade — multiplicador em `LifeTable.AnnualMortality` — e chance de concepção) e `Upbringing` (ambiental, `0..100`, fixado no nascimento a partir da riqueza do household na concepção, influencia patrimônio adulto). Registrado como **AD-050**. | Nenhum critério do roadmap exige os 16 atributos de `npc.md`; só precisa existir *um* atributo genético e *um* ambiental com origens distintas no snapshot para os testes de CV/correlação/causalidade/contrafactual terem o que medir. Mesmo padrão de `RateGene` (Fase 6): campo único, não modelo completo. | y |
| A2. Métrica de "sucesso" nos testes de correlação genética×sucesso e causal-ambiental | Patrimônio acumulado (`Wallet`) do NPC aos 30 anos (ou na morte, se antes) — reusa `Money`/`Wallet` já existente (Fase 5), não inventa métrica nova. Registrado como **AD-051**. | `docs/domain/genetics-and-family.md`: "bom gene = boa vida é bug" — usar riqueza (já mensurável, já teste de Fase 5) em vez de um "score de sucesso" novo evita inventar uma função de aptidão disfarçada. | y |
| A3. Onde vive o relacionamento (par ordenado, 4 eixos) | Nova coleção canônica em `WorldState`: dicionário chaveado por `(NpcId origem, NpcId destino)`, populado sob demanda (lazy — só quando dois NPCs se encontram), mesmo molde de `Households`/`Workplaces` (lista + dict). Registrado como **AD-052**. | Popular todos os pares desde o início é O(N²) mesmo para NPCs que nunca se encontram — viola "quem nunca se encontra nunca se conhece" (task 2) e explode memória em população grande. | y |
| A4. Pesos de atração, fórmula de decaimento de relação, limiares de cortejo (duração, requisitos) | Declarados em `FamilyRules` (novo, cenário-driven), nunca literal em C# — mesma convenção R3 de `NeedsRules`/`SkillsRules`/`EconomyRules`. Valores default desta fase são calibração inicial, sujeitos a ajuste (mesmo espírito de `RateGene.Spread`). Registrado como **AD-053**. | Sem isso o critério "pesos exatos de atração" ficaria hardcoded, quebrando o padrão já estabelecido em 5 fases anteriores. | y |
| A5. Tipo do motivo de rejeição de cortejo | Enum fechado `CourtshipRejectionReason { Incesto, ForaDaFaixaEtaria, SemAfinidade }` no Domain, não string livre — mesmo padrão de `ActionType`/`SkillType`. Registrado como **AD-054**. | Motivo nomeado (task 3) precisa ser auditável e testável por igualdade; string livre permitiria typo silencioso. | y |
| A6. Definição operacional de "incesto" | Parentesco de primeiro grau via ponteiros já existentes: pai/mãe-filho (`A.Id == B.MotherId \|\| A.Id == B.FatherId`, nos dois sentidos) ou irmãos completos (`A.MotherId == B.MotherId && A.FatherId == B.FatherId`, ambos não nulos). Meio-irmãos e parentesco mais distante ficam fora (Fora do Escopo). Registrado como **AD-055**. | O critério do roadmap fala em "parentes de primeiro grau"; os ponteiros de pai/mãe já existem no `Npc` — genealogia estendida (avós, primos) exigiria grafo novo sem pedido explícito do roadmap. | y |
| A7. Household do casal recém-casado | Casamento sempre cria um `Household` **novo** — nenhum dos dois cônjuges herda o household de nascimento do outro; ambos saem do household anterior (`LeaveHousehold`/`JoinHousehold` já existentes) e entram no novo. Registrado como **AD-056**. | Task 4 do roadmap ("casar cria um household novo com moradia e estoque próprios") já resolve a ambiguidade "quem fica" — não há disputa de household a arbitrar. | y |
| A8. Redistribuição na dissolução de household (morte de ambos os pais) | Filhos vivos remanescentes entram no household do parente adulto mais próximo disponível (avô/avó ou irmão adulto já com household); sem candidato, cada filho remanescente vira `Head` do próprio household unitário — mesmo fallback já usado em `PopulationGenerator.PairIntoHouseholds` quando não há adulto. | Task 4 pede "redistribui" sem especificar a regra; o fallback já existe no código para o caso análogo (geração inicial sem adultos), reuso evita inventar uma segunda regra. | y |
| A9. Casamento é monogâmico; sem divórcio | Um `Npc` só pode ter um cônjuge por vez (checado antes de iniciar cortejo — NPC já casado não entra em novo cortejo); viuvez (morte do cônjuge) libera para novo cortejo. Não existe ação de "divórcio" nesta fase. Registrado como **AD-060** (numeração final abaixo). | Roadmap não pede poligamia nem divórcio; monogamia é a forma mais simples que ainda produz "casais formam família" (task 4) sem abrir uma segunda dimensão de regra social não pedida. | y |
| A10. Risco de parto (mãe/criança) | Probabilidade fixa por cenário: `FamilyRules.MaternalDeathRisk` / `InfantDeathRisk`, resolvida no evento de nascimento já agendado (mesmo hook onde `NatalitySystem.HandleEvent` cria o bebê) — sem modelo médico, mesmo padrão de `LifeTable.AnnualMortality` (probabilidade declarada). Registrado como **AD-058**. | Task 5 pede "parto tem risco para mãe e criança" sem especificar mecanismo; reusar o padrão de probabilidade declarada já validado em 6 fases evita inventar um segundo modelo de risco. | y |
| A11. Cenário de deriva neutra e cenário contrafactual de household | Ambos são **variações de configuração** do harness de teste (flags novas em `FamilyRules`/parâmetro opcional em `ScenarioRunner.Create`, mesmo padrão de `EconomyRules.Enabled`/AD-047), não sistemas de produção novos: deriva neutra = ignorar atração/cortejo (acasalamento aleatório dentro da janela de fertilidade) e mortalidade não reagir a `Vitality`/`Health` (seleção desligada); contrafactual = mesmo genoma (`Vitality`/`RateGene` fixados) injetado em household com estoque/patrimônio alto vs baixo. Registrado como **AD-059**. | Task 9/10 e os critérios de verificação pedem esses cenários só para comparação estatística — não são comportamento de jogo novo, são composição diferente de regras já existentes (mesmo raciocínio de T25/T26 da Fase 5, AD-047). | y |
| A12. Dimensões implícitas sem requisito nesta fase | Failure/partial-failure, idempotência/retry, auth/rate-limit, dependência externa → **N/A**: sistemas de tick determinístico em processo único, sem I/O, sem API de jogador (mesmo motivo de fases anteriores). Concorrência/ordenação → coberta por FAM-01/FAM-09 (par ordenado desempata por `NpcId.Value`, mesma convenção de `EventScheduler`). Observabilidade → coberta por `ctx.LogEvent` em casamento/nascimento/rejeição de cortejo (mesmo padrão de `WorldEventKind.Birth`). Data lifecycle → relação nunca expira por si, só decai (FAM-04); household dissolvido sai da lista canônica (padrão já existente). | Nenhuma dessas dimensões tem superfície nova além do que `NatalitySystem`/`MortalitySystem`/`Household` já cobrem. | y |

**Open questions:** nenhuma — todas resolvidas ou logadas acima. Novas entradas
`docs/decisions-log.md`: **AD-050 a AD-060** (sequência final: AD-050 Vitality/Upbringing,
AD-051 métrica de sucesso, AD-052 armazenamento de relação, AD-053 `FamilyRules` cenário-driven,
AD-054 enum de rejeição, AD-055 definição de incesto, AD-056 household novo no casamento,
AD-057 redistribuição na dissolução, AD-058 risco de parto, AD-059 cenários como configuração,
AD-060 monogamia/sem divórcio).

---

## User Stories

### P1: Relação assimétrica com eixos numéricos ⭐ MVP

**User Story**: Como designer do mundo, quero que dois NPCs acumulem confiança, afeto, respeito
e dívida de forma assimétrica (A→B ≠ B→A), formada por proximidade/convivência e alterada por
eventos nomeados, para que exista substrato numérico real sobre o qual cortejo e casamento
decidem — nunca uma flag "são amigos".

**Why P1**: Toda a fase depende disso: cortejo, casamento e reprodução (P1 seguintes) leem a
relação para decidir. Sem substrato, não há o que ler.

**Acceptance Criteria**:

1. WHEN dois NPCs nunca se encontraram THEN o sistema SHALL não ter nenhuma entrada de relação
   entre eles (par ausente, não par com valor 0 — "quem nunca se encontra nunca se conhece").
2. WHEN dois NPCs convivem (mesmo `Household` ou mesmo `Workplace`/`CellCoord` prolongado, regra
   declarada em `FamilyRules`) THEN o sistema SHALL criar (se ausente) e evoluir os 4 eixos
   (Confiança, Afeto, Respeito, Dívida) do par ordenado A→B, independentemente do par B→A.
3. WHEN um evento nomeado ocorre (ajuda, traição, convívio, comércio) THEN o sistema SHALL
   aplicar o delta declarado em `FamilyRules` ao eixo correspondente, na direção correta
   (ex.: traição de A contra B altera `A→B`, não necessariamente `B→A`).
4. WHEN um par de NPCs deixa de conviver por um período declarado em `FamilyRules` THEN os 4
   eixos da relação SHALL decair em direção a um valor neutro, nunca ultrapassando-o.
5. WHEN a relação de A→B é lida em qualquer sistema (cortejo, casamento) THEN o valor lido SHALL
   ser exclusivamente o do par ordenado correto — `A→B` e `B→A` SHALL poder divergir na mesma
   run (assimetria comprovável).

**Independent Test**: Cenário com 2 NPCs que convivem N dias, comparado a um par que nunca se
encontra (mesma seed) — o primeiro par tem relação não-nula, o segundo não tem entrada; um dos
dois NPCs sofre um evento de traição unilateral e só o eixo Confiança na direção da vítima cai.

---

### P1: Atração, cortejo e rejeição nomeada

**User Story**: Como designer do mundo, quero que NPCs elegíveis calculem um score de atração e
entrem em cortejo por um tempo declarado, podendo ser rejeitados com um motivo nomeado
(`Incesto`, `ForaDaFaixaEtária`, `SemAfinidade`), para que a formação de casal seja auditável e
não uma sorte silenciosa.

**Why P1**: É o portão entre "duas pessoas se conhecem" (história anterior) e "casam e têm
filhos" (histórias seguintes) — sem ele, casamento seria arranjo aleatório, o mesmo problema que
a Fase 7 existe para resolver.

**Acceptance Criteria**:

1. WHEN um score de atração é calculado entre dois NPCs elegíveis (vivos, sexo/idade compatíveis
   com reprodução, sem cônjuge — Assunção A9) THEN o sistema SHALL combinar idade, saúde, status
   (profissão/riqueza), habilidade, afinidade cultural e a relação já existente (A3), com pesos
   declarados em `FamilyRules` (A4).
2. WHEN o score de atração entre dois candidatos elegíveis excede o limiar declarado em
   `FamilyRules` THEN o sistema SHALL iniciar cortejo, com duração declarada (não instantâneo).
3. WHEN o cortejo é avaliado entre dois candidatos que são parentes de primeiro grau (Assunção
   A6) THEN o sistema SHALL rejeitar com motivo `Incesto`, **mesmo que score de atração e todo o
   resto seja compatível**.
4. WHEN o cortejo é avaliado entre dois candidatos fora da janela de fertilidade compatível
   (idade abaixo/acima do declarado em `PopulationRules`) THEN o sistema SHALL rejeitar com
   motivo `ForaDaFaixaEtária`.
5. WHEN o cortejo é avaliado entre dois candidatos sem afinidade suficiente (score de atração
   abaixo do limiar após excluir os dois casos acima) THEN o sistema SHALL rejeitar com motivo
   `SemAfinidade`.
6. WHEN o cortejo é bem-sucedido (passa por todos os checks e completa a duração declarada)
   THEN o sistema SHALL registrar o resultado (evento nomeado, mesmo padrão de
   `WorldEventKind.Birth`) antes de acionar o casamento (próxima história).

**Independent Test**: (a) Negativo — cenário de 10 anos sem nenhum casamento entre parentes de
primeiro grau (household comum não gera irmãos coabitando compatíveis o suficiente para tentar).
(b) Positivo — cenário dedicado com dois irmãos adultos coabitando, compatíveis em tudo o mais
(mesma idade, saúde, cultura, sem outro candidato competindo) — o cortejo entre eles SHALL ser
rejeitado com motivo `Incesto` (critério do roadmap: "só o negativo passa também se irmãos nunca
se encontrarem" — este teste prova que o mecanismo existe, não que ele nunca é exercitado).

---

### P1: Casamento, household e reprodução agendada

**User Story**: Como designer do mundo, quero que um cortejo bem-sucedido case o casal em um
household novo, e que a reprodução dependa de janela de fertilidade, saúde, qualidade da relação
e recursos do household — agendando o nascimento no scheduler em vez de varrer por tick — para
que família seja uma consequência determinística e rastreável, não um evento instantâneo.

**Why P1**: É o mecanismo central do objetivo #1 — sem casamento real e reprodução ligada à
relação, não existem linhagens não-clonadas para rastrear.

**Acceptance Criteria**:

1. WHEN um cortejo é bem-sucedido THEN o sistema SHALL criar um `Household` novo (Assunção A7),
   remover ambos os cônjuges de seus households anteriores e adicioná-los ao novo, com estoque
   inicial próprio (declarado em `FamilyRules`, mesmo espírito de AD-046).
2. WHEN um casal casado avalia concepção (frequência `Yearly`, mesmo padrão de
   `NatalitySystem` atual) THEN o sistema SHALL exigir: ambos vivos, mulher dentro da janela de
   fertilidade (`PopulationRules.IsFertileAge`), saúde de ambos acima de um piso declarado,
   qualidade da relação (eixos A3) acima de um limiar declarado, e recursos do household
   (`Household.Stock`) acima de um piso declarado — faltando qualquer um, concepção não ocorre
   neste ano.
3. WHEN a concepção é bem-sucedida (rolagem determinística sobre `AnnualConceptionChance`
   modulada pelos fatores acima) THEN o sistema SHALL **agendar** o nascimento via
   `ctx.ScheduleEvent` na data de parto (gestação, mesmo mecanismo já existente em
   `NatalitySystem`) — nunca produzir o filho na hora nem varrer households por tick à procura
   de gestantes.
4. WHEN a gestação está em curso e a mãe morre antes do parto THEN o evento de nascimento
   SHALL ser tratado como falha silenciosa (sem exceção) — nenhum filho é criado (mesmo
   comportamento já existente: `mother is not { IsAlive: true }` no handler).
5. WHEN o evento de nascimento agendado dispara THEN o sistema SHALL rolar o risco de parto
   (Assunção A10): com probabilidade `MaternalDeathRisk`, a mãe morre no parto; com
   probabilidade `InfantDeathRisk`, a criança nasce morta (ambas as rolagens independentes,
   streams de RNG próprios por evento).
6. WHEN ambos os pais de um `Household` morrem THEN o sistema SHALL dissolver o household e
   redistribuir os membros remanescentes conforme Assunção A8.

**Independent Test**: Cenário com um casal casado, household com estoque suficiente e relação
alta — nascimento ocorre dentro da janela esperada, agendado (visível como `ScheduledEvent`
pendente antes do parto, não como filho já existente no tick da concepção); braço espelho com
household sem estoque — nenhuma concepção ocorre apesar de mesma relação/idade/saúde.

---

### P1: Hereditariedade genético vs ambiental e seleção emergente

**User Story**: Como designer do mundo, quero que o filho herde atributos genéticos dos pais por
uma fórmula declarada com mutação semeada por `NpcId`, com um campo ambiental de origem
totalmente distinta, e que nenhuma função de aptidão artificial exista, para que "quem sobrevive
e se reproduz" defina o pool — não um score de bug.

**Why P1**: É o mecanismo que faz a Fase 7 valer o "objetivo técnico #1" (linhagens não-clonadas)
e cumpre o aviso de design de `genetics-and-family.md` ("genética não é destino").

**Acceptance Criteria**:

1. WHEN um `Npc` nasce de pais conhecidos THEN o sistema SHALL calcular `Vitality` (Assunção A1)
   por `vitalidadeFilho = vitalidadeMãe*pesoMãe + vitalidadePai*pesoPai + mutação`, com o stream
   de RNG da mutação semeado por chave que inclui o `NpcId` do filho (mesmo padrão de
   `RateGene.Inherit`/`Personality.RollFrom`).
2. WHEN o mesmo `Npc` nasce THEN o sistema SHALL calcular `Upbringing` (Assunção A1) a partir da
   riqueza do `Household` na concepção — origem **inteiramente ambiental**, nunca misturada nem
   derivada de `Vitality`/genes dos pais.
3. WHEN o snapshot do mundo é inspecionado THEN `Vitality` (genético) e `Upbringing` (ambiental)
   SHALL existir como campos distintos do `Npc`, nunca o mesmo campo com origem ambígua (mesmo
   critério de separação de `genetics-and-family.md`).
4. WHEN a mortalidade anual de um `Npc` é calculada THEN `Vitality` SHALL influenciar o
   multiplicador de saúde (junto com `Health`, sem substituí-lo) — mas o peso do canal ambiental
   (`Upbringing`/recursos do household) SHALL entrar na mesma ordem de grandeza em algum
   resultado de vida mensurável (patrimônio adulto, Assunção A2), nunca só o gene sozinho
   decidindo o resultado.
5. WHEN a população evolui por 100 anos THEN nenhuma função de "score de aptidão global" ou
   ranking de "melhor NPC" SHALL existir no código — sobrevivência e reprodução emergem só de
   mortalidade (`LifeTable`+`Vitality`+`Health`), fertilidade (`PopulationRules`) e sucesso de
   cortejo (relação+atração), nunca de uma função de fitness explícita somando os três.

**Independent Test**: Harness de N nascimentos com `Vitality`/`Upbringing` dos pais e do filho
registrados — verificar que `Vitality(filho)` correlaciona com `Vitality(pais)` (herdado) e que
`Upbringing(filho)` não correlaciona com `Vitality` dos pais (canais independentes); grep no
código por qualquer campo/método chamado "fitness"/"aptidão"/"score global" SHALL retornar vazio
fora de comentário/doc.

---

### P2: Cenários de controle (deriva neutra e contrafactual de household)

**User Story**: Como quem verifica a fase, quero um cenário de deriva neutra (mesma demografia,
acasalamento aleatório, seleção desligada, mesma seed) e um cenário contrafactual de household
(mesmo genoma em household rico vs pobre), para que diversidade genética e "berço importa" sejam
provados por comparação, não por limiar mágico.

**Why P2**: Sem eles os testes estatísticos do roadmap (CV vs controle, contrafactual) não têm
comparador — mas o mundo funciona (casais formam, filhos nascem, herdam) sem esses cenários
existirem primeiro; eles são o instrumento de prova, não o mecanismo em si.

**Acceptance Criteria**:

1. WHEN o cenário de deriva neutra está ativo (flag `FamilyRules.NeutralDriftEnabled`, Assunção
   A11) THEN o sistema SHALL ignorar atração/cortejo (acasalamento aleatório entre elegíveis na
   janela de fertilidade) e a mortalidade SHALL ignorar `Vitality`/`Health` como fator de
   seleção (só idade/`LifeTable` base) — mesma demografia inicial, mesma seed do braço real.
2. WHEN o cenário contrafactual de household está ativo THEN o sistema SHALL permitir fixar
   `Vitality`/`RateGene` de um NPC semente e instanciá-lo em dois households com riqueza inicial
   diferente (rico/pobre, declarado no cenário de teste), demais condições fixadas.
3. WHEN qualquer um dos dois cenários acima está desativado (default) THEN o comportamento do
   mundo SHALL ser idêntico ao mundo sem a Fase 7 rodando esses cenários — ou seja, os cenários
   são aditivos ao harness de teste, nunca ao caminho default de produção.

**Independent Test**: Rodar o cenário default e o cenário de deriva neutra com a mesma seed,
comparar CV de `Vitality` nas duas populações finais (critério FAM-24); rodar o contrafactual
rico/pobre por 40 anos, 20 seeds, comparar medianas de patrimônio adulto (critério FAM-27).

---

## Edge Cases

- WHEN um NPC nunca encontra nenhum candidato elegível na vida THEN ele SHALL permanecer sem
  cônjuge e sem filhos, sem exceção nem log de erro (mesmo espírito do NPC sem-teto, AD-036).
- WHEN o único candidato de cortejo disponível é parente de primeiro grau THEN o cortejo SHALL
  ser rejeitado (`Incesto`) e o NPC SHALL permanecer disponível para cortejo com outro candidato
  em anos seguintes (a rejeição não bloqueia cortejos futuros com terceiros).
- WHEN um cônjuge morre antes de qualquer concepção THEN o household do casal SHALL dissolver
  (Assunção A8) e o sobrevivente SHALL poder entrar em novo cortejo (Assunção A9 — viuvez não é
  bloqueio permanente).
- WHEN a mãe morre no parto (Assunção A10) THEN a criança (se sobreviver) SHALL nascer com
  `FatherId` apontando ao pai se vivo, `MotherId` apontando à mãe mesmo morta (mesmo padrão já
  existente: referência histórica válida, não ponteiro solto — `AD-031`).
- WHEN dois NPCs se encontram mas nunca satisfazem o limiar de convivência declarado (encontro
  único, não repetido) THEN a relação SHALL permanecer ausente ou nos eixos iniciais mínimos,
  nunca pular direto para valores altos.
- WHEN `Vitality`/`Upbringing` são calculados para a população seed inicial (sem pais conhecidos)
  THEN o sistema SHALL sortear valores iniciais (mesmo padrão de `RateGene.RollInitial`), nunca
  lançar exceção por ausência de pai/mãe.

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
|---|---|---|---|
| FAM-01 | P1: Relação assimétrica — substrato e formação | Design | Pending |
| FAM-02 | P1: Relação — nunca se encontrar = nunca se conhecer | Design | Pending |
| FAM-03 | P1: Relação — evolução por evento nomeado | Design | Pending |
| FAM-04 | P1: Relação — decaimento sem contato | Design | Pending |
| FAM-05 | P1: Relação — assimetria comprovável (A→B ≠ B→A) | Design | Pending |
| FAM-06 | P1: Atração — score combinando idade/saúde/status/habilidade/afinidade/relação | Design | Pending |
| FAM-07 | P1: Cortejo — dura tempo declarado, não instantâneo | Design | Pending |
| FAM-08 | P1: Cortejo — rejeição `Incesto` (parentesco 1º grau) | Design | Pending |
| FAM-09 | P1: Cortejo — rejeição `ForaDaFaixaEtária` | Design | Pending |
| FAM-10 | P1: Cortejo — rejeição `SemAfinidade` | Design | Pending |
| FAM-11 | P1: Cortejo bem-sucedido registrado antes do casamento | Design | Pending |
| FAM-12 | P1: Casamento cria household novo com estoque próprio | Design | Pending |
| FAM-13 | P1: Concepção exige janela de fertilidade + saúde + relação + recursos | Design | Pending |
| FAM-14 | P1: Concepção agenda nascimento no scheduler (nunca varredura) | Design | Pending |
| FAM-15 | P1: Mãe morre antes do parto → nascimento falha silenciosamente | Design | Pending |
| FAM-16 | P1: Risco de parto — mãe e criança | Design | Pending |
| FAM-17 | P1: Morte de ambos os pais dissolve e redistribui household | Design | Pending |
| FAM-18 | P1: Hereditariedade — `Vitality` genético, fórmula + mutação por `NpcId` | Design | Pending |
| FAM-19 | P1: `Upbringing` ambiental — origem distinta de `Vitality` | Design | Pending |
| FAM-20 | P1: Separação genético/ambiental no snapshot (campos distintos) | Design | Pending |
| FAM-21 | P1: Ambiente pesa na mesma ordem de grandeza que genética em resultado de vida | Design | Pending |
| FAM-22 | P1: Sem função de aptidão artificial / ranking de "melhor NPC" | Design | Pending |
| FAM-23 | P2: Cenário de deriva neutra (acasalamento aleatório, seleção desligada) | Design | Pending |
| FAM-24 | P2: Cenário contrafactual de household (mesmo genoma, riqueza diferente) | Design | Pending |
| FAM-25 | P2: Cenários são aditivos ao harness, nunca ao caminho default | Design | Pending |
| FAM-26 | Verificação: linhagens rastreáveis, `esperado = anos/idadeMédiaPrimeiroParto` | Design | Pending |
| FAM-27 | Verificação: população final vs baseline de 20 seeds | Design | Pending |
| FAM-28 | Verificação: toda criança tem `PaiId`/`MãeId` válidos e vivos na concepção | Design | Pending |
| FAM-29 | Verificação: nenhum nascimento com mãe fora da janela de fertilidade | Design | Pending |
| FAM-30 | Verificação: incesto negativo (10 anos, zero casamentos 1º grau) | Design | Pending |
| FAM-31 | Verificação: incesto positivo (cenário dedicado, rejeição `Incesto`) | Design | Pending |
| FAM-32 | Verificação: CV de `Vitality` ≥ CV do controle de deriva neutra | Design | Pending |
| FAM-33 | Verificação: bootstrap `\|r\|` genética×sucesso, IC95 abaixo do mundo sem canal ambiental | Design | Pending |
| FAM-34 | Verificação: distância mesma-genética/seeds-ambientais ≥ distância mesma-ambiental/genéticas-diferentes | Design | Pending |
| FAM-35 | Verificação: contrafactual household — medianas diferem e distribuições se sobrepõem (≥ overlap de genomas extremos) | Design | Pending |
| FAM-36 | Verificação: flag off (hereditariedade + formação de casais) muda `Hash(world)` em 10 anos | Design | Pending |

**ID format:** `FAM-NN`

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 36 total, 0 mapeados a tasks ainda (fase de Specify), 36 pendentes de Design ⚠️

---

## Success Criteria

- [ ] Casais formam por atração/cortejo real (não fila de geração), com rejeição nomeada
      auditável nos 3 motivos declarados.
- [ ] Casamento produz household novo; reprodução agenda nascimento no scheduler; toda criança
      tem `PaiId`/`MãeId` válidos e vivos na concepção, zero órfãos de referência.
- [ ] `Vitality` (genético) e `Upbringing` (ambiental) existem como campos distintos, com
      hereditariedade e mutação semeada por `NpcId` do filho.
- [ ] Diversidade (CV de `Vitality`) da população real nunca fica abaixo do controle de deriva
      neutra na mesma seed.
- [ ] Correlação genética×sucesso tem IC95 do `\|r\|` inteiramente abaixo do mesmo mundo com o
      canal ambiental desligado — teto derivado, não inventado.
- [ ] Contrafactual de household (rico vs pobre, mesmo genoma) mostra medianas diferentes e
      overlap ≥ o overlap entre genomas extremos no mesmo household.
- [ ] Desligar hereditariedade + formação de casais por flag muda `Hash(world)` em 10 anos —
      Fase 7 "entrou na conta".
- [ ] `bash scripts/verify.sh` limpo (build + lint + test em 0) com a Fase 7 integrada em
      `ScenarioRunner.DefaultSystems()`.
