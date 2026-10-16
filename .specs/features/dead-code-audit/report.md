# Dead Code Audit — Relatório Consolidado

Gerado por varredura em `src/` (6 projetos), cruzando referências com `tests/`. Nenhum código foi alterado — só inventário e recomendação, decisão de aplicar é do usuário.

## Resumo executivo

| Categoria | Achados de ação direta | Achados "verificar manualmente" | Comentários fase/task pra limpar |
| --- | --- | --- | --- |
| Domain | 0 remover | 1 (`MovementRouteKind`) | ~280 ocorrências / 106 arquivos |
| Simulation | 1 remover (`ScenarioLoader` v1) | 2 (`DecisionSandbox`, `DecisionMetrics` — órfãos de teste) + 1 (`ExampleCounterSystem` — placeholder velho) | 397 ocorrências / 137 arquivos |
| Infrastructure | 0 remover | 1 (`WorldDbContextFactory` — EF tooling) + 1 (`ColdTierPersistence`/`ColdTierCompressionCodec` — órfão de teste) | 22 ocorrências / 12 arquivos |
| Api | 0 remover | 0 | 172 ocorrências / 34 arquivos |
| AI | 0 remover | 0 | 8 ocorrências / 3 arquivos (todas com WHY, nada pra remover só aparar citação) |
| Workers | 0 remover direto | 1 (`Worker` — possível template não usado) | 9 ocorrências / 1 arquivo |

**Total: ~888 comentários poluídos por fase/task em ~193 arquivos únicos.**

---

## P1 — Achado mais importante: correção da premissa do spec

O spec (`.specs/features/dead-code-audit/spec.md:77`) assumia que `ScenarioLoader` (v1) tinha 4 consumidores de produção (`WorldPreviewEndpoints`, `CityScenarioLoader`, `ExtraordinaryRuntimePlan`, `PeriodDefinitionValidator`). **Isso está desatualizado.** Os agentes Simulation e Api confirmaram independentemente:

- Esses 4 nomes na verdade chamam classes **diferentes e não relacionadas**: `MapScenarioLoader`, `CityScenarioLoader` (ela mesma, não `ScenarioLoader`), `ExtraordinaryScenarioLoader`, e por aí — nenhum chama `LivingWorld.Simulation.Scenarios.ScenarioLoader.LoadWorld`.
- `ScenarioLoader.LoadWorld` real tem **zero consumidores de produção** — só é chamado em `tests/LivingWorld.Tests.LongRunning/Population/PopulationArchitectureTests.cs:63,88`.
- `ScenarioLoaderV2.LoadWorld` é o que roda em produção (`WorldCreateEndpoints.cs:27`, `WorldStartService.cs:25`, etc.), com 7+ arquivos de teste também.

**Recomendação (AC3 do spec): candidato a remoção direta**, condicionado a migrar (ou confirmar que não precisa) o teste em `PopulationArchitectureTests.cs` para `ScenarioLoaderV2` antes de apagar `ScenarioLoader`.

---

## Candidatos a remoção direta — status final após execução

| Item | Arquivo | Status real |
| --- | --- | --- |
| `ScenarioLoader` (classe inteira) | `src/LivingWorld.Simulation/Scenarios/ScenarioLoader.cs` | **Removido.** Zero consumidor de produção confirmado. Migrar `PopulationArchitectureTests.cs` pra `ScenarioLoaderV2` falhou de início (schema V2 exige Economy/City/Behavior/Dynamics completo que os JSONs legados não tinham) — resolvido migrando o conteúdo de `scenarios/default.json`/`test-scifi.json` pro schema novo (a pedido do usuário), em vez de manter o v1. Ver `.specs/features/dead-code-audit/tasks.md` T1/T2 pro detalhe da migração e do que falta rodar (Integration/Scenario, usuário). |
| `DecisionMetrics`/`ModeRun`/`ModeComparison` | `src/LivingWorld.Simulation/Behavior/Decision/DecisionMetrics.cs` | **Não é dead code — mantido.** `tests/LivingWorld.Tests.Integration/Scenario/LivingVillageScenarioTests.cs:73-99` (`Doc85_metrics_baseline_is_collected_for_test_living_village`, `Category=Scenario`) chama `DecisionMetrics.CompareFullVsEventDriven` com asserts reais (não é autoteste da classe). Cai no caso já previsto pela própria spec (P1 AC3): sem consumidor de produção, mas com consumidor de teste real → não remover. |

## Verificar manualmente (não remover sem checar)

| Item | Arquivo | Por quê |
| --- | --- | --- |
| `Worker` (BackgroundService) | `src/LivingWorld.Workers/Worker.cs` | Template padrão do Generic Host (heartbeat de log a cada 1s), sem outro comportamento. Tem 1 consumidor real (`Program.cs:166`, `AddHostedService<Worker>()`) então não é "morto", mas parece cruft de template nunca substituído — confirmar se serve pra algo. |
| `DecisionSandbox` (+ `DecisionSandboxRequest/Result`) | `src/LivingWorld.Simulation/Behavior/Decision/DecisionSandbox.cs` | Zero consumidor em `src/`, só testes. Órfão de produção — ferramenta de diagnóstico sem uso real hoje. |
| `DecisionMetrics` (+ `ModeRun`/`ModeComparison`) | `src/LivingWorld.Simulation/Behavior/Decision/DecisionMetrics.cs` | Mesma situação — só testes chamam. |
| `ExampleCounterSystem` | `src/LivingWorld.Simulation/Scheduling/ExampleCounterSystem.cs` | Doc própria diz "descartável quando a Fase 3 chegar com sistemas reais" — projeto já passou da Fase 28 e ele continua registrado em `ScenarioRunner.DefaultSystems()`. Tem consumidor real, então não entra como "remover" cego — checar se é smoke-test de scheduling ainda necessário. |
| `ColdTierPersistence` / `ColdTierCompressionCodec` | `src/LivingWorld.Infrastructure/EventLog/ColdTierPersistence.cs` | Implementado e testado (`ColdTierPersistenceTests.cs`), mas zero chamador em `src/` (nenhuma DI, nenhum repositório usa). Decidir: plugar em produção ou remover-com-teste. |
| `WorldDbContextFactory` | `src/LivingWorld.Infrastructure/EfCore/WorldDbContextFactory.cs` | Zero referência de código, mas é descoberto por convenção pelo `dotnet ef migrations` (`IDesignTimeDbContextFactory<T>`). Não remover — é infra de tooling. |
| `MovementRouteKind` | `src/LivingWorld.Domain/Population/LazyPosition.cs` | Só usado dentro do próprio arquivo — confirmar se algum consumidor externo escolhe o valor via switch em outro lugar antes de tratar como interno. |

---

## Duplicação — candidatos a consolidação

**Alta confiança:**
- `SqliteWorldRepository` (`LoadLatestSnapshot`/`LoadSnapshotAt`) vs `SqlitePeriodTemplateRepository` (`FindLatestVersion`/`Find`) — mesmo padrão "latest por chave composta" / "lookup exato por versão". Candidato a `IVersionedRepository<TKey,TRecord>` ou helper LINQ compartilhado.
- `GlobalLayerBuilder.Build` vs `CityLayerBuilder.Build` (Api) — mesmo switch-com-fallback `ArgumentOutOfRangeException`; o próprio comentário de `CityLayerBuilder` já admite "mesmo padrão de fallback".
- `SettlementFoundingSystem` vs `SpatialSettlementFoundingSystem` (Simulation) — mesma fórmula de threshold+scheduling, doc própria confirma reuso da "MESMA fórmula". Geometria de materialização difere (grid vs cluster) — extrair só o threshold/scheduling.
- `Resolve(declared) => declared ?? Default` repetido idêntico em `AttentionRules`, `PowerUtilityRules`, `PowerInheritanceRules`, `BodyRules` (Domain) — candidato a helper genérico `Resolve<T>(T? declared, T default)`.

**Média confiança:**
- `SqlitePeriodTemplateRepository.Save` vs `SqliteWorldRepository.SaveSnapshotWithEvents` (Infra) — semântica reject-vs-upsert diferente, cuidado ao unir.
- `HistoryDistortionDigest` vs `HistoryCorrectionDigest` (Simulation) — mesma forma de digest determinístico sobre cadeia causal.
- `HistoryBeliefQuery` / `HistoryTruthQuery` / `NpcBeliefQuery` (Simulation) — mesmo padrão "ponto único de acesso" Verdade/Crença.
- `CityProjector.Build` vs `InteriorProjector.Build` (Api) — mesma assinatura `Result<TSnapshot> Build(WorldState, TId)`.
- `ResourceStock.Withdraw` vs `Money.TryDebit` (Domain) — mesmo fluxo guard→Fail/Ok, tipos de saldo diferentes (dicionário vs escalar).

**Baixa confiança (registrado, não recomendado agir):**
- `FloraLifecycleSystem` vs `FaunaLifecycleSystem`, `SkillPracticeSystem` vs `SkillTeachingSystem` (Simulation) — parecidos na forma, mecânica interna diferente o bastante pra não forçar abstração.

---

## Comentários fase/task — volume e padrão

**~888 ocorrências em ~193 arquivos.** Padrão dominante (~90%+ dos casos): citação de fase/task/ADR **acompanhada** de uma explicação real (invariante, constraint, motivo de design) — nesses casos a ação é só cirúrgica: **tirar a citação, manter a explicação**.

Uma minoria é citação pura sem WHY — candidatos a reescrita completa ou remoção do comentário:
- `src/LivingWorld.Domain/History/WorldEventKind.cs` — maioria dos `<summary>` de membros de enum só restatam o nome com "(Fase N)" — arquivo inteiro é candidato a limpeza.
- `src/LivingWorld.Domain/Geography/GeographyIds.cs`
- `src/LivingWorld.Workers/Program.cs:31,41,51,60,69,79` — 6 comentários "Fase 10: digest de X para teste de dois processos" repetidos, sem WHY além do óbvio.
- `src/LivingWorld.Api/Simulation/TickLoopService.cs:81`, `ConversationEndpoints.cs:31-32`, `ScopeTickDelta.cs:27-28`, `GlobalProjector.cs:84`

Dado o volume, reescrita linha-a-linha fica pra uma execução futura aprovada, agrupada por projeto/subpasta (Domain e Simulation concentram ~76% do total).

---

## Notas de escopo

- Migrations do EF Core (`EfCore/Migrations/*`) excluídas em todo o projeto, conforme spec.
- Nenhum tipo foi recomendado para remoção sem contagem real de consumidores (produção + teste).
- `LivingWorld.AI` e `LivingWorld.Api` saíram limpos de P1/P2 — nenhuma ação de remoção pendente ali, só aparar citações de fase/task.
