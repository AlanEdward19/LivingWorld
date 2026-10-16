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
