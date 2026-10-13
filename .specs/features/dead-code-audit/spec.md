> **Estado: Parcial** (specs-closeout 2026-10-12) - auditoria e maior parte da execucao confirmadas no codigo; gate/build/golden pendentes, ver closeout.md.

# Dead Code Audit Specification

## Problem Statement

O projeto passou por 28 fases de desenvolvimento agent-driven. Ao longo do caminho, versões antigas de componentes (ex.: `ScenarioLoader` vs `ScenarioLoaderV2`) e classes que perderam consumidor podem ter ficado no código sem necessidade. Junto com isso, boa parte dos comentários/`<summary>` do código documenta **em que fase/task/ADR uma linha foi escrita** (ex.: `(task 7)`, `(Fase 16.4)`, `(Fase 8, fix round 1, gap 1 — CITY-01 AC1)`) em vez de explicar o comportamento em si — uma varredura confirmou **266 arquivos** com esse padrão. O usuário revisa PRs em vez de escrever linha a linha, então nunca teve uma varredura dedicada pra achar nada disso. Objetivo: mesmo comportamento observável, com menos código pra manter e comentários que ajudam quem lê depois, não que arquivam a história de desenvolvimento.

## Goals

### Auditoria (concluída — ver `.specs/features/dead-code-audit/report.md`)

- [x] Inventariar todo código em `src/` que não tem consumidor real (nem produção, nem teste) — candidato a remoção direta
- [x] Inventariar pares/famílias de versões antigas coexistindo com a versão nova (padrão `XxxV2`, `XxxLegacy`, sufixos numéricos, ou nomes que sugerem substituição) e documentar quem ainda consome cada uma
- [x] Inventariar comentários/`<summary>` que citam fase/task/ADR/ID de requisito como conteúdo principal em vez de explicar o WHY do comportamento, e propor a reescrita descritiva
- [x] Inventariar duplicação de lógica que poderia virar uma única abstração, sem quebrar comportamento
- [x] Entregar um relatório priorizado (arquivo/linha, evidência, ação recomendada)

### Execução (esta rodada — escopo confirmado com o usuário contra risco de quebrar Fase 28 e fases recentes)

- [x] Remover `ScenarioLoader` (v1) (`DecisionMetrics` mantido, ver P5) — zero consumidor de produção confirmado, sem relação com nenhuma entrega de fase ativa
- [x] Plugar `ColdTierPersistence`/`ColdTierArchive` no ciclo real de save/resume — fecha o gap descoberto de que a compressão de estado frio prometida pela Fase 28 (T1-T3, T20/CMP-01/05) nunca foi chamada em produção
- [x] Consolidar as 4 duplicações de alta confiança do relatório sem mudar comportamento observável (byte-idêntico em golden/scale-sensor onde aplicável)
- [x] Reescrever/remover os comentários fase/task identificados como citação-pura (sem WHY), e podar a citação (mantendo o WHY) nos demais — mesmo comportamento observável, comentário que explica o presente em vez de arquivar o passado

## Out of Scope

Excluído explicitamente desta rodada. Documentado pra não crescer escopo.

| Item | Motivo |
| --- | --- |
| Auditar `tests/` em busca de testes redundantes/obsoletos além dos diretamente órfãos das remoções acima | Foco é código de produção (`src/`); testes órfãos de uma classe removida são consequência natural da remoção, tratados junto quando a remoção acontece |
| Refatoração de estilo/nomenclatura sem relação com uso morto, gap de fase ou comentário poluído | Fora do objetivo "mesmo resultado, menos código, comentário que ajuda" |
| Arquivos gerados (`obj/`, `bin/`, `EfCore/Migrations/*.Designer.cs`, `*.g.cs`) | Não são código de autoria manual; migrations do EF nunca são "mortas" mesmo sem uso ativo (histórico de schema) |
| Dependências de pacote NuGet não usadas | Fora do escopo "classes/código", é auditoria de outra natureza |
| Remover a referência a fase/task de commits, PRs ou `.specs/` histórico | Esses lugares são o registro de projeto por natureza — o problema é comentário **dentro do código-fonte**, não a existência de histórico em si |
| Remover `DecisionSandbox`/`DecisionSandboxRequest`/`DecisionSandboxResult` | É a própria entrega da Fase 28 (T12/T13) — ferramenta de autoria/pesquisa fora do relógio do mundo; uso só-teste é o comportamento esperado do próprio critério de aceite da fase, não código morto |
| Remover `WorldDbContextFactory` | Descoberto por convenção pelo `dotnet ef migrations` (`IDesignTimeDbContextFactory<T>`) — zero referência textual é esperado, não é sinal de morte |
| Decidir o destino de `ExampleCounterSystem` e `Worker` (Workers) | Registrados no relatório como observação para decisão futura do usuário — não fazem parte do escopo de execução confirmado desta rodada |
| Reescrita linha-a-linha de todos os ~888 comentários fase/task em uma única tarefa monolítica | Volume distribuído por 6 projetos; tasks.md quebra por projeto/subpasta para manter tarefas atômicas e revisáveis |

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

### P5: Remover código morto confirmado ⭐ Execução — `ScenarioLoader` CONCLUÍDA, `DecisionMetrics` CANCELADA

**Status real (verificado tentando executar, não só por grep):**

- `ScenarioLoader` (v1): **concluída.** Migrar `PopulationArchitectureTests.cs` pra `ScenarioLoaderV2` falhou de início — o schema V2 (`EconomyEnabled`, dezenas de campos Economy/City/Behavior/Dynamics) não existia nos JSONs legados. Resolvido migrando o conteúdo: `scenarios/default.json` e `scenarios/test-scifi.json` ganharam os blocos Economy/City/Dynamics completos (a pedido explícito do usuário) e tiveram o mapa redimensionado (20x20 e 40x40) pra caber o footprint dos workplaces autorados. `ScenarioLoader` v1 removido do código. Testes rodados nesta sessão: `LongRunning` (4/4, sem `Category=Scenario`) e `Unit` Periods/CityAndBuildingAuthoring/BehaviorScenarioLoader (121/121) verdes. `Integration` completo e `Category=Scenario` ficam para o usuário rodar (AD-009).
- `DecisionMetrics`/`ModeRun`/`ModeComparison`: **cancelada, não é dead code.** `LivingVillageScenarioTests.cs:73-99` (`Doc85_metrics_baseline_is_collected_for_test_living_village`, `Category=Scenario`) usa `DecisionMetrics.CompareFullVsEventDriven` com asserts reais — cai no caso já previsto pela P1 AC3 (consumidor de teste real ≠ código morto). Removido do escopo de execução.

**User Story (histórica, não executada)**: Como mantenedor, quero que `ScenarioLoader` (v1) e `DecisionMetrics`/`ModeRun`/`ModeComparison` saiam do código, para não manter caminho sem consumidor de produção.

**Why P5**: Achado de menor risco NA AUDITORIA — a tentativa real de execução revelou que ambos exigem trabalho fora do escopo de "limpeza" (reescrita de cenário JSON) ou não deveriam ser removidos (consumidor de teste real). Nenhuma ação de remoção acontece nesta rodada para os dois itens abaixo.

**Acceptance Criteria**:

1. WHEN `ScenarioLoader.LoadWorld` é removido THEN `tests/LivingWorld.Tests.LongRunning/Population/PopulationArchitectureTests.cs` SHALL já ter migrado para `ScenarioLoaderV2.LoadWorld` (mesmo comportamento observável do teste) antes da remoção
2. WHEN `DecisionMetrics` (+ `ModeRun`/`ModeComparison`) é removido THEN todo teste que só cobre essa classe (`DecisionMetricsTests.cs`) SHALL ser removido junto; `LivingVillageScenarioTests.cs` (que também referencia a classe) SHALL ser ajustado para não quebrar
3. WHEN a remoção termina THEN a build da solução inteira SHALL compilar sem referência pendente a `ScenarioLoader`/`DecisionMetrics` em `src/` ou `tests/`
4. WHEN o gate rápido (`scripts/test.sh`) roda após a remoção THEN SHALL passar sem nenhum teste novo falhando

**Independent Test**: `dotnet build LivingWorld.sln` limpo + `bash scripts/test.sh` verde após as duas remoções.

---

### P6: Plugar compressão de estado frio em produção ⭐ Execução

**User Story**: Como mantenedor, quero que `ColdTierPersistence`/`ColdTierArchive` realmente participem do ciclo de save/resume do mundo, para que a promessa da Fase 28 (compressão reduz custo real em disco) seja verdadeira em produção, não só em teste isolado.

**Why P6**: Achado crítico desta rodada — gap real entre o que a Fase 28 declarou fechado (`verify.sh` verde) e o que o código de produção realmente executa. Decisão do usuário: plugar, não remover.

**Acceptance Criteria**:

1. WHEN o mundo é salvo via `PersistentWorldRunner`/`SqliteWorldRepository` (ciclo real, não teste direto da classe) THEN `WorldState.ColdArchive` SHALL ser persistido em disco via `ColdTierPersistence.Save`, não descartado
2. WHEN o mundo é retomado (`persist-resume`, ou reidratação equivalente via Api) THEN `ColdTierPersistence.Load` SHALL popular `WorldState.ColdArchive` — dado frio sobrevive a um restart de processo real, não só ao teste unitário da classe
3. WHEN a compressão é plugada THEN o round-trip (salvar → carregar → mundo idêntico) SHALL continuar valendo no ciclo real, com o mesmo teste de round-trip de `ColdTierPersistenceTests.cs` cobrindo agora o caminho de produção (não só a chamada direta)
4. WHEN o sensor de escala/bytes-por-NPC (Fase 9/28) roda após a mudança THEN SHALL continuar dentro do teto declarado — se o teto mudar por efeito legítimo da compressão agora ativa, a mudança SHALL virar decisão registrada em `.specs/STATE.md` (padrão AD-014..017), nunca silenciosa
5. WHEN o formato binário de `ColdTierPersistence` já grava um `Magic`/`FormatVersion` próprio THEN a integração SHALL reutilizar esse contrato existente sem reinventar serialização

**Independent Test**: Rodar `persist-save` seguido de `persist-resume` (CLI de `LivingWorld.Workers`) num cenário com NPCs mortos arquivados no cold tier; confirmar que o arquivo de cold tier existe em disco após o save e que o resume relê exatamente os mesmos dados (comparação de hash/conteúdo).

---

### P7: Consolidar duplicações confirmadas ⭐ Execução

**User Story**: Como mantenedor, quero que as 4 duplicações de alta confiança do relatório virem uma implementação compartilhada, sem mudar nenhum resultado observável.

**Why P7**: Reduz superfície de manutenção com risco controlado — todas as 4 já têm justificativa concreta e comportamento hoje idêntico entre os pares.

**Acceptance Criteria**:

1. WHEN `SqliteWorldRepository` e `SqlitePeriodTemplateRepository` passam a compartilhar o helper de "latest por chave composta"/"lookup exato por versão" THEN as duas consultas SHALL retornar exatamente os mesmos registros de antes (mesma query, mesma ordenação)
2. WHEN `GlobalLayerBuilder` e `CityLayerBuilder` passam a compartilhar o switch-com-fallback THEN a mensagem de `ArgumentOutOfRangeException` e o comportamento de cada `VisualLayerId` SHALL permanecer idêntico
3. WHEN `SettlementFoundingSystem` e `SpatialSettlementFoundingSystem` passam a compartilhar o helper de threshold+scheduling THEN a fórmula de `CityRules.FoundingConcentrationThreshold`/`OrganizationTicks` SHALL produzir o mesmo tick de fundação de antes (byte-idêntico no golden, se aplicável)
4. WHEN `AttentionRules`/`PowerUtilityRules`/`PowerInheritanceRules`/`BodyRules` passam a usar um `Resolve<T>(declared, default)` genérico THEN o valor resolvido para cada regra SHALL ser idêntico ao método específico removido
5. WHEN qualquer uma das 4 consolidações altera um hash canônico (golden/scale-sensor) THEN a mudança SHALL ser tratada como regressão a investigar, nunca regravada sem confirmar que é efeito puramente estrutural (mesmo valor, caminho de código diferente)

**Independent Test**: Golden hash (`tests/golden/world-hashes.json`) e `scale-sensor.json` batem exatamente com o valor pré-consolidação após cada uma das 4 mudanças, rodadas isoladamente.

---

### P8: Higiene de comentários fase/task ⭐ Execução

**User Story**: Como mantenedor, quero que os comentários identificados no relatório como citação-pura sejam reescritos/removidos, e os demais tenham só a citação podada (WHY preservado), para que o código se leia sozinho.

**Why P8**: Segundo pedido explícito do usuário, maior volume (~888 ocorrências / ~193 arquivos) — sem risco funcional (comentário não afeta comportamento), mas precisa de tarefas por projeto/subpasta para ficar revisável.

**Acceptance Criteria**:

1. WHEN um comentário do relatório está classificado "citação-pura, sem WHY" THEN SHALL ser reescrito com a versão sugerida no relatório ou removido, nunca deixado como estava
2. WHEN um comentário está classificado "citação + WHY" THEN SHALL manter a explicação técnica (invariante/constraint/motivo) e remover só o trecho de fase/task/ADR/ID
3. WHEN a limpeza de um projeto termina THEN `dotnet build` desse projeto SHALL continuar limpo (comentário não compila, mas `<summary>` malformado quebra doc-gen se configurado — checar)
4. WHEN a limpeza termina em todos os 6 projetos THEN uma nova varredura do mesmo padrão (`Fase N|task N|ADR-NNNN|XXXX-NN`) SHALL retornar só os casos explicitamente fora de escopo (nenhum dos ~888 originais sobra sem justificativa)

**Independent Test**: Re-rodar o grep do padrão fase/task em `src/LivingWorld.Domain/History/WorldEventKind.cs` (arquivo 100% citação-pura no relatório) e confirmar zero ocorrência remanescente.

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
| AUDIT-01 | P1: Inventário sem consumidor | Specify | Verified - report.md, closeout.md |
| AUDIT-02 | P1: Inventário sem consumidor | Specify | Verified - report.md, closeout.md |
| AUDIT-03 | P1: Inventário sem consumidor | Specify | Verified - report.md, closeout.md |
| AUDIT-04 | P2: Versões antigas coexistindo | Specify | Verified - report.md, closeout.md |
| AUDIT-05 | P2: Versões antigas coexistindo | Specify | Verified - report.md, closeout.md |
| AUDIT-06 | P2: Versões antigas coexistindo | Specify | Verified - report.md, closeout.md |
| AUDIT-07 | P3: Higiene de comentários | Specify | Verified - report.md, closeout.md |
| AUDIT-08 | P3: Higiene de comentários | Specify | Verified - report.md, closeout.md |
| AUDIT-09 | P3: Higiene de comentários | Specify | Verified - report.md, closeout.md |
| AUDIT-10 | P3: Higiene de comentários | Specify | Verified - report.md, closeout.md |
| AUDIT-11 | P4: Oportunidades de consolidação | Specify | Verified - report.md, closeout.md |
| AUDIT-12 | P4: Oportunidades de consolidação | Specify | Verified - report.md, closeout.md |
| EXEC-01 | P5: Remover código morto confirmado | Design | Implemented — gate completo pendente (usuário) |
| EXEC-02 | P5: Remover código morto confirmado | Design | Dropped — DecisionMetrics tem consumidor de teste real |
| EXEC-03 | P5: Remover código morto confirmado | Design | Implemented — gate completo pendente (usuário) |
| EXEC-04 | P5: Remover código morto confirmado | Design | Implemented — gate completo pendente (usuário) |
| EXEC-05 | P6: Plugar compressão de estado frio | Design | Implemented — gate completo pendente (usuário) |
| EXEC-06 | P6: Plugar compressão de estado frio | Design | Implemented — gate completo pendente (usuário) |
| EXEC-07 | P6: Plugar compressão de estado frio | Design | Implemented — gate completo pendente (usuário) |
| EXEC-08 | P6: Plugar compressão de estado frio | Design | Implemented — gate completo pendente (usuário) |
| EXEC-09 | P6: Plugar compressão de estado frio | Design | Implemented — gate completo pendente (usuário) |
| EXEC-10 | P7: Consolidar duplicações confirmadas | Design | Implemented — gate completo pendente (usuário) |
| EXEC-11 | P7: Consolidar duplicações confirmadas | Design | Implemented — gate completo pendente (usuário) |
| EXEC-12 | P7: Consolidar duplicações confirmadas | Design | Implemented — gate completo pendente (usuário) |
| EXEC-13 | P7: Consolidar duplicações confirmadas | Design | Implemented — gate completo pendente (usuário) |
| EXEC-14 | P7: Consolidar duplicações confirmadas | Design | Implemented — gate completo pendente (usuário) |
| EXEC-15 | P8: Higiene de comentários fase/task | Design | Implemented — gate completo pendente (usuário) |
| EXEC-16 | P8: Higiene de comentários fase/task | Design | Implemented — gate completo pendente (usuário) |
| EXEC-17 | P8: Higiene de comentários fase/task | Design | Implemented — gate completo pendente (usuário) |
| EXEC-18 | P8: Higiene de comentários fase/task | Design | Implemented — gate completo pendente (usuário) |

**ID format:** `AUDIT-NN` (fase Specify, concluída) / `EXEC-NN` (fase de execução, esta rodada)

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 30 total (12 AUDIT verificados na auditoria + 18 EXEC desta rodada), tasks.md efêmero removido ao fim (convenção do repo); EXEC-02 dropped, demais implementados — falta só o gate completo (usuário).

---

## Success Criteria

**Auditoria (concluída):**
- [x] Todo tipo público de `src/` (6 projetos) foi classificado em uma das categorias: usado / sem consumidor / par vN / candidato a consolidação / verificar manualmente
- [x] Todo comentário/`<summary>` casando com o padrão fase/task/ADR/ID de requisito em `src/` foi listado com contagem por projeto e proposta de reescrita ou remoção
- [x] O relatório final não recomenda remoção de nada que ainda tenha consumidor de produção real, nem remoção de explicação técnica real (WHY) junto de uma citação de fase
- [x] O caso `ScenarioLoader`/`ScenarioLoaderV2` aparece no relatório com a contagem real de consumidores de cada lado (achado: premissa original do spec estava desatualizada, corrigida no relatório)
- [x] Zero código ou comentário alterado na rodada de auditoria

**Execução (esta rodada):**
- [x] `ScenarioLoader` (v1) removido (`DecisionMetrics` mantido — consumidor de teste real), build limpa, gate rápido verde
- [ ] `ColdTierPersistence`/`ColdTierArchive` plugados no ciclo real de save/resume — round-trip provado fora do teste direto da classe
- [ ] As 4 consolidações de alta confiança aplicadas sem mudar nenhum hash canônico (golden/scale-sensor) fora de uma mudança explicitamente decidida e registrada
- [ ] Comentários fase/task tratados em todos os 6 projetos conforme classificação do relatório (citação-pura → reescrito/removido; citação+WHY → citação podada, WHY mantido)
- [ ] Nenhuma fase anterior (16.x, 28, dynamic-city-growth) perde comportamento observável — mesmos testes de golden/scale-sensor/scenario que já passavam continuam passando
- [ ] O caso `ScenarioLoader`/`ScenarioLoaderV2` citado pelo usuário aparece no relatório com a contagem real de consumidores de cada lado
- [ ] Zero código ou comentário é alterado nesta rodada — entregável é só o relatório (mudança fica para uma execução futura, aprovada item a item)
