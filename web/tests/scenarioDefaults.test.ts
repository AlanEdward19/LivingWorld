import { describe, expect, it } from "vitest";
import { defaultScenarioForm, jsonToScenarioForm, scenarioFormToJson } from "../src/scenarioDefaults";

describe("scenarioFormToJson", () => {
  it("turns every settlement painted on the map into a founded city at the same coordinate", () => {
    const form = defaultScenarioForm();
    form.settlements = [
      { name: "Norte", x: 1, y: 2 },
      { name: "Sul", x: 3, y: 7 },
      { name: "Leste", x: 8, y: 4 },
      { name: "Oeste", x: 0, y: 5 },
    ];

    const scenario = JSON.parse(scenarioFormToJson(form));

    expect(scenario.Cities).toEqual([
      { X: 1, Y: 2, Name: "Norte", FoundedAtTick: 0, AggregatePool: { Count: 0, WealthSum: 0, HealthSum: 0 } },
      { X: 3, Y: 7, Name: "Sul", FoundedAtTick: 0, AggregatePool: { Count: 0, WealthSum: 0, HealthSum: 0 } },
      { X: 8, Y: 4, Name: "Leste", FoundedAtTick: 0, AggregatePool: { Count: 0, WealthSum: 0, HealthSum: 0 } },
      { X: 0, Y: 5, Name: "Oeste", FoundedAtTick: 0, AggregatePool: { Count: 0, WealthSum: 0, HealthSum: 0 } },
    ]);
    expect([scenario.VillageX, scenario.VillageY]).toEqual([1, 2]);
  });

  it("keeps the blank-world economy off until a productive economy is authored", () => {
    const scenario = JSON.parse(scenarioFormToJson(defaultScenarioForm()));

    expect(scenario.EconomyEnabled).toBe(false);
    expect(scenario.Workplaces).toEqual([]);
  });

  it("keeps extraordinary disabled and empty by default", () => {
    const form = defaultScenarioForm();
    const scenario = JSON.parse(scenarioFormToJson(form));

    expect(form.extraordinaryEnabled).toBe(false);
    expect(scenario.Extraordinary).toEqual({ Enabled: false, Descriptors: [] });
  });

  it("serializes and reloads every extraordinary descriptor axis as generic scenario data", () => {
    const form = defaultScenarioForm();
    form.extraordinaryEnabled = true;
    form.extraordinaryDescriptors = [{
      id: "descriptor-1", source: "source-tag", effects: "movement:1, health:-2",
      mode: "Active", costs: "fatigue:2", reliability: "ResolutionCheck",
      failureModes: "loss-of-control", intrinsicVulnerabilities: "source-disruption",
      manifestations: "state:visible", appearanceScaleMultiplier: 1.4,
      appearanceSkinTint: "#88ccff", appearanceMovementTrail: "dust",
      needSubstitutionReplacesNeed: "hunger", needSubstitutionResourceId: 9,
      needSubstitutionUnitsPerUse: 2, senescenceRateMultiplier: 0,
      manifestationCondition: "world:is-night",
      acquisitionRules: "condition-tag",
    }];

    const scenario = JSON.parse(scenarioFormToJson(form));
    expect(scenario.Extraordinary).toEqual({
      Enabled: true,
      Descriptors: [{
        Id: "descriptor-1", Source: "source-tag", Effects: ["movement:1", "health:-2"],
        Mode: "Active", Costs: ["fatigue:2"], Reliability: "ResolutionCheck",
        FailureModes: ["loss-of-control"], IntrinsicVulnerabilities: ["source-disruption"],
        Manifestations: ["state:visible"],
        AcquisitionRules: ["condition-tag"],
        Appearance: { ScaleMultiplier: 1.4, SkinTint: "#88ccff", MovementTrail: "dust" },
        NeedSubstitution: { ReplacesNeed: "hunger", ResourceId: 9, UnitsPerUse: 2 },
        SenescenceRateMultiplier: 0,
        ManifestationCondition: "world:is-night",
      }],
    });
    expect(jsonToScenarioForm(scenario).extraordinaryDescriptors).toEqual(form.extraordinaryDescriptors);
  });

  it("omits unconfigured extraordinary optional objects and restores their exact defaults", () => {
    const form = defaultScenarioForm();
    form.extraordinaryDescriptors = [{
      id: "plain", source: "source", effects: "health:1", mode: "Passive", costs: "",
      reliability: "Guaranteed", failureModes: "", intrinsicVulnerabilities: "",
      manifestations: "", acquisitionRules: "", appearanceScaleMultiplier: 1,
      appearanceSkinTint: "", appearanceMovementTrail: "", needSubstitutionReplacesNeed: "",
      needSubstitutionResourceId: null, needSubstitutionUnitsPerUse: 1,
      senescenceRateMultiplier: 1, manifestationCondition: "",
    }];

    const descriptor = JSON.parse(scenarioFormToJson(form)).Extraordinary.Descriptors[0];
    expect(descriptor).toEqual({
      Id: "plain", Source: "source", Effects: ["health:1"], Mode: "Passive", Costs: [],
      Reliability: "Guaranteed", FailureModes: [], IntrinsicVulnerabilities: [],
      Manifestations: [], AcquisitionRules: [], SenescenceRateMultiplier: 1,
    });
    expect(jsonToScenarioForm({ Extraordinary: { Enabled: true, Descriptors: [descriptor] } })
      .extraordinaryDescriptors[0]).toMatchObject({
        appearanceScaleMultiplier: 1, appearanceSkinTint: "", appearanceMovementTrail: "",
        needSubstitutionReplacesNeed: "", needSubstitutionResourceId: null,
        needSubstitutionUnitsPerUse: 1, senescenceRateMultiplier: 1, manifestationCondition: "",
      });
  });

  it("converts a city draft's buildings from the local canvas into world-absolute coordinates", () => {
    const form = defaultScenarioForm();
    form.width = 100;
    form.height = 100;
    form.initialPopulation = 64; // citySide(64, ...) = 4 (redondo, sem deslocamento fracionário)
    form.settlements = [{ name: "Vila", x: 50, y: 50 }];

    const scenario = JSON.parse(scenarioFormToJson(form, {
      0: { buildings: [{ x: 2, y: 2, rotation: 90 }, { x: 0, y: 0 }] },
    }));

    expect(scenario.Buildings).toEqual([
      { CityIndex: 0, BuildingTypeId: 1, X: 50, Y: 50, Orientation: 90 },
      { CityIndex: 0, BuildingTypeId: 1, X: 48, Y: 48, Orientation: 0 },
    ]);
  });

  it("clamps out-of-map draft buildings onto the nearest valid cell and drops collisions instead of failing", () => {
    const form = defaultScenarioForm();
    form.width = 10;
    form.height = 10;
    form.initialPopulation = 64; // citySide(64, 10, 10) = 4 (limitado pelo mapa, ainda redondo)
    form.settlements = [{ name: "Vila", x: 5, y: 5 }];

    const scenario = JSON.parse(scenarioFormToJson(form, {
      // 2,2 é o centro do canvas local (side/2): mapeia pra célula exata do assentamento.
      0: { buildings: [{ x: 2, y: 2 }, { x: 2, y: 2 }, { x: -1000, y: -1000 }] },
    }));

    expect(scenario.Buildings).toEqual([
      { CityIndex: 0, BuildingTypeId: 1, X: 5, Y: 5, Orientation: 0 },
      { CityIndex: 0, BuildingTypeId: 1, X: 0, Y: 0, Orientation: 0 },
    ]);
  });

  it("omits Buildings entirely when the creator was never opened for a settlement", () => {
    const form = defaultScenarioForm();
    form.settlements = [{ name: "Vila", x: 5, y: 5 }];

    const scenario = JSON.parse(scenarioFormToJson(form));

    expect(scenario.Buildings).toEqual([]);
  });
});
