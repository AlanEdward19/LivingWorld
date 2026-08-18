# Simulation Observability Gap
> City rules exist as isolated systems but are disconnected from normal runtime and realtime UI

Entry: `src/LivingWorld.Simulation/ScenarioRunner.cs:DefaultSystems()`

Runtime:
- City systems absent from default clock: growth, migration, construction, materialization, founding
- Tests instantiate them directly; search `tests/LivingWorld.Tests/Cities`

Semantics:
- `Cities/SettlementFoundingSystem.cs:HandleEvent()` — new city uses mother location; transfers entire aggregate pool
- `Cities/MigrationSystem.cs:Tick()` — changes CityId only; no route or `MoveTo()`
- `Cities/ConstructionSystem.cs:StartConstruction()` — requires external initiation; no demand-driven starter
- `Economy/EmploymentSystem.cs:Tick()` — assigns pre-authored workplace; no workplace creation
- `Behavior/BehaviorDecisionSystem.cs:TravelDestinationOf()` — destinations only household/market, not employer

UI boundary:
- `Api/Visual/ScopeTickDelta.cs` — only NPC position/removal
- `Api/Visual/GlobalProjector.cs:Build()` — ActiveEvents always empty
- `web/src/state/simulationStore.ts:applyDelta()` — cannot receive action/city/building changes
- `web/src/components/CityView.tsx:buildingEntities` — client lays buildings out in an artificial ring

Impact: isolated tests can pass while the playable app never runs or displays city evolution.

Updated: 2026-08-13
