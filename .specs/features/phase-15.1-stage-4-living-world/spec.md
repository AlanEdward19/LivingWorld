# Phase 15.1 — Stage 4: Living World Integration Specification
**Status**: Draft for approval

## Problem Statement
The engine contains population, behavior, economy, city, history, narrative, period, and
conversation capabilities, but many are absent from the production clock or meaningful frontend use.
Some “complete” actions also collapse material chains: sleep ignores furniture, wheat is eaten raw,
and farms create wheat and water instantly. Engine tests therefore do not yet produce a visibly alive world.

## Product Rule
Every capability that changes, explains, or lets the player interact with the living world SHALL be
visible or meaningfully used by the client. A final-state label does not count when the motor models—or
the product promise requires—the causal steps. `capability-matrix.md` is the canonical inventory.

## Goals
- Join living-world systems into one deterministic API → realtime → React vertical slice.
- Show who acts, why, where, with what, with whom, and the result through friendly visual feedback.
- Complete the minimum embodied chains for rest, food, crops, and water where the engine is superficial.
- Fail CI when a living system/event has no declared frontend consumer.
- Preserve aggregate/materialized LOD; anonymous population never becomes giant world NPCs.

## Out of Scope
| Feature | Reason |
| --- | --- |
| Combat, roads, trade vehicles, politics | Integrate the current living-world scope first |
| New art pipeline or 3D/skeletal animation | Existing 2D visual language is extended (LWV-07) |
| Advanced agriculture, cuisine, logistics, or item crafting | Stage 4 implements the minimum causal chain only |
| Cell identity for aggregate population | Only materialized NPCs have identity and position |
| Omniscient truth or engine debug data for players | UI exposes meaningful permitted knowledge |

## Assumptions & Open Questions
| Decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Coverage | All `ISimulationSystem`, `WorldEventKind`, and living APIs are classified | Makes “everything” executable | Yes |
| Presentation | Map, inspector, HUD, timeline, visual cue, or interaction count | Not every mechanic belongs on map | Yes |
| Rest quality | Ground/house/furniture are catalogued rest places with different recovery efficiency | Generalizes current homeless penalty | Yes |
| Food | Resources declare raw-edibility; recipes create edible meals; wheat is not edible by default | Prevents generic “food id” fiction | Yes |
| Crops | Planting creates a seeded batch with maturity tick and declared water demand | Makes farming temporal and visible | Yes |
| Water | Materialized NPC fetches from a valid source and carries it to a target stock | No remote water generation | Yes |
| Realtime | Typed state/event deltas extend the existing channel | Replay without polling | No |
| Knowledge | Player receives beliefs; truth stays in authorized diagnostics | Preserves LLM/history boundary | No |
| Settlement mobility | Emigration between cities needs ≥2 cities; **founding can create a 2nd city from one**; commute stays intra-footprint | Matches `MigrationSystem` + `SettlementFoundingSystem` | Yes |

This changes the authored scenario/API catalog in place after approval; no parallel v2 is required.
Existing spectator auth remains unchanged; realtime retry/order and transitions are specified below.

## P1 User Stories ⭐ MVP
### LWV-01 — No living mechanic is orphaned
1. WHEN CI enumerates concrete systems and event kinds THEN each SHALL have exactly one capability
   classification and a tested presentation consumer or justified `DiagnosticOnly` exclusion.
2. WHEN a living capability/event is added without a consumer THEN the coverage test SHALL fail.
3. WHEN a capability is diagnostic-only THEN it SHALL not appear as fictional world behavior.

### LWV-02 — People visibly live, relate, learn, and work
1. WHEN a materialized NPC acts THEN map and inspector SHALL show named action, reason, target,
   destination/progress, household, job, needs, health, relationships, and skill progress.
2. WHEN work, teaching, courtship, marriage, birth, death, or relationship changes THEN affected state
   SHALL update and an audience-safe human-readable event SHALL appear in the timeline.
3. WHEN work requires a destination THEN the NPC SHALL commute to a real workplace and work only there;
   absent capacity SHALL create demand rather than fake work.
4. WHEN focus requires identities THEN materialization SHALL be used; aggregates remain counts.

### LWV-03 — Rest, food, farming, and water are embodied
1. WHEN an NPC sleeps THEN it SHALL use a reachable valid rest place; recovery SHALL derive from its
   declared quality, and the client SHALL show sleep state, remaining duration, location, and a friendly
   cue such as animated `Zzz` with a non-visual accessible label.
2. WHEN an NPC eats THEN it SHALL consume a resource declared edible in its current preparation state;
   UI SHALL identify the food and whether it was raw or prepared. Raw wheat SHALL not satisfy hunger.
3. WHEN wheat is planted THEN a crop batch SHALL progress until its declared maturity tick, consume its
   declared water inputs, and become harvestable only when mature; no worker SHALL create instant wheat.
4. WHEN water is needed THEN a materialized worker SHALL route to a valid source, collect a conserved
   quantity, carry it, and deliver it before irrigation, cooking, drinking, or stock use can complete.
5. WHEN food requires preparation THEN a worker SHALL bring declared inputs to a valid cooking place,
   complete the recipe, and create the edible output; each step SHALL have progress and a visual cue.

### LWV-04 — Settlements visibly evolve through inhabitants
1. WHEN capacity is missing THEN residents SHALL request construction; completion SHALL create the
   authoritative building/workplace and update housing, employment, stocks, and economy surfaces.
2. WHEN households migrate or found a settlement THEN members SHALL travel and change membership only
   on arrival; founders choose a distinct seeded valid location and population is conserved each tick.
3. WHEN city indicators or construction change THEN city/building inspectors SHALL update without reload.
4. WHEN construction is queued or in progress THEN the city map SHALL show a visible site/scaffold and
   progress cue until the authoritative building exists.
5. WHEN a completed building exists THEN the city map SHALL place it at the API-projected coordinate,
   not only at a client-side ring approximation.
6. WHEN settlement founding completes THEN the world map SHALL show the new city at its seeded site and
   the timeline SHALL name the founding; the client SHALL not require a pre-existing second city.
7. WHEN a household migrates between two existing cities THEN the world map SHALL show travel and apply
   membership only after arrival.

### LWV-05 — History, periods, narrative, and conversation are used
1. WHEN facts/reports/books/corrections change THEN permitted knowledge SHALL be browsable without truth leaks.
2. WHEN biographies/chronicles exist THEN inspectors SHALL narrate engine-confirmed events with fallback.
3. WHEN conversation is available THEN the NPC inspector SHALL expose it; invalid proposals SHALL not mutate.
4. WHEN period evolves THEN HUD/catalog SHALL refresh labels and show the transformation event.

### LWV-06 — Realtime reconstructs canonical state
1. WHEN entity, action, process, indicator, knowledge, or event changes THEN subscribers SHALL receive
   an ordered typed final-state delta for that tick, including action/process progress.
2. WHEN deltas replay THEN React state SHALL equal a fresh projection; duplicates are idempotent, gaps resnapshot.
3. WHEN an entity crosses scope THEN origin removal and destination upsert SHALL share the tick.

### LWV-07 — NPC actions and life events have animated 2D cues
1. WHEN a materialized NPC performs a declared `ActionType` or active process THEN the map token
   SHALL show a data-driven animated cue (CSS/canvas) with an accessible non-visual label; unknown
   actions SHALL fall back to a static icon, never a blank tile.
2. WHEN work, cooking, construction, water, crop, eat, sleep, or socialize processes run THEN progress
   SHALL drive a visible animation or staged cue until completion or cancellation.
3. WHEN birth, death, courtship, marriage, or related `WorldEventKind` values fire for a materialized
   actor THEN a short audience-safe moment animation SHALL appear at the event location and the timeline
   SHALL retain its human-readable label.
4. WHEN `prefers-reduced-motion` is set THEN motion SHALL stop but the cue icon/label SHALL remain visible.
5. WHEN CI enumerates `ActionType`, Stage 4 process descriptors, and LWV-07 event kinds THEN each SHALL
   map to exactly one animation spec in the unified catalog or the coverage test SHALL fail.

## Edge Cases, Verification, and Traceability
- Blocked/cancelled/dead actors never teleport, consume remotely, or apply unfinished effects.
- Action cues are cosmetic projections; only seeded motor state decides completion and outcomes.
- Focused bounded scenarios assert each tick; the 100-year suite remains nightly only.
- Traceability: LWV-01..07 are **In Design**; success requires 7/7 plus complete capability coverage.
