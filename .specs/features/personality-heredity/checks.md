# Personality heredity checks

Profile: light
Plan: `.specs/features/personality-heredity/plan.md`

8 checks in 2 slices · 1 one-way door · 0 open

## Checks

### S1 - Herança mensurável

**C1** - A newborn trait is `50 + h x (mean of the parents + segregation - 50) + sqrt(1 - h^2) x (independent roll - 50)`, cut to [0,100] (variance-preserving); at `h = 0` it is exactly the independent roll of before (AC 1, AC 5, door 1)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~PersonalityInheritanceTests.With_weight_zero"`

**C2** - Every trait stays in [0,100] over 1,000 births in 20 seeds and with parents at the extremes (AC 2)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~PersonalityInheritanceTests.Every_trait_stays_in_range"`

**C3** - The same birth gives the same personality, in one process and across two processes (AC 3)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~PersonalityInheritanceTests.The_same_birth"`; `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~DeterminismTwoProcessTests"`

**C4** - `NatalitySystem` uses the scenario weight: a baby of two average parents lands in 14 to 86 (average plus segregation of 35.7) at full heritability (AC 1)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~NatalitySystemTests.A_baby_of_two_average_parents"`

### S2 - Famílias se parecem

**C5** - Over 200 sibling pairs the mean personality distance is below the stranger distance with z >= 3 (AC 4)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~PersonalityInheritanceTests.Siblings_are_closer"`

**C6** - The population variance of a trait is preserved over 6 generations at h = 0.7 (ratio 0.8 to 1.2), a child of an unknown father stays in range, and the scenario weight survives a snapshot round trip (AC 2)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~PersonalityInheritanceTests.The_population_variance|FullyQualifiedName~PersonalityInheritanceTests.A_child_with_an_unknown_father|FullyQualifiedName~PersonalityInheritanceTests.The_scenario_weight"`

**C6b** - With weight 0 (control) siblings are not closer than strangers; a weight outside [0,1] (NaN included) is rejected by the family rules (AC 5)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~PersonalityInheritanceTests.Without_inheritance|FullyQualifiedName~PersonalityInheritanceTests.A_weight_outside"`

**C7** - Time per NPC-tick and allocation per tick are unchanged (inheritance runs once per birth) and the hash is identical in two processes (AC 6)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~MeasureHarness|FullyQualifiedName~DeterminismTwoProcessTests|FullyQualifiedName~GoldenHashesTests"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| door 1: mixing formula (3) | independent roll at h = 0 C1 · mix at 0 < h <= 1 C4 and C5 · clamp C2 | - |
| weight cases (3) | zero C1 and C6 · default 0.5 C5 · invalid C6 | - |

- Claims naming a status code or response shape: none - no HTTP surface
- No other check claims more than the single case its proof exercises

## Swept

- validation: C6 (weight outside [0,1]), C2 (range)
- failure modes: C6 (invalid rules fail at creation)
- idempotency: n/a - the mix is computed once at birth from a dedicated stream
- authorization: n/a - local simulation, no caller identity
- concurrency: n/a - single-threaded tick
- data lifecycle: n/a - no stored field is added, the traits keep their shape
- dependency failure: n/a - no external dependency
- state transitions: n/a - the personality never changes after birth
- observability: n/a - no new event

## Out of scope

- FAM-32; appearance genetics; ontogeny - see the plan

## Deviations (kept true)

- The plan said the mix is the parent mean plus a uniform noise of ±10 at weight `w_gene` 0.5. The independent verifier measured that a plain mean halves the trait variance each generation (808 down to 230-325 after one). The shipped formula is the infinitesimal model that keeps the variance (segregation ±35.7, `sqrt(1 - h^2)` weight on the independent roll); the scenario default is 0.7 (heritability h^2 about 0.5, the range of twin studies), sibling correlation h^2/2.
- The 10 independent rolls are always drawn first and kept (so `h = 0` is the previous behavior byte for byte); at `h > 0` ten more noise draws follow from the same dedicated stream.
- The plan said `OverlapCoefficient` gains a caller in the selection test; the helper was removed in AD-095 and the sibling test uses a distance and a z score instead.
- The scenario default is 0.5 in `ScenarioRunner` (the family rules have no JSON source); rules built elsewhere default to 0 (no inheritance).
- The default re-recorded goldens, pins and the collapse baseline on purpose (AD-099).

## Handoff

Five source files plus tests, one builder.
