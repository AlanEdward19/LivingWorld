# Handoff 3/4


- **RETOMADO (2026-08-23) — os 2 fixes que estavam em background quando a sessão pausou foram
  revisados e commitados**: ver detalhe abaixo. Ainda pendente: 1 feature nova
  (`real-household-workplace-buildings`) com Design aprovado mas Tasks/Execute ainda não iniciada.

### Fixes commitados nesta retomada (eram 2 agentes background mortos antes do commit final)

Contexto original: usuário testou ao vivo os fixes anteriores de "seguir NPC entre escopos"
(`497a09d`, `379a665`) e de "cidade colada" (`a6584ad`, `822ba4a`) e **ambos os bugs originais
ainda ocorriam**, cada um por uma causa raiz DIFERENTE. Dois agentes de fix foram disparados em
background, cada um confirmou a causa raiz + testes passando, mas foram interrompidos (`TaskStop`)
um passo antes do commit final — as edições ficaram no working tree, não commitadas.

1. **Fix A — seleção/highlight de NPC seguido some quando ele cruza para um escopo onde está
   "pooled" (ainda não materializado) — commit `e024465`**: `MapView.refreshEntities` tratava
   "sem marcador desenhável no novo escopo" como "não existe mais" e limpava a seleção — um NPC
   pooled legitimamente não tem `NpcVisual`/marcador (`PoolNpcIds` reportado separado), então
   cruzar pra um escopo onde ele está pooled disparava limpeza incorreta. Fix: antes de limpar,
   `MapView` agora consulta a mesma `NpcInspection` que o `NpcInspector` já usa pra distinguir
   pooled de genuinamente ausente (`POOLED_LOD`), e só limpa quando a inspeção confirma que não é
   nenhum dos dois. `simulationStore.refreshNpcInspection` também cacheia `null` sincronamente
   quando não há fonte de inspeção configurada, pra `npcInspectionOf` dar veredito imediato em vez
   de ficar `undefined` pra sempre. Revisão confirmou que o diff era limpo e auto-contido nos 3
   arquivos que o agente tocou (`web/src/components/MapView.tsx`,
   `web/src/state/simulationStore.ts`, `web/tests/MapView.test.tsx`); `web/src/data/contracts.ts`
   tinha o `POOLED_LOD` export entrelaçado com ~10 hunks não relacionados (Stage-4 pré-existente,
   `rest`/`food`/`ProcessVisual`/etc.) — isolado via patch manual (`git apply --cached
   --unidiff-zero` num hunk de 4 linhas) em vez de `git add` do arquivo inteiro. `selectionStore.ts`
   não tinha edição nenhuma (a hipótese original de tocar esse arquivo não se confirmou no diff
   real do agente). `npx vitest run`: 409 passed, 0 failed.
2. **Fix B — cidade nova ainda funda colada numa existente — commit `077ed50`**: o sistema NOVO
   (`SpatialSettlementFoundingSystem`, `a6584ad`/`822ba4a`) só cobre overflow de prédios; households
   ainda não têm `Building` real, então o crescimento passa quase sempre pelo sistema ANTIGO,
   `SettlementFoundingSystem` + `FoundingSitePicker`, que nunca teve checagem de distância mínima
   (só evitava a MESMA célula exata). Fix: `FoundingSitePicker.Pick` agora rejeita qualquer
   candidato a menos de `AbsorptionRingCells` de qualquer OUTRA cidade existente (reusa
   `OverflowClusterFinder.IsWithinAbsorptionRangeOfAnyOtherCity`), devolve `null` (falha honesta)
   se nenhuma célula do mapa respeita essa distância. `FoundingSitePicker.cs` e
   `FoundingSitePickerTests.cs` eram inteiramente novos/untracked (sem risco de entrelaçamento) —
   commitados como estavam. `NpcEndpointsTests.cs`/`NpcInspectionDtoCoverageTests.cs` (modificados
   no `git status`) confirmados como trabalho Stage-4 pré-existente NÃO relacionado a este fix —
   deixados de fora do commit. Gate estreito
   (`--filter "Category!=Scenario&FullyQualifiedName~Cities"`): 226 passed, 0 failed. Gate completo
   (`bash scripts/test.sh`, backend+frontend): backend 1527 passed / 6 failed, frontend 409 passed /
   0 failed. Das 6 falhas backend: 2 já documentadas/aceitas nesta mesma seção antes desta rodada
   (`ScarcityPriceCausalTests`/`FamineCausalChainTests`, re-tuning de limiares pendente); as outras
   4 (`ProductionCompositionTests.Production_living_system_order_is_explicit_and_stable` + 3
   `GoldenHashesTests`) são causadas por OUTRA camada de trabalho Stage-4 já não-commitada
   (`SpatialSettlementFoundingSystem`/`RelocationArrivalSystem` já registrados no `Program.cs`
   pré-existente-dirty) — confirmado não relacionado a este fix (que não toca registro de sistema
   nenhum) antes de commitar. Ver `.specs/features/dynamic-city-growth/tasks.md` (FixT10) pro
   detalhe completo.
- **Observação adicional do usuário (ainda não endereçada, é o gap `real-household-workplace-buildings` mesmo)**: cidade cresceu 3x3→4x4 mas os NPCs que construíram casas fora da cidade NÃO
  foram incorporados nesse crescimento — confirmado pela investigação: `OwnedBuildingFootprintBoxesWithOwners` só olha `world.Buildings`, e household/NPC "morando fora" hoje é só uma
  coordenada sem `Building` nenhum atrás. Não tem conserto possível no código de crescimento até
  `real-household-workplace-buildings` (spec+design já aprovados, Tasks já escrito, Execute NÃO
  iniciado — ver seção própria abaixo) dar a eles um `Building` real.

### Feature parada, pronta pra Execute quando o usuário disser — `real-household-workplace-buildings`

- **Onde**: `.specs/features/real-household-workplace-buildings/` — `spec.md` e `design.md`
  aprovados pelo usuário, `tasks.md` escrito (5 tasks, 3 fases: T1/T3/T4 paralelas, T2 depende de
  T1, T5 gate final). **Execute NÃO começou** — usuário pediu pra pausar isso e focar nos bugs
  reportados ao vivo (seção "EM ANDAMENTO AGORA" acima) antes de retomar.
- **O que resolve**: households e workplaces nunca tiveram um `Building` real (só uma coordenada
  bare) — cidade no mapa não mostra casa/local de trabalho nenhum. Sem backfill (só mundos novos,
  por pedido explícito do usuário), 1 casa por household sem compartilhamento, posição resolvida
  UMA vez na criação e escrita igual no `Building` e no household/workplace (nunca duas fontes que
  podem dessincronizar).
- **Next step**: quando o usuário disser pra retomar, dispatch Phase 1 (T1 confirma/cria tipo de
  prédio "casa" no catálogo, T3 workplaces do cenário default, T4 workplaces autorados +
  reordenação cidade-antes-de-workplace — os 3 em paralelo), depois Phase 2 (T2, households), depois
  Phase 3 (T5, gate completo) — mesmo padrão de dispatch por fase já usado em `dynamic-city-growth`.

---

- **Feature**: `dynamic-city-growth` — `.specs/features/dynamic-city-growth/` (spec.md, design.md, tasks.md, validation.md) — **fechada e validada** (PASS, round 4), mas com 1 causa raiz nova encontrada em teste ao vivo pós-fechamento (Fix B acima) que a extrapola pro sistema de fundação ANTIGO. Sessão iniciada a partir de um bugfix de visual (fase-15.1-stage-4) onde o usuário pediu, além do bugfix, uma feature nova: cidade sem espaço livre constrói fora dos bounds (overflow), bounds crescem pra absorver overflow próximo, cluster longe o bastante e com população real de verdade (não só prédios) funda cidade nova — reusando a MESMA fórmula/limiar de `SettlementFoundingSystem`, nunca um limiar de contagem de prédios (correção explícita do usuário: "1 casa não monta 1 cidade, 1 pessoa não monta uma cidade, uma sociedade sim").
- **Post-ship fix (2026-08-23, achado em produção pelo usuário)**: cidade nova fundada ("UrVal") apareceu com os muros literalmente colados/sobrepostos aos de uma cidade já existente. Causa raiz: `CityBoundsResolver.Resolve` crescia os bounds de uma cidade só a partir dos próprios prédios de overflow, sem NUNCA checar contra os bounds de nenhuma outra cidade — duas cidades fundadas a uma distância segura podiam crescer uma em direção à outra, tick após tick, até se tocar/sobrepor. Causa compõe: `SpatialSettlementFoundingSystem.HandleEvent` reverificava o limiar de concentração no disparo do evento, mas nunca reverificava a distância de absorção (só checada uma vez, no agendamento) — se outra cidade crescesse durante a espera de `OrganizationTicks`, a fundação seguia mesmo assim. Fix 1 (`a6584ad`): `CityBoundsResolver.Resolve`/`SpatialBoundsResolver.ResolveCity` ganharam um `otherCityBoundsToAvoid` opcional — qualquer prédio de overflow que empurrasse os bounds pra dentro de `AbsorptionRingCells` de outra cidade simplesmente não é absorvido; `CityOccupancy.ResolveGrownBounds` alimenta essa lista com o crescimento PRÓPRIO (não cross-clamped) de cada outra cidade, um único nível não-recursivo pra não reintroduzir o blocker O(2^N) já corrigido nesta mesma feature. Fix 2 (`822ba4a`): `SpatialSettlementFoundingSystem.HandleEvent` agora reverifica a distância de absorção no disparo (`OverflowClusterFinder.IsWithinAbsorptionRangeOfAnyOtherCity`, exposto como `internal`), dropando a fundação silenciosamente se alguma cidade cresceu pra dentro do alcance durante a espera. Um teste existente (`ResolveGrownBounds_absorbs_an_overflow_building_only_into_its_own_city_even_when_another_city_is_geometrically_closer`) tinha uma premissa matematicamente incompatível com o novo invariante (prédio geometricamente mais perto de uma cidade estrangeira do que o próprio anel de absorção) — renomeado/reescrito pra `..._never_absorbs_an_overflow_building_into_a_city_that_is_not_its_owner` com a asserção correta. Gate: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"` em 223/223 (220 antes + 3 testes novos), 0 falhas.
- **Fixes/commits desta feature** (T1-T8 + 3 rondas de fix pós-Verifier + AD-007): `16b3ea8 dcdd4ec 77cb124 76940f6 7400416 25bb02c 2fb2895 f15acf3` (T1-T7), depois `596824f 2133401` (round-1 Verifier: blocker O(2^N) na resolução de ocupação + major do clamp de mapa/land-scarcity), depois `9a517bf 7fcfb61 3fe4c18 42e4305 e9524d1 142dd08` (round-2 Verifier: 6 gaps minor, incluindo um bug real — projeto de construção land-scarce sendo descartado sem retry), depois `f2219bc e48b15a` (AD-007: fila de construção passou a pular-adiante um projeto land-scarce em vez de bloquear os outros atrás dele, decisão de produto tomada em chat depois de 3 opções apresentadas). Ver `validation.md` pro histórico completo das 4 rodadas do Verifier.
- **In-progress**: nenhum — feature fechada, `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"` em 220/220, sensor de mutação 0 sobreviventes na rodada final.
- **Achado incidental corrigido no caminho** (fora do escopo desta feature, mas bloqueava o gate dela): `CropSystem.cs` (trabalho Stage-4 não commitado, pré-existente na working tree) não replantava no mesmo tick da colheita, cortando a produção de comida ~pela metade e colapsando a população em `PopulationBaselineTests`/`ScaleScenarioFixtureTests`. Corrigido (uncommitted, pertence à sessão de Stage-4 do usuário, não a esta feature) — `PopulationBaselineTests` voltou a passar depois do fix. `FamineCausalChainTests`/`ScarcityPriceCausalTests` ainda precisam de re-tuning dos limiares de choque pro novo modelo de ciclo de plantio (não tocado, decisão do usuário).
- **Uncommitted files**: a working tree já tinha um volume grande de trabalho Stage-4 não commitado ANTES desta sessão começar (relocation/crops/founding-site-picker/etc. — `RelocationArrivalSystem.cs`, `FoundingSitePicker.cs`, `ConstructionDemandSystem.cs` entre outros, mais o fix do `CropSystem.cs` acima) — nenhum deles foi tocado/commitado por esta sessão alem do fix pontual do crop replant; o usuário disse que vai commitar esse trabalho separadamente. `HEAD` (`e48b15a`) sozinho NÃO compila sem essa camada não commitada (T5 bundlou um hunk de `MigrationSystem.cs` que depende de `Household.BeginRelocation`/`RelocationArrivalSystem.cs` ainda não commitados — aceito explicitamente pelo usuário, ver `validation.md`).
- **Next step**: usuário decide — (a) commitar o resto do trabalho Stage-4 solto na working tree (relocation/crops/founding-site-picker), (b) retomar Fase 15.1 Stage 4 oficial em T10 (Demand-driven construction — ver `.specs/features/phase-15.1-stage-4-living-world/tasks.md`, ainda não tocado nesta sessão, T1-T9 done), ou (c) re-tunar os testes causais de fome/escassez pro novo modelo de crops.
- **Blockers**: nenhum técnico.
- **Branch**: main.

---
