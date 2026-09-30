# Dead Code Audit Specification

## Problem Statement

O projeto passou por 28 fases de desenvolvimento agent-driven. Ao longo do caminho, versões antigas de componentes (ex.: `ScenarioLoader` vs `ScenarioLoaderV2`) e classes que perderam consumidor podem ter ficado no código sem necessidade. Junto com isso, boa parte dos comentários/`<summary>` do código documenta **em que fase/task/ADR uma linha foi escrita** (ex.: `(task 7)`, `(Fase 16.4)`, `(Fase 8, fix round 1, gap 1 — CITY-01 AC1)`) em vez de explicar o comportamento em si — uma varredura confirmou **266 arquivos** com esse padrão. O usuário revisa PRs em vez de escrever linha a linha, então nunca teve uma varredura dedicada pra achar nada disso. Objetivo: mesmo comportamento observável, com menos código pra manter e comentários que ajudam quem lê depois, não que arquivam a história de desenvolvimento.

## Goals

- [ ] Inventariar todo código em `src/` que não tem consumidor real (nem produção, nem teste) — candidato a remoção direta
- [ ] Inventariar pares/famílias de versões antigas coexistindo com a versão nova (padrão `XxxV2`, `XxxLegacy`, sufixos numéricos, ou nomes que sugerem substituição) e documentar quem ainda consome cada uma
- [ ] Inventariar comentários/`<summary>` que citam fase/task/ADR/ID de requisito como conteúdo principal em vez de explicar o WHY do comportamento, e propor a reescrita descritiva
- [ ] Inventariar duplicação de lógica que poderia virar uma única abstração, sem quebrar comportamento
- [ ] Entregar um relatório priorizado (arquivo/linha, evidência, ação recomendada) para o usuário decidir o que remover/reescrever — este spec **não altera nada sozinho**

## Out of Scope

Excluído explicitamente desta rodada. Documentado pra não crescer escopo.

| Item | Motivo |
| --- | --- |
| Executar a remoção do código identificado | Fase separada, sob aprovação item a item do usuário (fora do "apenas Specify" pedido) |
| Reescrever de fato os comentários identificados como poluição de fase/task | Mesmo motivo — o relatório lista e propõe a versão descritiva, a aplicação em massa é execução futura aprovada pelo usuário |
| Auditar `tests/` em busca de testes redundantes/obsoletos | Foco é código de produção (`src/`); testes órfãos de uma classe removida são consequência natural da remoção, tratados junto quando a remoção acontecer |
| Refatoração de estilo/nomenclatura sem relação com uso morto ou comentário poluído | Fora do objetivo "mesmo resultado, menos código, comentário que ajuda" |
| Arquivos gerados (`obj/`, `bin/`, `EfCore/Migrations/*.Designer.cs`, `*.g.cs`) | Não são código de autoria manual; migrations do EF nunca são "mortas" mesmo sem uso ativo (histórico de schema) |
| Dependências de pacote NuGet não usadas | Fora do escopo "classes/código", é auditoria de outra natureza |
| Remover a referência a fase/task de commits, PRs ou `.specs/` histórico | Esses lugares são o registro de projeto por natureza — o problema é comentário **dentro do código-fonte**, não a existência de histórico em si |

---

## Assumptions & Open Questions

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Definição de "sem consumidor real" | Nenhuma referência ao símbolo fora do próprio arquivo, em `src/` OU `tests/` (contagem de referência, não apenas grep textual — evita falso-positivo de nome comum) | Uma classe só usada em teste ainda é "usada" (cobertura), não é código morto — só é candidata se nem produção nem teste chamam | n |
| Como classificar um par vN/vN+1 quando ambos têm consumidor ativo | Documentar como "convivência ativa, não remover sem decisão de negócio" — não é código morto, é decisão de produto | Caso do `ScenarioLoader`/`ScenarioLoaderV2`: v1 já tem 4 consumidores de produção fora de si mesmo (não é código morto), mas é candidato a uma futura decisão consciente de consolidação | n |
| Ferramenta de detecção | Análise por grafo de referências (grep estrutural por símbolo + confirmação manual do resultado antes de listar), sem depender de analisador Roslyn dedicado ainda não presente no projeto | Nenhum analisador de "unused symbol" solution-wide está configurado hoje; grep bem-targetado é suficiente pra uma primeira passada e é auditável no relatório | n |
| Nível de confiança exigido para entrar no relatório como "remover" | Só entra como remoção direta recomendada se zero referência em `src/` e `tests/` (fora definição/using); caso contrário vira "revisar" com a lista de consumidores anexada | Evita recomendar remoção de algo que na verdade é usado via reflection/DI/nome de string (comum em `ISimulationSystem`, endpoints minimal API) — esses casos exigem checagem manual extra listada no relatório | n |
| Escopo de projetos | Todos os 6 projetos de `src/` (Domain, Simulation, Infrastructure, Api, AI, Workers) | Cobertura completa da árvore de produção reorganizada no refactor de arquitetura em andamento | n |
| O que conta como "comentário poluído por fase/task" | Comentário/`<summary>` cujo conteúdo principal é uma referência a fase/task/ADR/ID de requisito (`(task 7)`, `(Fase 16.4)`, `(Fase 8, fix round 1, gap 1 — CITY-01 AC1)`, `ADR-0018`) **sem** explicar uma invariante, constraint ou motivo não-óbvio junto — se a referência acompanha uma explicação real do WHY, ela não entra na lista de reescrita, só a marca "referência de fase pode sair, o resto do comentário fica" | O pedido do usuário é sobre comentário que "avisa que veio em X fase" em vez de descrever; um comentário que também explica uma decisão não-óbvia não é o mesmo problema — cirurgia na citação, não remoção do comentário inteiro | n |
| Comentário sem nenhum WHY não-óbvio (nem citação de fase, só descreve o que o código já diz) | Fora do escopo desta spec — é um problema de estilo geral ("não comentar o óbvio"), não o padrão específico de poluição por fase/task que o usuário apontou | Manter o escopo fechado no que foi pedido evita a auditoria virar "reescrever todo comentário do projeto" | n |

**Open questions:** nenhuma pendente sem registro acima — tudo resolvido ou assumido explicitamente.

---

## User Stories

### P1: Inventário de código sem consumidor ⭐ MVP

**User Story**: Como mantenedor do projeto, quero uma lista de classes/métodos públicos em `src/` sem nenhuma referência real (fora de teste ou produção), para poder remover com segurança sem quebrar nada.

**Why P1**: É o núcleo do pedido — "código velho que não é usado" é o caso mais simples e mais seguro de agir.

**Acceptance Criteria**:

1. WHEN a auditoria roda sobre um projeto de `src/` THEN o relatório SHALL listar todo tipo público (classe/record/interface/enum) sem referência fora da própria declaração, com caminho de arquivo e linha
2. WHEN um tipo aparentemente sem referência é na verdade invocado via nome de string (reflection, DI por convenção, rota minimal API, `[Trait]`/atributo) THEN o relatório SHALL marcar esse caso como "verificar manualmente" em vez de "remover", citando o padrão de invocação indireta suspeito
3. WHEN um tipo só é referenciado em `tests/` e nunca em `src/` THEN o relatório SHALL classificá-lo como "teste órfão de produção" (não "remover"), já que remover a produção associada é decisão separada

**Independent Test**: Rodar a auditoria isolada em `src/LivingWorld.Simulation` e conferir manualmente 5 itens listados como "sem consumidor" — nenhum deve ter um caller real.

---

### P2: Inventário de versões antigas coexistindo (padrão vN)

**User Story**: Como mantenedor, quero saber quais componentes têm uma versão "antiga" e uma "nova" convivendo (ex.: `ScenarioLoader`/`ScenarioLoaderV2`), e quem ainda chama cada uma, para decidir se dá pra aposentar a antiga.

**Why P2**: Motivador explícito do pedido do usuário; mais raro que P1 mas de maior impacto quando existe (arquivo inteiro duplicado).

**Acceptance Criteria**:

1. WHEN a auditoria varre `src/` THEN o relatório SHALL listar todo par de nomes que sugere sucessão (`XV2`, `XLegacy`, `XOld`, `XV1`/`X` coexistindo, ou docstring que menciona substituição/depreciação)
2. WHEN um par é encontrado THEN o relatório SHALL listar, para cada lado do par, a contagem de consumidores de produção (fora do próprio arquivo e de `tests/`)
3. WHEN a versão antiga tem zero consumidores de produção THEN o relatório SHALL recomendar remoção direta; WHEN tem um ou mais THEN o relatório SHALL recomendar "convivência ativa — decisão de produto", listando os consumidores encontrados (não decide sozinho)

**Independent Test**: Conferir a entrada do relatório para `ScenarioLoader`/`ScenarioLoaderV2` — já sabemos hoje que `ScenarioLoader` (v1) tem consumidores em `WorldPreviewEndpoints`, `CityScenarioLoader`, `ExtraordinaryRuntimePlan` e `PeriodDefinitionValidator`; o relatório deve refletir exatamente isso, não recomendar remoção cega.

---

### P3: Higiene de comentários — tirar rastro de fase/task, deixar o WHY

**User Story**: Como mantenedor, quero que comentários e `<summary>` descrevam comportamento/invariante/motivo em vez de "isso foi feito na fase X/task Y", para que o código se leia sozinho sem precisar do histórico de desenvolvimento.

**Why P3**: Confirmado em 266 arquivos de `src/` — maior volume entre todos os achados desta auditoria, e o segundo pedido explícito do usuário.

**Acceptance Criteria**:

1. WHEN a auditoria varre um comentário/`<summary>` em `src/` THEN o relatório SHALL sinalizar toda ocorrência que casa com o padrão fase/task/ADR/ID de requisito (`Fase N`, `task N`, `ADR-NNNN`, `XXXX-NN` estilo `CITY-01`), citando arquivo e linha
2. WHEN a citação de fase/task vem acompanhada de uma explicação real do WHY (constraint, invariante, motivo de design) no mesmo comentário THEN o relatório SHALL propor só a remoção do trecho de citação, preservando a explicação
3. WHEN o comentário inteiro é só a citação de fase/task sem nenhum WHY adicional THEN o relatório SHALL propor a reescrita completa (versão sugerida) ou a remoção do comentário, conforme o comentário agregue ou não informação além do que o código já expressa
4. WHEN o relatório lista as ocorrências THEN SHALL agrupar por projeto (`src/LivingWorld.*`) com contagem total, pra dar noção de volume antes de qualquer decisão de aplicar em massa

**Independent Test**: Conferir a entrada do relatório para `src/LivingWorld.Domain/Population/Body/BodyRules.cs` (ou outro arquivo com `(Fase N)` conhecido) e validar que a sugestão de reescrita preserva qualquer invariante técnica citada no comentário original.

---

### P4: Oportunidades de consolidação (duplicação virando uma coisa só)

**User Story**: Como mantenedor, quero que a auditoria aponte lógica duplicada entre arquivos diferentes que poderia virar uma única implementação compartilhada, para reduzir superfície de manutenção.

**Why P4**: Mais subjetivo e de maior risco de falso-positivo que os anteriores — vale como aceno de oportunidade, não como lista de ação imediata.

**Acceptance Criteria**:

1. WHEN dois ou mais arquivos têm assinatura/estrutura de método muito similar operando sobre tipos relacionados THEN o relatório SHALL listar o par como "candidato a consolidação" com uma frase justificando a similaridade
2. WHEN um candidato de consolidação é listado THEN o relatório SHALL indicar o nível de confiança (alto/médio/baixo) e não prescrever a forma final da abstração — só sinaliza a oportunidade

**Independent Test**: Revisão humana de 3 candidatos listados — cada um deve ter uma justificativa concreta (não "parecem parecidos"), mesmo que a decisão final de consolidar seja do usuário.

---

## Edge Cases

- WHEN um tipo é público apenas para ser testado (não usado por nenhum outro tipo de produção) THEN o relatório SHALL diferenciar esse caso ("só existe pra ser testado" não é o mesmo que "código morto" — pode ser um value object/DTO legítimo)
- WHEN um tipo é referenciado só dentro de um arquivo de migration do EF Core (`EfCore/Migrations/*`) THEN o relatório SHALL excluí-lo (histórico de schema nunca é "morto")
- WHEN um método é `public` numa classe `internal`/`sealed` sem consumidor externo ao assembly THEN o relatório SHALL registrar isso como observação de visibilidade excessiva, não como remoção
- WHEN a mesma investigação já foi registrada em memória/decisão anterior do projeto (ex.: ADRs, `.specs/STATE.md`) THEN o relatório SHALL citar essa decisão em vez de recomendar algo contrário sem contexto

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| AUDIT-01 | P1: Inventário sem consumidor | Execute | Pending |
| AUDIT-02 | P1: Inventário sem consumidor | Execute | Pending |
| AUDIT-03 | P1: Inventário sem consumidor | Execute | Pending |
| AUDIT-04 | P2: Versões antigas coexistindo | Execute | Pending |
| AUDIT-05 | P2: Versões antigas coexistindo | Execute | Pending |
| AUDIT-06 | P2: Versões antigas coexistindo | Execute | Pending |
| AUDIT-07 | P3: Higiene de comentários | Execute | Pending |
| AUDIT-08 | P3: Higiene de comentários | Execute | Pending |
| AUDIT-09 | P3: Higiene de comentários | Execute | Pending |
| AUDIT-10 | P3: Higiene de comentários | Execute | Pending |
| AUDIT-11 | P4: Oportunidades de consolidação | Execute | Pending |
| AUDIT-12 | P4: Oportunidades de consolidação | Execute | Pending |

**ID format:** `AUDIT-NN`

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 12 total, 0 mapped to tasks (Tasks phase pulada — escopo Medium, passos viram implícitos no Execute), 12 unmapped ⚠️ (esperado nesta etapa; mapeamento acontece ao entrar em Execute)

---

## Success Criteria

- [ ] Todo tipo público de `src/` (6 projetos) foi classificado em uma das categorias: usado / sem consumidor / par vN / candidato a consolidação / verificar manualmente
- [ ] Todo comentário/`<summary>` casando com o padrão fase/task/ADR/ID de requisito em `src/` foi listado com contagem por projeto e proposta de reescrita ou remoção
- [ ] O relatório final não recomenda remoção de nada que ainda tenha consumidor de produção real, nem remoção de explicação técnica real (WHY) junto de uma citação de fase
- [ ] O caso `ScenarioLoader`/`ScenarioLoaderV2` citado pelo usuário aparece no relatório com a contagem real de consumidores de cada lado
- [ ] Zero código ou comentário é alterado nesta rodada — entregável é só o relatório (mudança fica para uma execução futura, aprovada item a item)
